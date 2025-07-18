using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Quartz;

namespace LineBotScheduler
{
    public class NotifyJob : IJob
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly LineOptions _options;

        public NotifyJob(IHttpClientFactory httpClientFactory, IOptions<LineOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var message = context.MergedJobDataMap.GetString("message") ?? string.Empty;
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
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to send message: {response.StatusCode}");
            }
        }
    }
}
