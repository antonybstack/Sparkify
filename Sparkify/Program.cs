using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Common.Configuration;
using Common.Observability;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
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

// builder.Services.AddAntiforgery();

builder.Services.AddHttpsRedirection(options => options.HttpsPort = appOptions.Port);

// string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
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
    // options.ReturnHttpNotAcceptable = true;
    options.InputFormatters.Add(new Formatters.TextPlainInputFormatter());
    options.InputFormatters.Add(new Formatters.RawRequestBodyFormatter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument();

// builder.Services.AddSwaggerGen(c =>
// {
//     c.UseInlineDefinitionsForEnums();
//     // c.AddMissingSchemas();
// });
builder.RegisterOpenTelemetry(otlpOptions);
builder.RegisterSerilog(otlpOptions);
/* DEPENDENCY INJECTION (SERVICES) SECTION */
// builder.Services.TryAddSingleton<IEventChannel, EventChannel>();
// builder.Services.AddHostedService<SubscriptionWorker>();
builder.Services.AddHttpClient<FaviconHttpClient>(static client =>
{
    client.DefaultRequestHeaders.UserAgent
        .ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36");
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
    // client.DefaultRequestVersion = new Version(2, 0);
});

// builder.Services.Configure<ForwardedHeadersOptions>(static options =>
// {
//     options.ForwardedHeaders =
//         ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
// });

// builder.Services.AddResponseCaching();

// builder.Services.AddRateLimiter(static options =>
// options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
// {
//     return RateLimitPartition.GetFixedWindowLimiter(httpContext.Request.Headers.Host.ToString(),
//         partition =>
//             new FixedWindowRateLimiterOptions
//             {
//                 PermitLimit = 5, AutoReplenishment = true, Window = TimeSpan.FromSeconds(10)
//             });
// }));
// options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, IPAddress>(context =>
// {
//     var remoteIpAddress = context.Connection.RemoteIpAddress;
//     if (remoteIpAddress is null)
//     {
//         return RateLimitPartition.GetNoLimiter(IPAddress.Loopback);
//     }
//     if (!IPAddress.IsLoopback(remoteIpAddress!))
//     {
//         return RateLimitPartition.GetFixedWindowLimiter(remoteIpAddress,
//             static _ =>
//                 new FixedWindowRateLimiterOptions { PermitLimit = 1, AutoReplenishment = true, Window = TimeSpan.FromMilliseconds(100), QueueLimit = 5, QueueProcessingOrder = QueueProcessingOrder.NewestFirst });
//     }
//     return RateLimitPartition.GetNoLimiter(IPAddress.Loopback);
// }));
//
// DbManager.HttpUriString = configuration.GetValue<string>("Urls:RavenDb:Http");
// DbManager.TcpHostName = configuration.GetValue<string>("Urls:RavenDb:TcpHostName");
// DbManager.TcpPort = configuration.GetValue<int>("Urls:RavenDb:TcpPort");
DbManager.CreateStore(databaseOptions.Name, databaseOptions.Http, databaseOptions.TcpHostName, databaseOptions.TcpPort);
var app = builder.Build();

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseRouting();
// app.UseAntiforgery();
// app.UseRateLimiter();
app.UseCors();
// app.UseResponseCaching();

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
    // app.UseSwagger(
    //     c => { c.RouteTemplate = "api/{documentName}/swagger.json"; } // documentName is version number
    // );
    // app.UseSwaggerUI(c =>
    // {
    //     c.RoutePrefix = "api";
    //     c.SwaggerEndpoint("v1/swagger.json", "Sparkify API v1");
    //     c.DisplayRequestDuration();
    // });

    const string HtmlContent = """
                               <!DOCTYPE html>
                               <html lang=""en"">
                                   <head>
                                       <title>Sparkifyy</title>
                                       <link rel=\"icon\" href=\"data:,\">
                                   </head>
                                   <body>
                                       <h1>Sparkify</h1>
                                       <body style=\"background: rgb(43, 42, 51); color: #333;\">Hello Sparkify!</body>
                                   </body>
                               </html>
                               """;
    app.MapGet("",
        (HttpContext context) =>
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

            // Manual Instrumentation
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
    // app.MapPaymentApi();
}

app.MapBlogsApi();

app.Map("error",
    async context =>
        await context.Response.WriteAsync(
            "An error occurred. The server encountered an error and could not complete your request.")
);
app.MapFallback(static async context => { await context.Response.WriteAsync("Page not found"); });
// log all endpoints
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

// app.MapGet("/testtt",
//     static (context) =>
//     {
//         var rawRequestData = context.Request.BodyReader.ReadAsync().Result;
//         var data = Encoding.UTF8.GetString(rawRequestData.Buffer);
//         return context.Response.WriteAsync("Hello World!");
//     });

// string GetOrCreateFilePath(string fileName, string filesDirectory = "uploadFiles")
// {
//     var directoryPath = Path.Combine(app.Environment.ContentRootPath, filesDirectory);
//     Directory.CreateDirectory(directoryPath);
//     return Path.Combine(directoryPath, fileName);
// }
//
// // Function to upload the file with the specified name
// async Task UploadFileWithName(IFormFile file, string fileSaveName)
// {
//     var filePath = GetOrCreateFilePath(fileSaveName);
//     await using var fileStream = new FileStream(filePath, FileMode.Create);
//     await file.CopyToAsync(fileStream);
// }

// app.MapPost("/upload",
//         async (IFormFile file) =>
//         {
//             var fileSaveName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
//             await UploadFileWithName(file, fileSaveName);
//             return TypedResults.Ok("File uploaded successfully!");
//         })
//     .DisableAntiforgery();

// Add OpenAPI 3.0 document serving middleware
// Available at: http://localhost:<port>/swagger/v1/swagger.json
// app.UseOpenApi();
//
// // Add web UIs to interact with the document
// // Available at: http://localhost:<port>/swagger
// app.UseSwaggerUi3();

// app.UseReDoc(options =>
// {
//     options.Path = "/redoc";
// });

app.Run();
