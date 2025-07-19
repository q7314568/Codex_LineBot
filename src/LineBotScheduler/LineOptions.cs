namespace LineBotScheduler
{
    /// <summary>
    /// Configuration options for connecting to the LINE Messaging API.
    /// </summary>
    public class LineOptions
    {
        /// <summary>
        /// Channel access token issued by LINE Developers.
        /// </summary>
        public string ChannelAccessToken { get; set; } = string.Empty;

        /// <summary>
        /// Target group ID to which notifications should be sent.
        /// </summary>
        public string GroupId { get; set; } = string.Empty;
    }
}
