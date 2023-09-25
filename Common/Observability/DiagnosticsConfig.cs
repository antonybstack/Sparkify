using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Common.Observability;

// public static partial class OpenTelemetry
// {
//     public static class DiagnosticsConfig
//     {
//         public const string ServiceName = "Sparkify";
//         public static readonly ActivitySource ActivitySource = new(ServiceName);
//         public static Meter Meter = new(ServiceName);
//         public static Counter<long> RequestCounter =
//             Meter.CreateCounter<long>("app.request_counter");
//     }
// }

public static partial class OpenTelemetry
{
    public static DiagnosticsConfig Config => _lazy.Value;
    private static readonly Lazy<DiagnosticsConfig> _lazy = new(static () => new DiagnosticsConfig());

    public sealed class DiagnosticsConfig
    {
        public DiagnosticsConfig()
        {
            ServiceName = Assembly.GetEntryAssembly()?.GetName().Name ??
                          throw new ArgumentNullException(nameof(Assembly.GetEntryAssembly));
            ActivitySource = new(ServiceName);
            Meter = new(ServiceName);
            RequestCounter = Meter.CreateCounter<long>("app.request_counter");
        }

        public string ServiceName { get; init; }
        public ActivitySource ActivitySource { get; init; }
        public Meter Meter { get; init; }
        public Counter<long> RequestCounter { get; init; }
    }
}
// {
//     // public const string ServiceName = "Sparkify";
//     // public static readonly ActivitySource ActivitySource = new(ServiceName);
//     // public static Meter Meter = new(ServiceName);
//     // public static Counter<long> RequestCounter =
//     //     Meter.CreateCounter<long>("app.request_counter");
// }
// {
//     public static readonly string ServiceName = "Sparkify";
//     public static readonly ActivitySource ActivitySource = new(ServiceName);
//     public static Meter Meter = new(ServiceName);
//     public static Counter<long> RequestCounter =
//         Meter.CreateCounter<long>("app.request_counter");
// }
