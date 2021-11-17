using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NotifySlackOfWebMeeting.Apis.Entities;
using FluentValidation;
using Newtonsoft.Json.Serialization;

namespace NotifySlackOfWebMeeting.Apis
{
    /// <summary>
    /// Web会議情報API
    /// </summary>
    public static class WebMeetings
    {
        [FunctionName("WebMeetings")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
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
                switch(req.Method)
                {
                    case "GET":
                        log.LogInformation("GET webMeetings");
                        break;
                    case "POST":
                        log.LogInformation("POST webMeetings");

                        // リクエストのBODYからパラメータ取得
                        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                        dynamic data = JsonConvert.DeserializeObject(requestBody);

                        // エンティティに設定
                        var webMeeting = new WebMeeting();
                        webMeeting.Name = data?.name;
                        webMeeting.StartDateTime = data?.startDateTime ?? DateTime.MinValue;
                        webMeeting.Url = data?.url;
                        webMeeting.RegisteredBy = data?.registeredBy;
                        webMeeting.SlackChannelId = data?.slackChannelId;

                        // 入力値チェックを行う
                        var validator = new WebMeetingValidator();
                        validator.ValidateAndThrow(webMeeting);

                        // Web会議情報を登録
                        message = await AddWebMetting(documentsOut, webMeeting);

                        break;
                    default:
                        throw new InvalidOperationException($"Invalid method: method={req.Method}");
                }
            }
            catch(Exception ex) 
            {
                return new BadRequestObjectResult(ex.Message);
            }

            return new OkObjectResult($"This HTTP triggered function executed successfully.\n{message}");
        }

        /// <summary>
        /// Web会議情報を追加する
        /// </summary>
        /// <param name="documentsOut">CosmosDBのドキュメント</param>
        /// <param name="webMeeting">Web会議情報</param>
        /// <returns>追加したWeb会議情報の文字列</returns>
        private static async Task<string> AddWebMetting(
            IAsyncCollector<dynamic> documentsOut,
            WebMeeting webMeeting)
        {
            // 登録日時にUTCでの現在日時を設定
            webMeeting.RegisteredAt = DateTime.UtcNow;
            string documentItem = JsonConvert.SerializeObject(webMeeting, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
            await documentsOut.AddAsync(documentItem);
            return documentItem;
        }
    }
}
