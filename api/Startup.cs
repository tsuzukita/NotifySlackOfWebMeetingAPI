using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace NotifySlackOfWebMeeting.Apis
{
    /// <summary>
    /// スタートアップクラス
    /// </summary>
    public class Startup : FunctionsStartup
    {
        /// <summary>
        /// DIコンテナへのインスタンス登録
        /// </summary>
        /// <param name="builder"></param>
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient();
        }
    }
}