using System.Runtime.InteropServices;
using Data;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using Sparkify.Features.Message;
using Sparkify.Features.Payment;

// configure use web root
WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseQuic();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.UseInlineDefinitionsForEnums());

builder.Host.UseSerilog((context, loggerConfig) => { loggerConfig.ReadFrom.Configuration(context.Configuration); });

// enables displaying database-related exceptions:
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

/* DEPENDENCY INJECTION (SERVICES) SECTION */
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDbContext<Models>(opt => opt.UseInMemoryDatabase("Messages"));

builder.Services.TryAddSingleton(DbManager.Store);

builder.Services.AddSignalR();


WebApplication app = builder.Build();

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("Host", httpContext.Request.Host.Value);
        diagnosticContext.Set("Protocol", httpContext.Request.Protocol);
        diagnosticContext.Set("Scheme", httpContext.Request.Scheme);
        diagnosticContext.Set("QueryString", httpContext.Request.QueryString.Value);
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
logger.LogInformation("Web server: {WebServer}",
    server.GetType().Name); // Will log "Web server: KestrelServer" if Kestrel is being used

/* MIDDLEWARE SECTION */
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
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
        <body style=\"background: rgb(43, 42, 51); color: #cacaca;\">Hello Sparkify!</body>
    </body>
</html>
""";
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

app.Map("/Error", async context =>
    await context.Response.WriteAsync(
        "An error occurred. The server encountered an error and could not complete your request.")
);

app.MapFallback(async context => { await context.Response.WriteAsync("Page not found"); });

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

app.Run();
