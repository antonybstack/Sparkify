using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Sparkify.Features.Message; 
using Sparkify.Features.OmniLog;

// configure use web root
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "Client/wwwroot"
});

builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig.ReadFrom.Configuration(context.Configuration);
});

// enables displaying database-related exceptions:
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

/* DEPENDENCY INJECTION (SERVICES) SECTION
 * The preceding code adds the MVC services to the dependency injection container */

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDbContext<Models>(opt => opt.UseInMemoryDatabase("Messages"));
builder.Services.AddSignalR();

/*
 * AddSingleton is called twice with IOmniLog as the service type.
 * The second call to AddSingleton overrides the previous one when a class constructor resolves the injection as IOmniLog
 * The second call adds to the previous one when multiple services are resolved via IEnumerable<IOmniLog>.
 * Services appear in the order they were registered when resolved via IEnumerable<IOmniLog>.
 */
builder.Services.AddSingleton<IOmniLog, OmniLog>();
builder.Services.AddSingleton<IOmniLog, OmniLog>();

// builder.Services.AddTransient<RequestMiddleware>();

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
    /* adds the Strict-Transport-Security header to responses
     This informs the browser that the application must only be accessed with HTTPS
     and that any future attempts to access it using HTTP should
     automatically be converted to HTTPS */
    // app.UseHsts();
}

/* enforces causes an automatic redirection to HTTPS URL
 when an HTTP URL is received in a way that forces a secure connection.
 This way, after the initial first HTTPS secure connection is established,
 the strict-security header (from UseHsts) prevents future redirections that
 might be used to perform man-in-the-middle attacks.*/
// app.UseHttpsRedirection();
/* The preceding code allows the server to locate and serve the index.html file.
 * The file is served whether the user enters its full URL or the root URL of the web app.
 * Middleware that enables the use of static files, default files, and directory browsing */
app.UseFileServer();
// app.UseCookiePolicy();
app.UseRouting();

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
app.MapHub<MessageHub>("/hub");
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