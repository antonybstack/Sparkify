using Grpc.Core;

namespace Sparkify.Grpc.Services;

public class HealthService : Health.HealthBase
{
    private readonly ILogger<HealthService> _logger;

    public HealthService(ILogger<HealthService> logger)
    {
        _logger = logger;
    }

    public override Task<HealthResponse> Ping(HealthRequest request, ServerCallContext context)
    {
        return Task.FromResult(new HealthResponse
        {
            Message = "Healthy"
        });
    }
}