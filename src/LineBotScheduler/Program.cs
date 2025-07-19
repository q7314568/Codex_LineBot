using System.Globalization; // For parsing date strings
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

// Entry point for the console application. This program reads a class date from
// the user, schedules two LINE push notifications via Quartz.NET, and starts a
// hosted service that executes the jobs at the desired times.

var builder = Host.CreateDefaultBuilder(args)
    .UseContentRoot(AppContext.BaseDirectory)
    .ConfigureServices((context, services) =>
    {
        // Bind configuration section to LineOptions so we can inject it later
        services.Configure<LineOptions>(context.Configuration.GetSection("Line"));

        // Register an HttpClient for sending requests to the LINE API
        services.AddHttpClient();
        services.AddQuartz(q =>
        {
            // Use DI for job creation
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

// Start hosting environment so Quartz can run
await host.StartAsync();

var schedulerFactory = host.Services.GetRequiredService<ISchedulerFactory>();
var scheduler = await schedulerFactory.GetScheduler();

// Two reminder times: the evening before and the morning of the class
var remindTimes = new[]
{
    classDate.AddDays(-1).Date.AddHours(21),
    classDate.Date.AddHours(7)
};

int index = 1;
foreach (var time in remindTimes)
{
    // Create a job that will push a reminder message at the scheduled time
    var job = JobBuilder.Create<NotifyJob>()
        .WithIdentity($"notifyJob{index}")
        .UsingJobData("message", $"Class reminder for {classDate:yyyy-MM-dd}")
        .Build();

    // Configure the trigger for the specified reminder time
    var trigger = TriggerBuilder.Create()
        .WithIdentity($"trigger{index}")
        .StartAt(time)
        .ForJob(job)
        .Build();

    await scheduler.ScheduleJob(job, trigger);
    index++;
}

// Notify the group that reminders have been successfully scheduled
await SendLineMessageAsync(host.Services, $"\u5df2\u6210\u529f\u8a2d\u5b9a {classDate:yyyy-MM-dd} \u7684\u63d0\u9192");

Console.WriteLine("Scheduler started. Press Ctrl+C to exit.");
await host.WaitForShutdownAsync(); // Keep the application running

/// <summary>
/// Sends a single text message to the configured LINE group.
/// </summary>
static async Task SendLineMessageAsync(IServiceProvider services, string message)
{
    var options = services.GetRequiredService<IOptions<LineOptions>>().Value;
    var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();

    // Prepare HTTP request to LINE Push API
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
    // Report failure so the user knows the message did not go through
    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine($"Failed to send confirmation message: {response.StatusCode}");
    }
}

