using System.Collections.Generic;
using System.Linq;
using Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

namespace Common.Observability;

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
    public static void RegisterSerilog(this IHostApplicationBuilder builder, OtlpOptions otlpOptions)
    {
        builder.Logging.ClearProviders();

        // if type WebApplicationBuilder
        if (builder is WebApplicationBuilder webApplicationBuilder)
        {
            webApplicationBuilder.Host.UseSerilog((context, loggerConfig) =>
            {
                loggerConfig.ReadFrom.Configuration(context.Configuration);

                if (builder.Environment.IsDevelopment())
                {
                    return;
                }

                loggerConfig.WriteTo.OpenTelemetry(opts =>
                {
                    opts.Endpoint = otlpOptions.SinkEndpoint;
                    opts.RestrictedToMinimumLevel = LogEventLevel.Information;
                    opts.ResourceAttributes = new Dictionary<string, object>
                    {
                        {
                            "service.name", OpenTelemetry.Config.ServiceName
                        },
                        {
                            "deployment.environment", context.HostingEnvironment.EnvironmentName
                        }
                    };
                });
            });
        }
        else
        {
            builder.Services.AddSerilog(loggerConfig =>
            {
                loggerConfig.ReadFrom.Configuration(builder.Configuration);
                loggerConfig.WriteTo.OpenTelemetry(opts =>
                {
                    opts.Endpoint = otlpOptions.SinkEndpoint;
                    opts.RestrictedToMinimumLevel = LogEventLevel.Information;
                    opts.ResourceAttributes = new Dictionary<string, object>
                    {
                        {
                            "service.name", OpenTelemetry.Config.ServiceName
                        },
                        {
                            "deployment.environment", builder.Environment.EnvironmentName
                        }
                    };
                });
            });
        }
    }

    /// <summary>
    /// Registers Serilog with http request logging with additional properties and a custom message template.
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static IApplicationBuilder RegisterSerilogRequestLogging(this WebApplication app) =>
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "{RequestMethod} {Protocol} {RequestPath} responded {StatusCode} {ContentType} in {Elapsed:0.00} ms from {TraceIdentifier} {RemoteIpAddress}:{RemotePort}";
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
                var headers = httpContext.Request.Headers.ToDictionary(
                    static header => header.Key,
                    static header => header.Value.ToString());
                foreach (var (key, value) in headers)
                {
                    diagnosticContext.Set($"Headers:{key}", value);
                }
            };

        });
}
