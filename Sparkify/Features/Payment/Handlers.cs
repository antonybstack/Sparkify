using System.Diagnostics;
using System.Text;
using Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Raven.Client.Documents;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Session;

namespace Sparkify.Features.Payment;

public static class ApiEndpointRouteBuilderExtensions
{
    private static readonly byte[] s_lineBreak = "\n"u8.ToArray();

    /// <summary>
    ///     Add endpoints for payment related operations.
    /// </summary>
    /// <param name="endpoints">
    ///     The <see cref="IEndpointRouteBuilder" /> to add the endpoints to.
    /// </param>
    /// <returns>An <see cref="IEndpointConventionBuilder" /> to further customize the added endpoints.</returns>
    public static IEndpointConventionBuilder MapPaymentApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder routeGroup = endpoints.MapGroup("api/payment");

        routeGroup.MapGet("/", async Task<Results<Ok<List<PaymentEvent>>, NotFound, ValidationProblem>>
                ([FromQuery] string? id, [FromQuery] string? userId, [FromServices] IDocumentStore store) =>
            {
                try
                {
                    using IAsyncDocumentSession session = store.OpenAsyncSession();

                    IRavenQueryable<PaymentEvent> query = session
                        .Query<PaymentEvent>();

                    if (id is not null)
                    {
                        query = query.Where(x => x.Id == id);
                    }

                    if (userId is not null)
                    {
                        query = query.Where(x => x.ReferenceId == userId);
                    }

                    List<PaymentEvent>? data = await query.ToListAsync();

                    if (data.Count is 0)
                    {
                        return TypedResults.NotFound();
                    }

                    return TypedResults.Ok(data);
                }
                catch (Exception e)
                {
                    return TypedResults.ValidationProblem(
                        new Dictionary<string, string[]> { { "errors", new[] { e.Message } } });
                }
            })
            .Produces<List<PaymentEvent>>()
            .ProducesValidationProblem();

        routeGroup.MapPost("/", async Task<Results<Created<object>, ValidationProblem>>
                ([FromBody] PaymentEvent @event, [FromServices] IDocumentStore store) =>
            {
                try
                {
                    using IAsyncDocumentSession session = store.OpenAsyncSession();
                    await session.StoreAsync(@event);
                    await session.SaveChangesAsync();
                    return TypedResults.Created<object>(@event.Id, new { @event.Id });
                }
                catch (Exception e)
                {
                    return TypedResults.ValidationProblem(
                        new Dictionary<string, string[]> { { "errors", new[] { e.Message } } });
                }
            })
            .Produces<Created<object>>()
            .ProducesValidationProblem();

        routeGroup.MapGet("/sse", async (HttpContext context,
                IDocumentStore store,
                IEventChannel channel) =>
            {
                Debug.WriteLine($"ConnectionId: {context.Connection.Id}");
                Debug.WriteLine($"LocalIpAddress: {context.Connection.LocalIpAddress}");
                Debug.WriteLine($"LocalPort: {context.Connection.LocalPort}");
                Debug.WriteLine($"RemoteIpAddress: {context.Connection.RemoteIpAddress}");
                Debug.WriteLine($"RemotePort: {context.Connection.RemotePort}");
                Debug.WriteLine($"TraceIdentifier: {context.TraceIdentifier}");

                context.Response.Headers["cache-control"] = "no-cache";
                context.Response.Headers["content-type"] = "text/event-stream";
                context.Response.Headers["connection"] = "keep-alive";

                // send heartbeat event every 15 seconds to client to keep connection alive
                _ = Task.Run(async () =>
                {
                    while (!context.RequestAborted.IsCancellationRequested)
                    {
                        await context.Response.WriteAsync("data: heartbeat\n\n");
                        await context.Response.Body.FlushAsync();
                        Debug.WriteLine($"Heartbeat sent {DateTime.UtcNow}");
                        await Task.Delay(TimeSpan.FromSeconds(10), context.RequestAborted);
                    }
                }, context.RequestAborted);

                string uidBase64 = string.Empty;

                while (!context.RequestAborted.IsCancellationRequested)
                {
                    try
                    {
                        using IAsyncDocumentSession? session = store.OpenAsyncSession();
                        session.Advanced.MaxNumberOfRequestsPerSession = 10000;

                        var account = await  session
                            .Query<UsersWithBalance.IndexEntry, UsersWithBalance>()
                            .Customize(x => x.RandomOrdering())
                            .FirstOrDefaultAsync();

                        var accountJson = JsonSerializer.Serialize(account);
                        Debug.WriteLine(accountJson);
                        var sb = new StringBuilder();
                        sb.AppendLine("event: account");
                        sb.AppendLine($"data: {accountJson}\n");
                        await context.Response.WriteAsync(sb.ToString(), context.RequestAborted);
                        await context.Response.Body.FlushAsync();
                        sb.Clear();

                        uidBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{context.TraceIdentifier}|{account.Id}"));
                        channel.RegisterClient(uidBase64);

                        await foreach (PaymentEvent paymentEvent in channel.ReadAllAsync(uidBase64, context.RequestAborted))
                        {
                            Debug.WriteLine($"Sending event {paymentEvent.Id} to client");
                            var data = JsonSerializer.Serialize(paymentEvent);
                            sb.AppendLine($"data: {data}\n");

                            await context.Response.WriteAsync(sb.ToString(), context.RequestAborted);
                            await context.Response.Body.FlushAsync();
                            sb.Clear();

                            // wait for the indexing to complete on AccountsWithBalance index
                            AccountsWithBalance.IndexEntry? result = await session
                                .Query<AccountsWithBalance.IndexEntry, AccountsWithBalance>()
                                .Customize(x => x.WaitForNonStaleResults())
                                .FirstOrDefaultAsync(x => x.Id == account.Id);
                            // get additional information from the document index result
                            var accountWithBalance = JsonSerializer.Serialize(result);
                            Debug.WriteLine(accountWithBalance);
                            sb.AppendLine("event: account");
                            sb.AppendLine($"data: {accountWithBalance}\n");
                            await context.Response.WriteAsync(sb.ToString(), context.RequestAborted);
                            await context.Response.Body.FlushAsync();
                            sb.Clear();
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                        throw;
                    }
                    finally
                    {
                        channel.UnregisterClient(uidBase64);
                    }
                }
                Debug.WriteLine($"Connection {context.Connection.Id} completed");
            })
            .Produces<PaymentEvent>()
            .ProducesValidationProblem();


        routeGroup.MapGet("/health", async context =>
            {
                try
                {
                    if (context.Request.Protocol.StartsWith("HTTP/1"))
                    {
                        context.Response.Headers.Connection = "keep-alive";
                    }
                    context.Response.Headers.CacheControl = "no-cache";
                    context.Response.Headers.ContentType = "text/event-stream";

                    while (!context.RequestAborted.IsCancellationRequested)
                    {
                        await context.Response.WriteAsync("data: heartbeat\n");
                        await context.Response.Body.FlushAsync();
                        await Task.Delay(TimeSpan.FromMilliseconds(500));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    await context.Response.Body.DisposeAsync();
                }
            });

        routeGroup.MapGet("/streamwrites", async (HttpContext context, IDocumentStore store) =>
            {
                context.Response.Headers["cache-control"] = "no-cache";
                context.Response.Headers["content-type"] = "plain/text";

                await foreach (PaymentEvent paymentEvent in StreamEventsAsync(store))
                {
                    await JsonSerializer.SerializeAsync(context.Response.Body, paymentEvent);
                    await context.Response.Body.WriteAsync(s_lineBreak);
                }
            })
            .Produces<PaymentEvent>()
            .ProducesValidationProblem();

        static async IAsyncEnumerable<PaymentEvent> StreamEventsAsync(IDocumentStore store)
        {
            using IAsyncDocumentSession? session = store.OpenAsyncSession();

            await using IAsyncEnumerator<StreamResult<PaymentEvent>>? streamResults =
                await session.Advanced.StreamAsync(session.Query<PaymentEvent>(),
                    out StreamQueryStatistics streamQueryStats);

            // Read from the stream
            while (await streamResults.MoveNextAsync())
            {
                // Process the received result
                StreamResult<PaymentEvent> currentResult = streamResults.Current;
                yield return currentResult.Document;
            }
        }
        return routeGroup.WithOpenApi();
    }
}
