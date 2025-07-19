namespace LineBotScheduler
{
    /// <summary>
    /// 連線至 LINE Messaging API 的設定項目。
    /// </summary>
    public class LineOptions
    {
        /// <summary>
        /// 從 LINE Developers 取得的 Channel Access Token。
        /// </summary>
        public string ChannelAccessToken { get; set; } = string.Empty;

        /// <summary>
        /// 接收推播訊息的群組 ID。
        /// </summary>
        public string GroupId { get; set; } = string.Empty;
    }
}
