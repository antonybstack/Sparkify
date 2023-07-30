namespace Sparkify.Features.Payment;

// public class FireAndForgetService
// // : FireAndForget.FireAndForgetService.FireAndForgetServiceBase
// {
//     public override async Task FireAndForgetMethod(string request, IServerStreamWriter<string> responseStream, ServerCallContext context)
//     {
//         // Handle the request, but don't block.
//         _ = Task.Run(() => HandleRequest(request, responseStream, context));
//     }

//     private async Task HandleRequest(string request, IServerStreamWriter<object> responseStream, ServerCallContext context)
//     {
//         // Implement your request handling logic here.

//         // You can write responses to the stream as often as you want.
//         // The client will receive these responses in real time.
//         await responseStream.WriteAsync(new { Response = "Your response here" });

//         // Later on, when there's a subsequent change:
//         await responseStream.WriteAsync(new { Response = "Another response here" });
//     }
// }
