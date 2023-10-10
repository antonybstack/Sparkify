using Serilog;
using Serilog.Events;

namespace Sparkify.Observability;

public static class SerilogRegistration
{
    /// <summary>
    /// Registers Serilog with configurations from appsettings.json
    /// </summary>
    /// <remarks>
    /// Serilog does not write to OpenTelemetry when running in Development.
    /// </remarks>
    /// <returns>
    /// <see cref="IHostBuilder"/> with Serilog configured.
    /// </returns>
    public static IHostBuilder RegisterSerilog(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();

        return builder.Host.UseSerilog((context, loggerConfig) =>
        {
            loggerConfig.ReadFrom.Configuration(context.Configuration);

            if (builder.Environment.IsDevelopment())
            {
                return;
            }

            loggerConfig.WriteTo.OpenTelemetry(opts =>
            {
                opts.Endpoint = builder.Configuration.GetValue<string>("Urls:Otlp");
                opts.RestrictedToMinimumLevel = LogEventLevel.Information;
                opts.ResourceAttributes = new Dictionary<string, object>
                {
                    { "service.name", OpenTelemetry.DiagnosticsConfig.ServiceName },
                    { "deployment.environment", context.HostingEnvironment.EnvironmentName }
                };
            });
        });
    }


    /// <summary>
    /// Registers Serilog with http request logging with additional properties and a custom message template.
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static IApplicationBuilder RegisterSerilogRequestLogging(this WebApplication app) =>
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "{RequestMethod} {Protocol} {RequestPath} responded {StatusCode} {ContentType} in {Elapsed:0.00} ms from {TraceIdentifier} {RemoteIpAddress}:{RemotePort}";
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("Host", httpContext.Request.Host.Value);
                diagnosticContext.Set("Protocol", httpContext.Request.Protocol);
                diagnosticContext.Set("Scheme", httpContext.Request.Scheme);
                diagnosticContext.Set("QueryString", httpContext.Request.QueryString.Value);
                diagnosticContext.Set("ContentType", httpContext.Request.ContentType);
                diagnosticContext.Set("ContentLength", httpContext.Request.ContentLength);
                diagnosticContext.Set("TraceIdentifier", httpContext.TraceIdentifier);
                diagnosticContext.Set("RemoteIpAddress", httpContext.Connection.RemoteIpAddress);
                diagnosticContext.Set("RemotePort", httpContext.Connection.RemotePort);
                diagnosticContext.Set("LocalIpAddress", httpContext.Connection.LocalIpAddress);
                diagnosticContext.Set("LocalPort", httpContext.Connection.LocalPort);
            };
        });
}
