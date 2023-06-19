using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.EntityFrameworkCore;
using Serilog;

// configure use web root
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig.ReadFrom.Configuration(context.Configuration);
});

// enables displaying database-related exceptions:
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDbContext<Models>(opt => opt.UseInMemoryDatabase("Messages"));

var app = builder.Build();

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
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var isDevelopment = app.Environment.IsDevelopment();
var server = app.Services.GetRequiredService<IServer>();
logger.LogInformation("Application Name: {ApplicationName}", builder.Environment.ApplicationName);
logger.LogInformation("Environment Name: {EnvironmentName}", builder.Environment.EnvironmentName);
logger.LogInformation("ContentRoot Path: {ContentRootPath}", builder.Environment.ContentRootPath);
logger.LogInformation("WebRootPath: {WebRootPath}", builder.Environment.WebRootPath);
logger.LogInformation("IsDevelopment: {IsDevelopment}", isDevelopment);
logger.LogInformation("Web server: {WebServer}", server.GetType().Name); // Will log "Web server: KestrelServer" if Kestrel is being used

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


app.MapGet("/systeminfo", async (HttpContext context) =>
{
    var systemInfo = new
    {
        RuntimeInformation.OSDescription,
        RuntimeInformation.OSArchitecture,
        RuntimeInformation.ProcessArchitecture,
        Environment.ProcessorCount,
        Environment.SystemPageSize
    };

    logger.LogInformation("System Info: {SystemInfo}", systemInfo);

    await context.Response.WriteAsJsonAsync(systemInfo);
});

app.MapGroup("/messages").MapMessagesApi();
app.Map("/Error",
    async context =>
    {
        await context.Response.WriteAsync(
            "An error occurred. The server encountered an error and could not complete your request.");
    });

app.MapFallback(async context => { await context.Response.WriteAsync("Page not found"); });


// app.UseMiddleware<RequestMiddleware>();
// // register middleware to the server's request pipeline
// app.Use(async (context, next) =>
// {
//     var option = context.Request.Query["option"];
//     if (!string.IsNullOrWhiteSpace(option))
//     {
//         context.Items["option"] = "override";
//     }
//     await next(context);
//     // do work that doesn't write to the Response.
// });

// app.UseRateLimiter();
// app.UseRequestLocalization();
// app.UseCors();
// app.UseAuthentication();
// app.UseAuthorization();
// app.UseSession();
// app.UseResponseCompression();
// app.UseResponseCaching();


app.Run();