using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sparkify.Features.BlogFeatures;
using Sparkify.Features.Payment;
using Sparkify.Observability;

// using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);
ServicePointManager.DefaultConnectionLimit = 10000;

builder.WebHost
    .UseQuic()
    .UseKestrel(options =>
    {
        options.ListenAnyIP(443,
            listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                listenOptions.UseHttps();
                // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/connection-middleware?view=aspnetcore-8.0
                // listenOptions.UseConnectionLogging();
            });
    });

builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);
// string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins("https://sparkify.dev", "https://www.sparkify.dev")
                .AllowAnyHeader()
                .WithMethods("GET");
        }
    });
});
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.UseInlineDefinitionsForEnums());
builder.RegisterOpenTelemetry();
builder.RegisterSerilog();
/* DEPENDENCY INJECTION (SERVICES) SECTION */
builder.Services.TryAddSingleton<IEventChannel, EventChannel>();
// builder.Services.AddHostedService<SubscriptionWorker>();
builder.Services.AddHttpClient<FaviconHttpClient>(static client =>
{
    client.DefaultRequestHeaders.UserAgent
        .ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36");
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
    // client.DefaultRequestVersion = new Version(2, 0);
});

builder.Services.AddRateLimiter(static options =>
    // options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    // {
    //     return RateLimitPartition.GetFixedWindowLimiter(httpContext.Request.Headers.Host.ToString(),
    //         partition =>
    //             new FixedWindowRateLimiterOptions
    //             {
    //                 PermitLimit = 5, AutoReplenishment = true, Window = TimeSpan.FromSeconds(10)
    //             });
    // }));
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, IPAddress>(context =>
    {
        var remoteIpAddress = context.Connection.RemoteIpAddress;
        if (remoteIpAddress is null)
        {
            return RateLimitPartition.GetNoLimiter(IPAddress.Loopback);
        }
        // if (!IPAddress.IsLoopback(remoteIpAddress!))
        // {
        return RateLimitPartition.GetFixedWindowLimiter(remoteIpAddress,
            static _ =>
                new FixedWindowRateLimiterOptions { PermitLimit = 1, AutoReplenishment = true, Window = TimeSpan.FromMilliseconds(100), QueueLimit = 5, QueueProcessingOrder = QueueProcessingOrder.NewestFirst });
        // }
        // return RateLimitPartition.GetNoLimiter(IPAddress.Loopback);
    }));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseCors();
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

const string htmlContent = """
                           <!DOCTYPE html>
                           <html lang=""en"">
                               <head>
                                   <meta charset=""UTF-8"">
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
        return htmlContent;
    });
app.UseSwagger(
    c => { c.RouteTemplate = "api/{documentName}/swagger.json"; } // documentName is version number
);
app.UseSwaggerUI(c =>
{
    c.RoutePrefix = "api";
    c.SwaggerEndpoint("v1/swagger.json", "Sparkify API v1");
    c.DisplayRequestDuration();
});
app.MapGet("api/systeminfo",
    async (HttpContext context, ILogger<Program> logger) =>
    {
        var systemInfo = new { RuntimeInformation.OSDescription, RuntimeInformation.OSArchitecture, RuntimeInformation.ProcessArchitecture, Environment.ProcessorCount, Environment.SystemPageSize };

        logger.LogError("systeminfo executed!");
        await context.Response.WriteAsJsonAsync(systemInfo);
    });
app.MapPaymentApi();
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
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    using var scope = app.Services.CreateScope();
    var dataSource = scope.ServiceProvider.GetRequiredService<EndpointDataSource>();
    var kestrelServer = scope.ServiceProvider.GetRequiredService<IServer>();
    string? baseUrl = kestrelServer.Features.Get<IServerAddressesFeature>()?.Addresses.First();
    logger.LogInformation("Open API: {Route}/api", baseUrl);
    foreach (var endpoint in dataSource.Endpoints)
    {
        if (endpoint is not RouteEndpoint routeEndpoint)
        {
            continue;
        }
        logger.LogInformation("{Route}/{RawText} : {DisplayName}",
            baseUrl,
            routeEndpoint.RoutePattern.RawText,
            routeEndpoint.DisplayName);
    }
});
app.Run();
