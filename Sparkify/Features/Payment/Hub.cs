using Microsoft.AspNetCore.SignalR;

namespace Sparkify.Features.Payment;

public interface IPaymentClient
{
    Task SendMessage(string username, string message);
}

public class PaymentHub : Hub<IPaymentClient>
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "SignalR Users");
        await Clients.Group("SignalR Users").SendMessage(Context.ConnectionId, " has joined the channel.");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "SignalR Users");
        await Clients.Group("SignalR Users").SendMessage(Context.ConnectionId, " has left the channel.");
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(string message) => await Clients.All.SendMessage(Context.ConnectionId, message);

    public async Task SendMessageToCaller(string message) =>
        await Clients.Caller.SendMessage(Context.ConnectionId, message);

    public async Task SendMessageToGroup(string message) =>
        await Clients.Group("SignalR Users").SendMessage(Context.ConnectionId, message);
}
