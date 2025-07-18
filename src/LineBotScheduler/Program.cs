using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using LineBotScheduler;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.Configure<LineOptions>(context.Configuration.GetSection("Line"));
        services.AddHttpClient();
        services.AddQuartz(q =>
        {
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

await host.StartAsync();

var schedulerFactory = host.Services.GetRequiredService<ISchedulerFactory>();
var scheduler = await schedulerFactory.GetScheduler();

var remindTimes = new[]
{
    classDate.AddDays(-1).Date.AddHours(21),
    classDate.Date.AddHours(7)
};

int index = 1;
foreach (var time in remindTimes)
{
    var job = JobBuilder.Create<NotifyJob>()
        .WithIdentity($"notifyJob{index}")
        .UsingJobData("message", $"Class reminder for {classDate:yyyy-MM-dd}")
        .Build();

    var trigger = TriggerBuilder.Create()
        .WithIdentity($"trigger{index}")
        .StartAt(time)
        .ForJob(job)
        .Build();

    await scheduler.ScheduleJob(job, trigger);
    index++;
}

Console.WriteLine("Scheduler started. Press Ctrl+C to exit.");
await host.WaitForShutdownAsync();

