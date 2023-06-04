using System.Diagnostics;
using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using Sparkify;
using Sparkify.Features.Message;
using Sparkify.Hubs;
using Sparkify.Features.OmniLog;

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

// adds the database context to the dependency injection container
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
var app = builder.Build();

if (isDevelopment)
    app.UseDeveloperExceptionPage();

/* The preceding code allows the server to locate and serve the index.html file.
 * The file is served whether the user enters its full URL or the root URL of the web app. */
app.UseDefaultFiles(); // Enables default file mapping on the current path
app.UseStaticFiles(); // Enables static file serving for the current request path

app.MapGroup("/messages").MapMessagesApi();
app.MapHub<MessageHub>("/hub");

// Instantiates a gRPC channel containing the connection information of the gRPC service.
using var channel = GrpcChannel.ForAddress("http://localhost:6002");
var client = new Health.HealthClient(channel);
app.UseMiddleware<RequestMiddleware>();
// register middleware to the server's request pipeline
app.Use(async (context, next) =>
{
    var option = context.Request.Query["option"];
    if (!string.IsNullOrWhiteSpace(option))
    {
        context.Items["option"] = "override";
    }
    await next(context);
    // do work that doesn't write to the Response.
});

// Console client running concurrently to provide that acts as a gRPC client
Task.Run(async () =>
{
    while (true)
    {
        var reply = await client.PingAsync(new HealthRequest { Name = "GrpcClient" });
        Console.WriteLine("gRPC Server Status: " + reply.Message);
        Console.WriteLine("Press any key to ping...");
        Console.ReadKey();
    }
});

app.Run();