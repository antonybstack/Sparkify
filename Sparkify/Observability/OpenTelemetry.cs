using System.Reflection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Sparkify.Observability;

public static partial class OpenTelemetry
{
    /// <summary>
    /// Registers OpenTelemetry with tracing and metrics.
    /// </summary>
    /// <remarks>
    /// OpenTelemetry is not registered when running in Development.
    /// </remarks>
    public static WebApplicationBuilder RegisterOpenTelemetry(this WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
        {
            return builder;
        }

        ResourceBuilder resource = ResourceBuilder.CreateDefault()
            .AddService(
                serviceNamespace: "Sparkify",
                serviceName: builder.Environment.ApplicationName,
                // serviceVersion: Assembly.GetEntryAssembly()?.GetName().Version?.ToString(),
                serviceVersion: "0.0.1",
                serviceInstanceId: Environment.MachineName
            )
            .AddAttributes(new Dictionary<string, object>
            {
                { "deployment.environment", builder.Environment.EnvironmentName }
            });

        builder.Services
            .AddOpenTelemetry()
            .AddTracing(builder.Configuration, resource)
            .AddMetrics(builder.Configuration);

        return builder;
    }

    private static OpenTelemetryBuilder AddTracing(this OpenTelemetryBuilder builder,
        IConfiguration configuration,
        ResourceBuilder resource) =>
        builder.WithTracing(tracerProviderBuilder =>
            tracerProviderBuilder
                .AddSource(DiagnosticsConfig.ActivitySource.Name)
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
                .AddOtlpExporter(options => { options.Endpoint = new Uri(configuration["Urls:Otlp"]); })
                .SetSampler(new AlwaysOnSampler()));

    private static OpenTelemetryBuilder AddMetrics(this OpenTelemetryBuilder builder,
        IConfiguration configuration) =>
        builder.WithMetrics(metricsProviderBuilder =>
            metricsProviderBuilder
                .ConfigureResource(resource => resource.AddService(DiagnosticsConfig.ServiceName))
                .AddMeter(DiagnosticsConfig.Meter.Name)
                .AddAspNetCoreInstrumentation()
                .AddProcessInstrumentation()
                .AddRuntimeInstrumentation()
                .AddHttpClientInstrumentation()
                // Metrics provides by ASP.NET Core in .NET 8
                .AddMeter("Microsoft.AspNetCore.Hosting")
                .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                .AddOtlpExporter((options, o) =>
                {
                    options.Endpoint = new Uri(configuration["Urls:Otlp"]);
                    o.PeriodicExportingMetricReaderOptions = new PeriodicExportingMetricReaderOptions
                    {
                        ExportIntervalMilliseconds = 5000,
                        ExportTimeoutMilliseconds = 5000
                    };
                })
        );
}
