using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NotifySlackOfWebMeeting.Apis.Entities;
using NotifySlackOfWebMeeting.Apis.Queries;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using FluentValidation;
using Newtonsoft.Json.Serialization;

namespace NotifySlackOfWebMeeting.Apis
{
    /// <summary>
    /// Slackチャンネルの取得・追加をするためのファンクションを定義するクラス
    /// </summary>
    public static class SlackChannels
    {
        /// <summary>
        /// Slackチャンネル情報を登録する。
        /// </summary>
        /// <returns>登録したSlackチャンネル情報</returns>
        [FunctionName("AddSlackChannels")]
        public static async Task<IActionResult> AddSlackChannels(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "SlackChannels")] HttpRequest req,
            [CosmosDB(
                databaseName: "notify-slack-of-web-meeting-db",
                collectionName: "SlackChannels",
                ConnectionStringSetting = "CosmosDbConnectionString")]IAsyncCollector<dynamic> documentsOut,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            string message = string.Empty;

            try
            {

                log.LogInformation("POST SlackChannels");

                // リクエストのBODYからパラメータ取得
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);

                // エンティティに設定
                SlackChannel slackChannel = new SlackChannel()
                {
                    Name = data?.name,
                    WebhookUrl = data?.webhookUrl,
                    RegisteredBy = data?.registeredBy
                };

                // 入力値チェックを行う
                SlackChannelValidator validator = new SlackChannelValidator();
                validator.ValidateAndThrow(slackChannel);

                // Slackチャンネル情報を登録
                message = await AddSlackChannel(documentsOut, slackChannel);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ex);
            }

            return new OkObjectResult(message);
        }

        /// <summary>
        /// Slackチャンネル情報一覧を取得する。
        /// </summary>
        /// <param name="req">HTTPリクエスト</param>
        /// <param name="client">CosmosDBのドキュメントクライアント</param>
        /// <param name="log">ロガー</param>
        /// <returns>Slackチャンネル情報一覧</returns>
        [FunctionName("GetSlackChannels")]
        public static async Task<IActionResult> GetSlackChannels(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "SlackChannels")] HttpRequest req,
            [CosmosDB(
                databaseName: "notify-slack-of-web-meeting-db",
                collectionName: "SlackChannels",
                ConnectionStringSetting = "CosmosDbConnectionString")
                ]DocumentClient client,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            string message = string.Empty;

            try
            {
                log.LogInformation("GET SlackChannels");

                // クエリパラメータから検索条件パラメータを設定
                SlackChannelsQueryParameter queryParameter = new SlackChannelsQueryParameter()
                {
                    Ids = req.Query["ids"],
                    Name = req.Query["name"],
                    WebhookUrl = req.Query["webhookUrl"],
                    RegisteredBy = req.Query["registeredBy"]
                };

                // Slackチャンネル情報を取得
                message = JsonConvert.SerializeObject(await GetSlackChannels(client, queryParameter, log));
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ex);
            }

            return new OkObjectResult(message);
        }

        /// <summary>
        /// Slackチャンネル情報を取得する。
        /// </summary>
        /// <param name="req">HTTPリクエスト</param>
        /// <param name="client">CosmosDBのドキュメントクライアント</param>
        /// <param name="log">ロガー</param>
        /// <returns>Slackチャンネル情報</returns>
        [FunctionName("GetSlackChannelById")]
        public static IActionResult GetSlackChannelById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "SlackChannels/{id}")] HttpRequest req,
            [CosmosDB(
                databaseName: "notify-slack-of-web-meeting-db",
                collectionName: "SlackChannels",
                ConnectionStringSetting = "CosmosDbConnectionString",
                Id = "{id}", PartitionKey = "{id}")
                ]SlackChannel slackChannel,
            ILogger log)
        {
            string id = req.RouteValues["id"].ToString();
            log.LogInformation($"GET slackChannels/{id}");

            if (slackChannel == null)
            {
                return new NotFoundObjectResult($"Target item not found. Id={id}");
            }

            return new OkObjectResult(JsonConvert.SerializeObject(slackChannel));
        }

        /// <summary>
        /// Slackチャンネル情報を削除する。
        /// </summary>
        /// <param name="req">HTTPリクエスト</param>
        /// <param name="client">CosmosDBのドキュメントクライアント</param>
        /// <param name="log">ロガー</param>
        /// <returns>削除したSlackチャンネル情報</returns>
        [FunctionName("DeleteSlackChannelById")]
        public static async Task<IActionResult> DeleteSlackChannelById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "SlackChannels/{id}")] HttpRequest req,
            [CosmosDB(
                databaseName: "notify-slack-of-web-meeting-db",
                collectionName: "SlackChannels",
                ConnectionStringSetting = "CosmosDbConnectionString")
                ]DocumentClient client,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            string message = string.Empty;

            try
            {
                string id = req.RouteValues["id"].ToString();
                log.LogInformation($"DELETE slackChannels/{id}");

                // Slackチャンネル情報を削除
                var documentItems = await DeleteSlackChannelById(client, id, log);

                if(!documentItems.Any())
                {
                    return new NotFoundObjectResult($"Target item not found. Id={id}");
                }
                message = JsonConvert.SerializeObject(documentItems);

            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ex);
            }

            return new OkObjectResult(message);
        }

        /// <summary>
        /// Slackチャンネル情報を追加する
        /// </summary>
        /// <param name="documentsOut">CosmosDBのドキュメント</param>
        /// <param name="slackChannel">Slackチャンネル情報</param>
        /// <returns></returns>
        internal static async Task<string> AddSlackChannel(
            IAsyncCollector<dynamic> documentsOut,
            SlackChannel slackChannel)
        {
            // 登録日時にUTCでの現在日時を設定
            slackChannel.RegisteredAt = DateTime.UtcNow;
            string documentItem = JsonConvert.SerializeObject(slackChannel, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
            await documentsOut.AddAsync(documentItem);
            return documentItem;
        }

        /// <summary>
        /// Slackチャンネル情報一覧を取得する。
        /// </summary>
        /// <param name="client">CosmosDBのドキュメントクライアント</param>
        /// <param name="queryParameter">抽出条件パラメータ</param>
        /// <param name="log">ロガー</param>
        /// <returns>Slackチャンネル情報一覧</returns>
        internal static async Task<IEnumerable<SlackChannel>> GetSlackChannels(
                   DocumentClient client,
                   SlackChannelsQueryParameter queryParameter,
                   ILogger log
                   )
        {
            // Get a JSON document from the container.
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri("notify-slack-of-web-meeting-db", "SlackChannels");
            IDocumentQuery<SlackChannel> query = client.CreateDocumentQuery<SlackChannel>(collectionUri, new FeedOptions { EnableCrossPartitionQuery = true, PopulateQueryMetrics = true })
                .Where(queryParameter.GetWhereExpression())
                .AsDocumentQuery();

            var documentItems = new List<SlackChannel>();
            while (query.HasMoreResults)
            {
                foreach (var documentItem in await query.ExecuteNextAsync<SlackChannel>())
                {
                    documentItems.Add(documentItem);
                }
            }
            return documentItems;
        }

        /// <summary>
        /// Slackチャンネル情報を削除する。
        /// </summary>
        /// <param name="client">CosmosDBのドキュメントクライアント</param>
        /// <param name="ids">削除するSlackチャンネル情報のID</param>
        /// <param name="log">ロガー</param>
        /// <returns>削除したSlackチャンネル情報</returns>
        private static async Task<IEnumerable<SlackChannel>> DeleteSlackChannelById(
                   DocumentClient client,
                   string ids,
                   ILogger log)
        {
            // 事前に存在確認後に削除

            // クエリパラメータに削除するSlackチャンネル情報のIDを設定
            SlackChannelsQueryParameter queryParameter = new SlackChannelsQueryParameter()
            {
                Ids = ids,
            };

            // Slackチャンネル情報を取得
            var documentItems = await GetSlackChannels(client, queryParameter, log);
            foreach (var documentItem in documentItems)
            {
                // Slackチャンネル情報を削除
                // Delete a JSON document from the container.
                Uri documentUri = UriFactory.CreateDocumentUri("notify-slack-of-web-meeting-db", "SlackChannels", documentItem.Id);
                await client.DeleteDocumentAsync(documentUri, new RequestOptions() { PartitionKey = new PartitionKey(documentItem.Id) });
            }

            return documentItems;
        }
    }
}
