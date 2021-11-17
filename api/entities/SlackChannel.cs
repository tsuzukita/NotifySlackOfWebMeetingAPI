using System;

namespace NotifySlackOfWebMeeting.Apis.Entities
{
    /// <summary>
    /// Slackチャンネルを表す
    /// </summary>
    public class SlackChannel
    {
        /// <summary>
        /// 既定のコンストラクタ
        /// </summary>
        public SlackChannel()
        {
            Id = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 一意とするID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Slackチャンネル名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// SlackチャンネルのWebhook URL
        /// </summary>
        public string WebhookUrl { get; set; }

        /// <summary>
        /// 登録者
        /// </summary>
        public string RegisteredBy { get; set; }

        /// <summary>
        /// 登録日時（UTC）
        /// </summary>
        public DateTime RegisteredAt { get; set; }
    }
}