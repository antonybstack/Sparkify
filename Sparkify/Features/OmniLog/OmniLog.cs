using System.Reflection;

namespace Sparkify.Features.OmniLog;

public interface IOmniLog
{
    void Output(string message);
}

public class OmniLog : IOmniLog
{
    private readonly ILogger<OmniLog> _logger;

    public OmniLog(ILogger<OmniLog> logger)
    {
        _logger = logger;
    }

    public void Output(string message)
    {
        var className = GetType().Name;
        var methodName = MethodBase.GetCurrentMethod()?.Name ?? string.Empty;
        // Console.WriteLine($"{className}.{methodName}: '{message}'");
        // Debug.WriteLine($"{className}.{methodName}: '{message}'");
        // Trace.WriteLine($"{className}.{methodName}: '{message}'");
        // Debugger.Log(level: 0, category: "Service", message: $"{className}.{methodName}: '{message}'");
        // ServiceEventSource.Log.OutputEvent(className, methodName, message);
        _logger.LogInformation("{ClassName}.{MethodName}: '{Message}'", className, methodName, message);
    }
}