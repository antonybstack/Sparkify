using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using EventStore.Client;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Sparkify.Features.Message;

// configure use web root
WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseQuic();

builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig.ReadFrom.Configuration(context.Configuration);
});

// enables displaying database-related exceptions:
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

/* DEPENDENCY INJECTION (SERVICES) SECTION */
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDbContext<Models>(opt => opt.UseInMemoryDatabase("Messages"));

var settings = EventStoreClientSettings
    .Create("esdb://localhost:2113?tls=false");
var client = new EventStoreClient(settings);

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

const string htmlContent = "<html><head><link rel=\"icon\" href=\"data:,\"></head><body style=\"background: rgb(43, 42, 51); color: #cacaca;\">Hello Sparkify!</body></html>";
app.MapGet("/", (HttpContext context) =>
{
    context.Response.ContentType = "text/html";
    return htmlContent;
});

var systemInfo = new
{
    RuntimeInformation.OSDescription,
    RuntimeInformation.OSArchitecture,
    RuntimeInformation.ProcessArchitecture,
    Environment.ProcessorCount,
    Environment.SystemPageSize
};

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

    await context.Response.WriteAsJsonAsync(systemInfo);
});

app.MapGet("/addtestevent", async (HttpContext context, CancellationToken cancellationToken) =>
{
    var eventData = new EventData(
        Uuid.NewUuid(),
        "test-event",
        JsonSerializer.SerializeToUtf8Bytes(systemInfo)
    );

    await client.AppendToStreamAsync(
        "concurrency-stream",
        StreamState.Any,
        new[] { eventData },
        cancellationToken: cancellationToken
    );

    var events = client.ReadAllAsync(Direction.Forwards,
        Position.Start,
        resolveLinkTos: true,
        cancellationToken: cancellationToken);

    await foreach (var e in events)
    {
        if (e.Event.EventType.StartsWith("$"))
            continue;
        Console.WriteLine(Encoding.UTF8.GetString(e.Event.Data.ToArray()));
    }

    var clientOneRead = client.ReadStreamAsync(Direction.Forwards,
        "concurrency-stream",
        StreamPosition.Start,
        cancellationToken: cancellationToken);
    var clientOneRevision = (await clientOneRead.LastAsync(cancellationToken: cancellationToken)).Event.EventNumber.ToUInt64();

    EventStoreClient.ReadStreamResult clientTwoRead = client.ReadStreamAsync(Direction.Forwards,
        "concurrency-stream",
        StreamPosition.Start,
        cancellationToken: cancellationToken);
    var clientTwoRevision = (await clientTwoRead.LastAsync(cancellationToken: cancellationToken)).Event.EventNumber.ToUInt64();

    var clientOneData = new EventData(
        Uuid.NewUuid(),
        "some-event",
        Encoding.UTF8.GetBytes("{\"id\": \"1\" \"value\": \"clientOne\"}")
    );

    var test = await client.AppendToStreamAsync("no-stream-stream", clientOneRevision, new List<EventData> {
        clientOneData
        },
        configureOperationOptions: options => options.ThrowOnAppendFailure = false,
        cancellationToken: cancellationToken);

    var clientTwoData = new EventData(
        Uuid.NewUuid(),
        "some-event",
        Encoding.UTF8.GetBytes("{\"id\": \"2\" \"value\": \"clientTwo\"}")
    );

    await client.AppendToStreamAsync("no-stream-stream", clientTwoRevision, new List<EventData> {
        clientTwoData,
        },
        configureOperationOptions: options => options.ThrowOnAppendFailure = false,
        cancellationToken: cancellationToken);

    var result = client.ReadStreamAsync(
    Direction.Forwards,
    "some-stream",
    revision: 10,
    maxCount: 20);

    await foreach (var e in result)
        Console.WriteLine(Encoding.UTF8.GetString(e.Event.Data.ToArray()));

    await context.Response.WriteAsJsonAsync(systemInfo, cancellationToken: cancellationToken);
});

app.MapGet("/read", async (HttpContext context, CancellationToken cancellationToken) =>
{
    EventStoreClient.ReadStreamResult result = client.ReadStreamAsync(
        Direction.Forwards,
        "concurrency-stream",
        StreamPosition.Start,
        cancellationToken: cancellationToken);

    List<ResolvedEvent> events = await result.ToListAsync(cancellationToken);
    // foreach event source db event, deserialize the data and add to a list
    var list = new List<object>();
    foreach (ResolvedEvent @event in events)
    {
        var data = JsonSerializer.Deserialize<object>(@event.Event.Data.Span);
        list.Add(data);
    }
    await context.Response.WriteAsJsonAsync(list, cancellationToken: cancellationToken);
});

app.MapGroup("/messages").MapMessagesApi();

app.Map("/Error", async context =>
    await context.Response.WriteAsync(
        "An error occurred. The server encountered an error and could not complete your request.")
);

app.MapFallback(async context => { await context.Response.WriteAsync("Page not found"); });

var input = new ConsoleInput();
input.KeyPressed += HandleKeyPress;

app.Run();

static void HandleKeyPress(char key)
{
    Console.WriteLine($"You pressed {key}");
}

public class ConsoleInput
{
    // Event triggered when a key is pressed
    public event Action<char> KeyPressed;

    public ConsoleInput()
    {
        new Thread(() =>
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                KeyPressed?.Invoke(key.KeyChar);
            }
        })
        { IsBackground = true }.Start();
    }
}
