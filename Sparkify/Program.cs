using System.Diagnostics;
using System.Text.Json;
using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using Sparkify;
using Sparkify.Features.Message;
using Sparkify.Features.OmniLog;
using Health = Grpc.Health.V1.Health;

// configure use web root
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "Client/wwwroot"
});
// enables displaying database-related exceptions:
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
var isDevelopment = builder.Environment.IsDevelopment();
Debug.WriteLine($"ContentRoot Path: {builder.Environment.ContentRootPath}");
Debug.WriteLine($"WebRootPath: {builder.Environment.WebRootPath}");
Debug.WriteLine($"IsDevelopment: {isDevelopment}");

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

builder.Services.AddTransient<RequestMiddleware>();

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = false;
    options.TimestampFormat = "HH:mm:ss ";
    options.JsonWriterOptions = new JsonWriterOptions
    {
        Indented = true
    };
});


var app = builder.Build();


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
    app.UseHsts();
}

/* enforces causes an automatic redirection to HTTPS URL
 when an HTTP URL is received in a way that forces a secure connection.
 This way, after the initial first HTTPS secure connection is established,
 the strict-security header (from UseHsts) prevents future redirections that
 might be used to perform man-in-the-middle attacks.*/
app.UseHttpsRedirection();
/* The preceding code allows the server to locate and serve the index.html file.
 * The file is served whether the user enters its full URL or the root URL of the web app.
 * Middleware that enables the use of static files, default files, and directory browsing */
app.UseFileServer();
app.UseCookiePolicy();
app.UseRouting();

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

// app.UseCookiePolicy();
// app.UseRateLimiter();
// app.UseRequestLocalization();
// app.UseCors();
// app.UseAuthentication();
// app.UseAuthorization();
// app.UseSession();
// app.UseResponseCompression();
// app.UseResponseCaching();

// Console client running concurrently to provide that acts as a gRPC client
_ = Task.Run(async () =>
{
    try
    {
        await Task.Delay(1000);
        // Instantiates a gRPC channel containing the connection information of the gRPC service.
        using var channel = GrpcChannel.ForAddress("http://localhost:6002");
        var healthClient = new Health.HealthClient(channel);
        
        var status = (await healthClient.CheckAsync(new())).Status;
        Console.WriteLine("gRPC Server Status: " + status);
        
        var client = new Sparkify.Messenger.MessengerClient(channel);
        
        while (true)
        {
            Console.WriteLine("Press any key to ping...");
            Console.ReadKey();
            
            var reply =  await client.SendAsync(new MessageRequest
            {
                Name = "gRPC Client"
            });
            
            Console.WriteLine("gRPC Server Response: " + reply.Message);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
    }
});

app.Run();