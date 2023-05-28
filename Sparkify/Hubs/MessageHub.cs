using Microsoft.AspNetCore.SignalR;

namespace Sparkify.Hubs;

public class MessageHub : Hub
{
    public async Task NewMessage(long username, string message)
    {
        await Clients.All.SendAsync("messageReceived", username, message);
    }
}