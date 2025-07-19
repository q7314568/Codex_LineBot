using System.Globalization; // 解析日期字串用
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Quartz;
using LineBotScheduler;

// 主程式入口。讀取使用者輸入的上課日期，利用 Quartz.NET 排程兩則 LINE 推播，
// 並啟動 Hosted Service 於指定時間執行。

var builder = Host.CreateDefaultBuilder(args)
    .UseContentRoot(AppContext.BaseDirectory)
    .ConfigureServices((context, services) =>
    {
        // 將設定節點繫結至 LineOptions 供 DI 使用
        services.Configure<LineOptions>(context.Configuration.GetSection("Line"));

        // 註冊 HttpClient 以便呼叫 LINE API
        services.AddHttpClient();
        services.AddQuartz(q =>
        {
            // 透過 DI 建立 Job
            q.UseMicrosoftDependencyInjectionJobFactory();
        });
        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });
        services.AddTransient<NotifyJob>();
    });

using var host = builder.Build();

Console.Write("Enter class date (yyyy-MM-dd): ");
var input = Console.ReadLine();
if (!DateTime.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var classDate))
{
    Console.WriteLine("Invalid date format.");
    return;
}

// 啟動宿主環境讓 Quartz 運行
await host.StartAsync();

var schedulerFactory = host.Services.GetRequiredService<ISchedulerFactory>();
var scheduler = await schedulerFactory.GetScheduler();

// 兩個提醒時間：前一晚與當日早上
var remindTimes = new[]
{
    classDate.AddDays(-1).Date.AddHours(21),
    classDate.Date.AddHours(7)
};

int index = 1;
foreach (var time in remindTimes)
{
    // 建立於排定時間推播提醒的 Job
    var job = JobBuilder.Create<NotifyJob>()
        .WithIdentity($"notifyJob{index}")
        .UsingJobData("message", $"Class reminder for {classDate:yyyy-MM-dd}")
        .Build();

    // 為此提醒時間設定 Trigger
    var trigger = TriggerBuilder.Create()
        .WithIdentity($"trigger{index}")
        .StartAt(time)
        .ForJob(job)
        .Build();

    await scheduler.ScheduleJob(job, trigger);
    index++;
}

// 通知群組提醒已排程成功
await SendLineMessageAsync(host.Services, $"\u5df2\u6210\u529f\u8a2d\u5b9a {classDate:yyyy-MM-dd} \u7684\u63d0\u9192");

Console.WriteLine("Scheduler started. Press Ctrl+C to exit.");
await host.WaitForShutdownAsync(); // 讓程式持續運行

/// <summary>
/// 向設定的 LINE 群組傳送單一文字訊息。
/// </summary>
static async Task SendLineMessageAsync(IServiceProvider services, string message)
{
    var options = services.GetRequiredService<IOptions<LineOptions>>().Value;
    var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();

    // 準備 LINE Push API 的 HTTP 請求
    using var client = httpClientFactory.CreateClient();
    var request = new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/v2/bot/message/push");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ChannelAccessToken);

    var payload = new
    {
        to = options.GroupId,
        messages = new[] { new { type = "text", text = message } }
    };
    request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    var response = await client.SendAsync(request);
    // 若發送失敗則輸出提示訊息
    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine($"Failed to send confirmation message: {response.StatusCode}");
    }
}

