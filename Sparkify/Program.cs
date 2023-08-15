using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Data;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using Sparkify.Features.Payment;

WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);
ServicePointManager.DefaultConnectionLimit = 10000;

builder.WebHost
.UseQuic()
.UseKestrel(options =>
{
    options.ListenLocalhost(6002, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
        listenOptions.UseHttps();
        // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/connection-middleware?view=aspnetcore-8.0
        // listenOptions.UseConnectionLogging();
    });
});

builder.Services.AddHttpsRedirection(options => options.HttpsPort = 6002);

builder.Services.AddCors(c => c.AddDefaultPolicy(policy => policy.AllowAnyMethod().AllowAnyHeader().AllowAnyOrigin()));
builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.UseInlineDefinitionsForEnums());
builder.Host.UseSerilog((context, loggerConfig) => { loggerConfig.ReadFrom.Configuration(context.Configuration); });
/* DEPENDENCY INJECTION (SERVICES) SECTION */
builder.Services.TryAddSingleton(DbManager.Store);
builder.Services.TryAddSingleton<IEventChannel, EventChannel>();
// builder.Services.AddSignalR();
builder.Services.AddHostedService<SubscriptionWorker>();

WebApplication app = builder.Build();

app.UseHttpsRedirection();

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "{RequestMethod} {Protocol} {RequestPath} responded {StatusCode} {ContentType} in {Elapsed:0.00} ms from {TraceIdentifier} {RemoteIpAddress}:{RemotePort}";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("Host", httpContext.Request.Host.Value);
        diagnosticContext.Set("Protocol", httpContext.Request.Protocol);
        diagnosticContext.Set("Scheme", httpContext.Request.Scheme);
        diagnosticContext.Set("QueryString", httpContext.Request.QueryString.Value);
        diagnosticContext.Set("ContentType", httpContext.Request.ContentType);
        diagnosticContext.Set("ContentLength", httpContext.Request.ContentLength);
        diagnosticContext.Set("TraceIdentifier", httpContext.TraceIdentifier);
        diagnosticContext.Set("RemoteIpAddress", httpContext.Connection.RemoteIpAddress);
        diagnosticContext.Set("RemotePort", httpContext.Connection.RemotePort);
        diagnosticContext.Set("LocalIpAddress", httpContext.Connection.LocalIpAddress);
        diagnosticContext.Set("LocalPort", httpContext.Connection.LocalPort);
    };
});

// Log the application startup information
ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();
var isDevelopment = app.Environment.IsDevelopment();
IServer server = app.Services.GetRequiredService<IServer>();
logger.LogInformation("Application Name: {ApplicationName}", builder.Environment.ApplicationName);
logger.LogInformation("Environment Name: {EnvironmentName}", builder.Environment.EnvironmentName);
logger.LogInformation("ContentRoot Path: {ContentRootPath}", builder.Environment.ContentRootPath);
logger.LogInformation("WebRootPath: {WebRootPath}", builder.Environment.WebRootPath);
logger.LogInformation("IsDevelopment: {IsDevelopment}", isDevelopment);
logger.LogInformation("Web server: {WebServer}", server.GetType().Name);

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
        <title>Sparkify</title>
        <link rel=\"icon\" href=\"data:,\">
    </head>
    <body>
        <h1>Sparkify</h1>
        <body style=\"background: rgb(43, 42, 51); color: #333;\">Hello Sparkify!</body>
    </body>
</html>
""";
app.MapGet("", (HttpContext context) =>
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
app.MapGet("api/systeminfo", async context =>
{
    var systemInfo = new
    {
        RuntimeInformation.OSDescription,
        RuntimeInformation.OSArchitecture,
        RuntimeInformation.ProcessArchitecture,
        Environment.ProcessorCount,
        Environment.SystemPageSize
    };

    await context.Response.WriteAsJsonAsync(systemInfo);
});
app.MapPaymentApi();
app.Map("error", async context =>
    await context.Response.WriteAsync(
        "An error occurred. The server encountered an error and could not complete your request.")
);
app.MapFallback(async context => { await context.Response.WriteAsync("Page not found"); });
// log all endpoints
app.Lifetime.ApplicationStarted.Register(() =>
{
    using IServiceScope scope = app.Services.CreateScope();
    EndpointDataSource dataSource = scope.ServiceProvider.GetRequiredService<EndpointDataSource>();
    IServer kestrelServer = scope.ServiceProvider.GetRequiredService<IServer>();
    var baseUrl = kestrelServer.Features.Get<IServerAddressesFeature>()?.Addresses.First();
    logger.LogInformation("Open API: {Route}/api", baseUrl);
    foreach (Endpoint endpoint in dataSource.Endpoints)
    {
        if (endpoint is not RouteEndpoint routeEndpoint) continue;
        logger.LogInformation("{Route}/{RawText} : {DisplayName}", baseUrl, routeEndpoint.RoutePattern.RawText, routeEndpoint.DisplayName);
    }
});
app.UseCors();
app.Run();
