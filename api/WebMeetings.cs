using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json;
using NotifySlackOfWebMeeting.Apis.Entities;
using NotifySlackOfWebMeeting.Apis.Queries;
using FluentValidation;
using Newtonsoft.Json.Serialization;

namespace NotifySlackOfWebMeeting.Apis
{
    /// <summary>
    /// Web会議情報を取得・追加するためのファンクションを定義するクラス
    /// </summary>
    public static class WebMeetings
    {
        /// <summary>
        /// Web会議情報を登録します。
        /// </summary>
        /// <param name="req">HTTPリクエスト。</param>
        /// <param name="documentsOut">CosmosDBのドキュメント。</param>
        /// <param name="log">ロガー。</param>
        /// <returns></returns>
        [FunctionName("AddWebMeetings")]
        public static async Task<IActionResult> AddWebMeetings(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "WebMeetings")] HttpRequest req,
            [CosmosDB(
                databaseName: "notify-slack-of-web-meeting-db",
                collectionName: "WebMeetings",
                ConnectionStringSetting = "CosmosDbConnectionString")]IAsyncCollector<dynamic> documentsOut,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            string message = string.Empty;

            try
            {

                log.LogInformation("POST webMeetings");

                // リクエストのBODYからパラメータ取得
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);

                // エンティティに設定
                var webMeeting = new WebMeeting()
                {
                    Name = data?.name,
                    StartDateTime = data?.startDateTime ?? DateTime.UnixEpoch,
                    Url = data?.url,
                    RegisteredBy = data?.registeredBy,
                    SlackChannelId = data?.slackChannelId
                };

                // 入力値チェックを行う
                var validator = new WebMeetingValidator();
                validator.ValidateAndThrow(webMeeting);

                // Web会議情報を登録
                message = await AddWebMetting(documentsOut, webMeeting);
            }
            catch(Exception ex) 
            {
                return new BadRequestObjectResult(ex.Message);
            }

            return new OkObjectResult($"This HTTP triggered function executed successfully.\n{message}");
        }

        /// <summary>
        /// Web会議情報一覧を取得します。
        /// </summary>
        /// <param name="req">HTTPリクエスト。</param>
        /// <param name="client">CosmosDBのドキュメントクライアント。</param>
        /// <param name="log">ロガー。</param>
        /// <returns></returns>
        [FunctionName("GetWebMeetings")]
        public static async Task<IActionResult> GetWebMeetings(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "WebMeetings")] HttpRequest req,
            [CosmosDB(
                databaseName: "notify-slack-of-web-meeting-db",
                collectionName: "WebMeetings",
                ConnectionStringSetting = "CosmosDbConnectionString")]DocumentClient client,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            string message = string.Empty;

            try
            {
                log.LogInformation("GET webMeetings");


                // クエリパラメータから検索条件パラメータを設定
                WebMeetingsQueryParameter queryParameter = new WebMeetingsQueryParameter()
                {
                    FromDate = req.Query["fromDate"],
                    ToDate = req.Query["toDate"],
                    RegisteredBy = req.Query["registeredBy"],
                    SlackChannelId = req.Query["slackChannelId"]
                };

                // 入力値チェックを行う
                var queryParameterValidator = new WebMeetingsQueryParameterValidator();
                queryParameterValidator.ValidateAndThrow(queryParameter);

                // Web会議情報を取得
                message = JsonConvert.SerializeObject(await GetWebMeetings(client, queryParameter));
            }
            catch(Exception ex) 
            {
                return new BadRequestObjectResult(ex.Message);
            }

            return new OkObjectResult($"This HTTP triggered function executed successfully.\n{message}");
        }

        /// <summary>
        /// Web会議情報を取得する。
        /// </summary>
        /// <param name="req">HTTPリクエスト。</param>
        /// <param name="client">CosmosDBのドキュメントクライアント。</param>
        /// <param name="log">ロガー。</param>
        /// <returns>Web会議情報</returns>
        [FunctionName("GetWebMeetingById")]
        public static async Task<IActionResult> GetWebMeetingById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "WebMeetings/{ids}")] HttpRequest req,
            [CosmosDB(
                databaseName: "notify-slack-of-web-meeting-db",
                collectionName: "WebMeetings",
                ConnectionStringSetting = "CosmosDbConnectionString")
                ]DocumentClient client,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            string message = string.Empty;

            try
            {
                string ids = req.RouteValues["ids"].ToString();
                log.LogInformation($"GET webMeetings/{ids}");

                // クエリパラメータから検索条件パラメータを設定
                WebMeetingsQueryParameter queryParameter = new WebMeetingsQueryParameter()
                {
                    Ids = ids
                };

                // Web会議情報を取得
                var documentItems = await GetWebMeetings(client, queryParameter);

                if(!documentItems.Any())
                {
                    return new BadRequestObjectResult($"Target item not found. Id={ids}");
                }
                message = JsonConvert.SerializeObject(documentItems);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ex);
            }

            return new OkObjectResult($"This HTTP triggered function executed successfully.\n{message}");
        }


        [FunctionName("DeleteWebMettingById")]
        public static async Task<IActionResult> DeleteWebMeetingById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "WebMeetings/{ids}")] HttpRequest req,
            [CosmosDB(
                databaseName: "notify-slack-of-web-meeting-db",
                collectionName: "WebMeetings",
                ConnectionStringSetting = "CosmosDbConnectionString")
                ]DocumentClient client,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processd a request.");
            var message = string.Empty;

            try
            {
                var ids = req.RouteValues["ids"].ToString();
                log.LogInformation($"DELETE webMeetings/{ids}");

                // Web会議情報を削除
                var documentItems = await DeleteWebMeetingById(client, ids);

                if(!documentItems.Any())
                {
                    return new BadRequestObjectResult($"Target item not found. Id={ids}");
                }
                message = JsonConvert.SerializeObject(documentItems);
            }
            catch(Exception ex)
            {
                return new BadRequestObjectResult(ex);
            }
            return new OkObjectResult($"This HTTP triggered function executed successfully.\n{message}");
        }

        /// <summary>
        /// Web会議情報を追加する
        /// </summary>
        /// <param name="documentsOut">CosmosDBのドキュメント</param>
        /// <param name="webMeeting">Web会議情報</param>
        /// <returns>追加したWeb会議情報の文字列</returns>
        internal static async Task<string> AddWebMetting(
            IAsyncCollector<dynamic> documentsOut,
            WebMeeting webMeeting)
        {
            // 登録日時にUTCでの現在日時を設定
            webMeeting.RegisteredAt = DateTime.UtcNow;
            string documentItem = JsonConvert.SerializeObject(webMeeting, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
            await documentsOut.AddAsync(documentItem);
            return documentItem;
        }

        /// <summary>
        /// Web会議情報一覧を取得する。
        /// </summary>
        /// <param name="client">CosmosDBのドキュメントクライアント</param>
        /// <param name="queryParameter">抽出条件パラメータ</param>
        /// <returns></returns>
         internal static async Task<IEnumerable<WebMeeting>> GetWebMeetings(
            DocumentClient client,
            WebMeetingsQueryParameter queryParameter
        ) {
            // Get a JSON document from the container.
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri("notify-slack-of-web-meeting-db", "WebMeetings");
            IDocumentQuery<WebMeeting> query = client.CreateDocumentQuery<WebMeeting>(collectionUri, new FeedOptions{ EnableCrossPartitionQuery = true, PopulateQueryMetrics = true})
                .Where(queryParameter.GetWhereExpression())
                .AsDocumentQuery();

            var documentItems = new List<WebMeeting>();
            while (query.HasMoreResults)
            {
                foreach (var documentItem in await query.ExecuteNextAsync<WebMeeting>())
                {
                    documentItems.Add(documentItem);
                }
            }
            return documentItems;
        }

        /// <summary>
        /// Web会議情報を削除する。
        /// </summary>
        /// <param name="client">CosmosDBのドキュメントクライアント</param>
        /// <param name="ids">削除するWeb会議情報のID</param>
        /// <returns>削除したWeb会議情報</returns>
        internal static async Task<IEnumerable<WebMeeting>> DeleteWebMeetingById(
                   DocumentClient client,
                   string ids)
        {
            // 削除に必要なパーティションキーを取得するため、Web会議情報を取得後に削除する。

            // クエリパラメータに削除するWeb会議情報のIDを設定
            WebMeetingsQueryParameter queryParameter = new WebMeetingsQueryParameter()
            {
                Ids = ids
            };

            // Web会議情報を取得
            var documentItems = await GetWebMeetings(client, queryParameter);
            foreach (var documentItem in documentItems)
            {
                // パーティションキーを取得
                var partitionKey = documentItem.DateUnixTimeSeconds;
                // Web会議情報を削除
                // Delete a JSON document from the container.
                Uri documentUri = UriFactory.CreateDocumentUri("notify-slack-of-web-meeting-db", "WebMeetings", documentItem.Id);
                await client.DeleteDocumentAsync(documentUri, new RequestOptions() { PartitionKey = new PartitionKey(partitionKey) });
            }

            return documentItems;
        }
    }
}
