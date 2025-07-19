using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Quartz;

namespace LineBotScheduler
{
    /// <summary>
    /// Quartz job that sends a text message to the configured LINE group.
    /// </summary>
    public class NotifyJob : IJob
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly LineOptions _options;

        /// <summary>
        /// Initializes a new instance of <see cref="NotifyJob"/>.
        /// </summary>
        public NotifyJob(IHttpClientFactory httpClientFactory, IOptions<LineOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        /// <summary>
        /// Sends the message provided in the job data to the LINE API.
        /// </summary>
        public async Task Execute(IJobExecutionContext context)
        {
            var message = context.MergedJobDataMap.GetString("message") ?? string.Empty;
            // Prepare HTTP request to LINE Push API
            using var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/v2/bot/message/push");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ChannelAccessToken);
            var payload = new
            {
                to = _options.GroupId,
                messages = new[]
                {
                    new { type = "text", text = message }
                }
            };
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.SendAsync(request);
            // Log an error if the push message fails
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to send message: {response.StatusCode}");
            }
        }
    }
}
