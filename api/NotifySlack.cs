using System;
using System.Text;
using System.Linq;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NotifySlackOfWebMeeting.Apis.Queries;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;

namespace NotifySlackOfWebMeeting.Apis
{
    /// <summary>
    /// Slackへ通知するファンクションを定義するクラス
    /// </summary>
    public class NotifySlack
    {
        #region フィールド

        /// <summary>
        /// Httpクライアント
        /// </summary>
        private readonly HttpClient m_HttpClient;

        #endregion

        #region 構築

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="httpClientFactory">Httpクライアントのファクトリ</param>
        public NotifySlack(IHttpClientFactory httpClientFactory)
        {
            m_HttpClient = httpClientFactory.CreateClient();
        }

        #endregion

        #region ファンクション

        #region Slackへ通知

        /// <summary>
        /// Slackに通知するファンクション
        /// </summary>
        [FunctionName("NotifySlack")]
        public async Task Run([TimerTrigger("0 0 9 * * 1-5")]TimerInfo myTimer, 
            [CosmosDB(
                databaseName: "notify-slack-of-web-meeting-db",
                collectionName: "WebMeetings",
                ConnectionStringSetting = "CosmosDbConnectionString")]DocumentClient client,
                ILogger log)
        {
            // 本日のWeb会議を取得
            var today = DateTime.UtcNow.Date.ToString("yyy-MM-dd");
            var webMeetingsParam = new WebMeetingsQueryParameter
            {
                FromDate = today,
                ToDate = today
            };

            // SlackチャンネルIdの一覧を取得
            var webMeetings = await WebMeetings.GetWebMeetings(client, webMeetingsParam);
            if(!webMeetings.Any()) return;
            var webMeetingsBySlackChannelMap = webMeetings.OrderBy(w => w.StartDateTime).GroupBy(w => w.SlackChannelId).ToDictionary(g => g.Key, ws => ws.OrderBy(w => w.StartDateTime).ToList());

            var slackChannelIds = webMeetingsBySlackChannelMap.Keys;
            if(!slackChannelIds.Any()) return;
            var slackChannelParam = new SlackChannelsQueryParameter{
                Ids = string.Join(", ",slackChannelIds)
            };
            var slackChannels = await SlackChannels.GetSlackChannels(client, slackChannelParam, log);

            foreach(var slackChannel in slackChannels)
            {
                var message = new StringBuilder($"{DateTime.Today.ToString("yyyy/MM/dd")}のWeb会議情報\n");
                foreach(var webMeeting in webMeetingsBySlackChannelMap[slackChannel.Id])
                {
                    message.AppendLine($"{webMeeting.StartDateTime.ToString("HH:mm")}～：{webMeeting.Name}\n\t{webMeeting.Url}");
                }
                var content = new StringContent(JsonConvert.SerializeObject(new {text = message.ToString()}), Encoding.UTF8, "application/json");
                var response = await m_HttpClient.PostAsync(slackChannel.WebhookUrl, content);

                if(response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    // 通知したWeb会議情報を削除する
                    await WebMeetings.DeleteWebMeetingById(client, string.Join(",", webMeetingsBySlackChannelMap[slackChannel.Id].Select(w => w.Id)));
                }
            }

            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }

        #endregion

        #endregion
    }
}
