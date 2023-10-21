using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Common.Configuration;
using Common.Observability;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using Sparkify.Features.Payment;
using Sparkify.Observability;

WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);
ServicePointManager.DefaultConnectionLimit = 10000;

var appOptions = builder.AddConfigAndValidate<ApiOptions, ValidateApiOptions>();
var databaseOptions = builder.AddConfigAndValidate<DatabaseOptions, ValidateDatabaseOptions>();
var otlpOptions = builder.AddConfigAndValidate<OtlpOptions, ValidateOtlpOptions>();

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
builder.Services.AddHttpsRedirection(options => options.HttpsPort = appOptions.Port);

builder.Services.AddCors(c => c.AddDefaultPolicy(policy => policy.AllowAnyMethod().AllowAnyHeader().AllowAnyOrigin()));
builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddControllers(options =>
{
    options.RespectBrowserAcceptHeader = true;
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
builder.Services.TryAddSingleton(DbManager.Store);
builder.Services.TryAddSingleton<IEventChannel, EventChannel>();
// builder.Services.AddSignalR();
builder.Services.AddHostedService<SubscriptionWorker>();

WebApplication app = builder.Build();

DbManager.CreateStore(databaseOptions.Name, databaseOptions.Http, databaseOptions.TcpHostName, databaseOptions.TcpPort);
app.UseHttpsRedirection();
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
    ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();
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
