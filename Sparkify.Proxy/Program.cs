using Microsoft.AspNetCore.Hosting.Server;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig.ReadFrom.Configuration(context.Configuration);
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
var app = builder.Build();

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
}
else
{
    app.UseExceptionHandler("/Error");

}

/* adds the Strict-Transport-Security header to responses
    This informs the browser that the application must only be accessed with HTTPS
    and that any future attempts to access it using HTTP should
    automatically be converted to HTTPS */
//app.UseHsts();


/* enforces causes an automatic redirection to HTTPS URL
    when an HTTP URL is received in a way that forces a secure connection.
    This way, after the initial first HTTPS secure connection is established,
    the strict-security header (from UseHsts) prevents future redirections that
    might be used to perform man-in-the-middle attacks. */
//app.UseHttpsRedirection();

app.UseCors();
app.MapReverseProxy();
app.Run();
