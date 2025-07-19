using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Quartz;

namespace LineBotScheduler
{
    /// <summary>
    /// Quartz 工作，負責向設定的 LINE 群組傳送文字訊息。
    /// </summary>
    public class NotifyJob : IJob
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly LineOptions _options;

        /// <summary>
        /// 建立 <see cref="NotifyJob"/> 的新實例。
        /// </summary>
        public NotifyJob(IHttpClientFactory httpClientFactory, IOptions<LineOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        /// <summary>
        /// 將 JobData 中的訊息送至 LINE API。
        /// </summary>
        public async Task Execute(IJobExecutionContext context)
        {
            var message = context.MergedJobDataMap.GetString("message") ?? string.Empty;
            // 準備呼叫 LINE Push API 的 HTTP 請求
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
            // 若推播失敗則記錄錯誤
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to send message: {response.StatusCode}");
            }
        }
    }
}
