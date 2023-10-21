using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Common.Configuration;
using Common.Observability;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Sparkify;
using Sparkify.Features.BlogFeatures;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWindowsService();

ServicePointManager.DefaultConnectionLimit = 10000;

var appOptions = builder.AddConfigAndValidate<ApiOptions, ValidateApiOptions>();
var databaseOptions = builder.AddConfigAndValidate<DatabaseOptions, ValidateDatabaseOptions>();
var otlpOptions = builder.AddConfigAndValidate<OtlpOptions, ValidateOtlpOptions>();

builder.WebHost
    .UseQuic()
    .UseKestrel((hostingContext, options) =>
    {
        options.ListenAnyIP(appOptions.Port,
            listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                listenOptions.UseHttps();
                if (hostingContext.HostingEnvironment.IsDevelopment())
                {
                    listenOptions.UseConnectionLogging();
                }
            });
    });

builder.Services.AddHttpsRedirection(options => options.HttpsPort = appOptions.Port);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (!builder.Environment.IsProduction())
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(appOptions.AllowedOrigins)
                .AllowAnyHeader()
                .WithMethods(HttpMethods.Get);
        }
    });
});
builder.Services.Configure<JsonOptions>(static options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
});

builder.Services.AddControllers(options =>
{
    options.RespectBrowserAcceptHeader = true;
    options.InputFormatters.Add(new Formatters.TextPlainInputFormatter());
    options.InputFormatters.Add(new Formatters.RawRequestBodyFormatter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument();

builder.RegisterOpenTelemetry(otlpOptions);
builder.RegisterSerilog(otlpOptions);
/* DEPENDENCY INJECTION (SERVICES) SECTION */
builder.Services.AddHttpClient<FaviconHttpClient>(static client =>
{
    client.DefaultRequestHeaders.UserAgent
        .ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36");
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
});

builder.Services.Configure<ForwardedHeadersOptions>(static options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services.AddResponseCaching();

builder.Services.AddRateLimiter(static options =>
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, IPAddress>(static context =>
    {
        var remoteIpAddress = context.Connection.RemoteIpAddress;
        if (remoteIpAddress is null)
        {
            return RateLimitPartition.GetNoLimiter(IPAddress.Loopback);
        }
        if (!IPAddress.IsLoopback(remoteIpAddress!))
        {
            return RateLimitPartition.GetFixedWindowLimiter(remoteIpAddress,
                static _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 1,
                        AutoReplenishment = true,
                        Window = TimeSpan.FromMilliseconds(100),
                        QueueLimit = 5,
                        QueueProcessingOrder = QueueProcessingOrder.NewestFirst
                    });
        }
        return RateLimitPartition.GetNoLimiter(IPAddress.Loopback);
    }));

DbManager.CreateStore(databaseOptions.Name, databaseOptions.Http, databaseOptions.TcpHostName, databaseOptions.TcpPort);
var app = builder.Build();

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseCors();
app.UseResponseCaching();

app.RegisterSerilogRequestLogging();
app.LogStartupInfo(builder);

/* MIDDLEWARE SECTION */
// see middleware order at https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-8.0#middleware-order
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
}

if (app.Environment.IsDevelopment())
{
    const string HtmlContent = """
                               <!DOCTYPE html>
                               <html lang=""en"">
                                   <head>
                                       <title>Sparkify</title>
                                       <link rel=\"icon\" href=\"data:,\">
                                   </head>
                                   <body>
                                       <h1>Sparkify</h1>
                                       <body style=\"background: rgb(43, 42, 51); color: #333;\">Hello Sparkify!</body>
                                   </body>
                               </html>
                               """;
    app.MapGet("",
        static (HttpContext context) =>
        {
            context.Response.ContentType = "text/html";
            return HtmlContent;
        });

    app.MapGet("api/systeminfo",
        async static (HttpContext context, ILogger<Program> logger) =>
        {
            var systemInfo = new
            {
                RuntimeInformation.OSDescription,
                RuntimeInformation.OSArchitecture,
                RuntimeInformation.ProcessArchitecture,
                Environment.ProcessorCount,
                Environment.SystemPageSize
            };
            logger.LogInformation("systeminfo executed!");
            logger.LogTrace("systeminfo executed trace!");

            using var activity =
                Common.Observability.OpenTelemetry.Config.ActivitySource.StartActivity("SystemInfoActivity",
                    ActivityKind.Server);
            activity?.SetTag("foo", 1);
            activity?.SetTag("bar", "Hello, World!");
            activity?.SetTag("baz",
                new[]
                {
                    1, 2, 3
                });

            await context.Response.WriteAsJsonAsync(systemInfo);
        });
}

app.MapBlogsApi();

app.Map("error",
    static context =>
        context.Response.WriteAsync(
            "An error occurred. The server encountered an error and could not complete your request.")
);
app.MapFallback(static async context => { await context.Response.WriteAsync("Page not found"); });

// log all endpoint paths
app.Lifetime.ApplicationStarted.Register(() =>
{
    using var scope = app.Services.CreateScope();
    var dataSource = scope.ServiceProvider.GetRequiredService<EndpointDataSource>();
    var kestrelServer = scope.ServiceProvider.GetRequiredService<IServer>();
    var baseUrl = kestrelServer.Features.Get<IServerAddressesFeature>()?.Addresses.First();
    app.Logger.LogInformation("Open API: {Route}/api", baseUrl);
    foreach (var endpoint in dataSource.Endpoints)
    {
        if (endpoint is not RouteEndpoint routeEndpoint)
        {
            continue;
        }
        app.Logger.LogInformation("{Route}/{RawText} : {DisplayName}",
            baseUrl,
            routeEndpoint.RoutePattern.RawText,
            routeEndpoint.DisplayName);
    }
});

app.Run();
