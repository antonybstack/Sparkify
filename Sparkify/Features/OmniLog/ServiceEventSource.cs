using System.Diagnostics.Tracing;

namespace Sparkify.Features.OmniLog;

[EventSource(Name = "Service")]
public class ServiceEventSource : EventSource
{
    public static readonly ServiceEventSource Log = new();

    [Event(1, Message = "{0}.{1}: {2}", Level = EventLevel.Informational)]
    public void OutputEvent(string className, string methodName, string message)
    {
        if (IsEnabled()) WriteEvent(1, className, methodName, message);
    }
}