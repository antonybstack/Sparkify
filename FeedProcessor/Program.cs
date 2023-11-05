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

builder.Services.AddSingleton<Processor>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

await host.RunAsync();
