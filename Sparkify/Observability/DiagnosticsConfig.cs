using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Sparkify.Observability;

public static partial class OpenTelemetry
{
    public static class DiagnosticsConfig
    {
        public const string ServiceName = "Sparkify";
        public static readonly ActivitySource ActivitySource = new(ServiceName);
        public static Meter Meter = new(ServiceName);
        public static Counter<long> RequestCounter =
            Meter.CreateCounter<long>("app.request_counter");
    }
}
