using Microsoft.AspNetCore.Server.Kestrel.Core;
using Sparkify.Grpc.Services;

var builder = WebApplication.CreateBuilder(args);

/* Kestrel doesn't support HTTP/2 with TLS on macOS before .NET 8.
 * This configures Kestrel and the gRPC client to use HTTP/2 without TLS.
 */
builder.WebHost.ConfigureKestrel(options =>
{
    // Setup a HTTP/2 endpoint without TLS.
    options.ListenLocalhost(6002, o => o.Protocols =
        HttpProtocols.Http2);
});

// Add services to the container.
builder.Services.AddGrpc();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<HealthService>();
app.MapGet("/", () => "gRPC Server");

app.Run();