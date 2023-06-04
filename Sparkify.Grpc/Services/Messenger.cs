using Grpc.Core;


namespace Sparkify.Grpc.Services;

public class MessengerService : Messenger.MessengerBase
{
    private readonly ILogger<MessengerService> _logger;
    public MessengerService(ILogger<MessengerService> logger)
    {
        _logger = logger;
    }

    public override Task<MessageResponse> Send(MessageRequest request, ServerCallContext context)
    {
        return Task.FromResult(new MessageResponse
        {
            Message = $"Hello {request.Name}!"
        });
    }
}