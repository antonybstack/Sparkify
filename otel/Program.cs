using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.HttpLogging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sparkify;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

ResourceBuilder resource = ResourceBuilder.CreateDefault()
    .AddService(
        serviceNamespace: "Sparkify",
        serviceName: builder.Environment.ApplicationName,
        serviceVersion: Assembly.GetEntryAssembly()?.GetName().Version?.ToString(),
        serviceInstanceId: Environment.MachineName
    )
    .AddAttributes(new Dictionary<string, object> { { "deployment.environment", builder.Environment.EnvironmentName } });

builder.Services
    .AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
        tracerProviderBuilder
            .AddSource(DiagnosticsConfig.ActivitySource.Name)
            // .ConfigureResource(resource => resource.AddService(DiagnosticsConfig.ServiceName))
            // recommended resource attributes: https://grafana.com/docs/opentelemetry/instrumentation/configuration/resource-attributes/
            .SetResourceBuilder(resource)
            .AddAspNetCoreInstrumentation(options =>
            {
                options.EnableGrpcAspNetCoreSupport = true;
                options.RecordException = true;
                options.EnrichWithException = (activity, exception) =>
                {
                    activity?.SetTag("exception.type", exception.GetType().Name);
                    activity?.SetTag("exception.message", exception.Message);
                    activity?.SetTag("exception.stacktrace", exception.StackTrace);
                };
                options.EnrichWithHttpRequest = (activity, request) =>
                {
                    activity?.SetTag("http.request.host", request?.Host);
                    activity?.SetTag("http.request.path", request?.Path);
                    activity?.SetTag("http.request.query", request?.QueryString);
                    activity?.SetTag("http.request.content_length", request?.ContentLength);
                    activity?.SetTag("http.request.content_type", request?.ContentType);
                };
                options.EnrichWithHttpResponse = (activity, response) =>
                {
                    activity?.SetTag("http.response.status_code", response?.StatusCode);
                    activity?.SetTag("http.response.content_length", response?.ContentLength);
                    activity?.SetTag("http.response.content_type", response?.ContentType);
                };
            })
            // .AddEventCounterMetrics()
            // .AddOtlpExporter() // http://localhost:4317 by gRPC protocol
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://192.168.1.200:4317");
            })
            .SetSampler(new AlwaysOnSampler()))
    // .AddConsoleExporter())
    .WithMetrics(metricsProviderBuilder =>
            metricsProviderBuilder
                .ConfigureResource(resource => resource.AddService(DiagnosticsConfig.ServiceName))
                .AddMeter(DiagnosticsConfig.Meter.Name)
                .AddAspNetCoreInstrumentation()
                // options =>
                // {
                //     options.Enrich = (name, context,  _) =>
                //     {
                //
                //     };
                //
                // })
                .AddProcessInstrumentation()
                .AddRuntimeInstrumentation()
                // Metrics provides by ASP.NET Core in .NET 8
                .AddMeter("Microsoft.AspNetCore.Hosting")
                .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                .AddOtlpExporter((options, o) =>
                {
                    options.Endpoint = new Uri("http://192.168.1.200:4317");
                    o.PeriodicExportingMetricReaderOptions = new PeriodicExportingMetricReaderOptions
                    {
                        ExportIntervalMilliseconds = 5000, ExportTimeoutMilliseconds = 5000
                    };
                })
        // .AddConsoleExporter()
    );
// (options, readerOptions) =>
// readerOptions.PeriodicExportingMetricReaderOptions = new()
// {
//     ExportIntervalMilliseconds = 1000,
//     ExportTimeoutMilliseconds = 1000,
// }));

// https://www.twilio.com/blog/build-a-logs-pipeline-in-dotnet-with-opentelemetry
builder.Logging
    .ClearProviders()
    .SetMinimumLevel(LogLevel.Information)
    // set microsft ones to error
    .AddFilter("Microsoft", LogLevel.Error)
    .AddOpenTelemetry(loggerOptions =>
    {
        loggerOptions
            .SetResourceBuilder(resource)
            .AddConsoleExporter();
        loggerOptions.IncludeFormattedMessage = true;
        loggerOptions.IncludeScopes = true;
        loggerOptions.ParseStateValues = true;
        loggerOptions.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://192.168.1.200:4317");
        });
    });

builder.Services.AddHttpLogging(o => o.LoggingFields = HttpLoggingFields.All);


WebApplication app = builder.Build();

app.MapGet("/", (ILogger<Program> log) =>
{
    // Manual Instrumentation
    using Activity? activity = DiagnosticsConfig.ActivitySource.StartActivity("RootActivity", ActivityKind.Client);
    activity?.SetTag("foo", 1);
    activity?.SetTag("bar", "Hello, World!");
    activity?.SetTag("baz", new[] { 1, 2, 3 });

    // log.LogInformation("Hello Info");
    // log.LogError("Hello Error");

    throw new Exception("Hello Exception");

    DiagnosticsConfig.RequestCounter.Add(1, new KeyValuePair<string, object?>("foo", 1), new KeyValuePair<string, object?>("bar", "Hello, World!"),
        new KeyValuePair<string, object?>("baz", new[] { 1, 2, 3 }));

    return "Hello World!";
});

app.Run();
