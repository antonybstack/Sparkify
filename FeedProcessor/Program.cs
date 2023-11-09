using System.Globalization;
using Common.Configuration;
using Common.Observability;
using Sparkify;
using Sparkify.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService();

builder.Services.Configure<HostOptions>(static options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
    options.ServicesStartConcurrently = true;
    options.ServicesStopConcurrently = true;
});

var appOptions = builder.AddConfigAndValidate<FeedProcessorAppOptions, ValidateFeedProcessorAppOptions>();
var databaseOptions = builder.AddConfigAndValidate<DatabaseOptions, ValidateDatabaseOptions>();
var otlpOptions = builder.AddConfigAndValidate<OtlpOptions, ValidateOtlpOptions>();

builder.RegisterOpenTelemetry(otlpOptions);
builder.RegisterSerilog(otlpOptions);

DbManager.CreateStore(databaseOptions.Name, databaseOptions.Http, databaseOptions.TcpHostName, databaseOptions.TcpPort);

DbManager.Store.ClearAllRefreshMetadata();
DbManager.Store.DeleteNonUniqueArticles();
DbManager.Store.InitializeRssBlogFeeds();

builder.Services.AddSingleton<Processor>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

await host.RunAsync();

//
// builder.Services.AddHttpClient<RssHttpClient>(static client =>
// {
//     client.DefaultRequestHeaders.UserAgent
//         .ParseAdd(
//             "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36");
//     client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
//     // client.DefaultRequestVersion = new Version(2, 0);
// });

// var httpClient = new HttpClient();
// httpClient.DefaultRequestHeaders.UserAgent
//     .ParseAdd(
//         "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome");
//
// // get rss xml from "https://etsy.com/codeascraft/rss"
// httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://etsy.com/codeascraft/rss"))
//     .ContinueWith(static task =>
//     {
//         var response = task.Result;
//         var responseBytes = response.Content.ReadAsByteArrayAsync().Result;
//         var rawHtml = Encoding.UTF8.GetString(responseBytes);
//         var feed = FeedReader.ReadFromByteArray(responseBytes);
//         var rssBlogFeed = new RssBlogFeed
//         {
//             BlogId = "blogs/1-A"
//         };
//         using var memoryStream = new MemoryStream();
//         memoryStream.Write(responseBytes);
//         memoryStream.Position = 0;
//         var store = DbManager.Store;
//         store.OpenSession().Advanced.Attachments.Store(rssBlogFeed, $"{Ulid.NewUlid(feed.LastUpdatedDate ?? DateTimeOffset.UtcNow).ToString()}.rss", memoryStream, "application/rss+xml");
//         store.OpenSession().SaveChanges();
//     });
