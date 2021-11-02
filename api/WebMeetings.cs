using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace NotifySlackOfWebMeeting.Apis
{
    /// <summary>
    /// Web会議情報API
    /// </summary>
    public static class WebMeetings
    {
        [FunctionName("webMeetings")]
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
                    string name = data?.name;
                    // 会議名が未指定の場合は無効な値とする。
                    if(string.IsNullOrEmpty(name))
                    {
                        return new BadRequestObjectResult("name is null or empty");
                    }
                    DateTime? startDateTime = data?.startDateTime;
                    // 日付が未指定もしくは今日以前の場合は無効な値とする。
                    if(!startDateTime.HasValue || startDateTime <= DateTime.Today)
                    {
                        return new BadRequestObjectResult("startDateTime is invalid. Please specify the date and time after tomorrow.");
                    }
                    string url = data?.url;
                    url = string.IsNullOrEmpty(url) ? string.Empty : data?.url;
                    string registeredAt = data?.registeredAt;
                    registeredAt = string.IsNullOrEmpty(registeredAt) ? string.Empty : data?.registeredAt;
                    string slackChannelId = data?.slackChannelId;
                    // SlackチャンネルIDが未指定の場合は無効な値とする。
                    if(string.IsNullOrEmpty(slackChannelId))
                    {
                        return new BadRequestObjectResult("slackChannelId is null or empty");
                    }
                    // Web会議情報を登録
                    message = await AddWebMetting(documentsOut, name, startDateTime.Value, url, registeredAt, slackChannelId);
    
                    break;
                default:
                    throw new InvalidOperationException($"Invalid method: method={req.Method}");
            }

            return new OkObjectResult($"This HTTP triggered function executed successfully.\n{message}");
        }

        /// <summary>
        /// Web会議情報を追加
        /// </summary>
        private static async Task<string> AddWebMetting(
            IAsyncCollector<dynamic> documentsOut,
            string name,
            DateTime startDateTime,
            string url,
            string registeredAt,
            string slackChannelId)
        {
            var documentItem = new
            {
                // create a random ID
                id = System.Guid.NewGuid().ToString(),
                name = name,
                date = startDateTime.Date.ToString("yyyy-MM-dd"),
                startDateTime = $"{startDateTime:O}",
                url = url,
                registeredAt = registeredAt,
                slackChannelId = slackChannelId
            };
            await documentsOut.AddAsync(documentItem);
            return JsonConvert.SerializeObject(documentItem);
        }
    }
}
