using System.Text.Json;
using Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Raven.Client.Documents;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace Sparkify.Features.Payment;
public static class PaymentApiEndpointRouteBuilderExtensions
{
    private static readonly byte[] s_lineBreak = "\n"u8.ToArray();

    /// <summary>
    ///     Add endpoints for registering, logging in, and logging out using ASP.NET Core Identity.
    /// </summary>
    /// <typeparam name="TUser">
    ///     The type describing the user. This should match the generic parameter in
    ///     <see cref="UserManager{TUser}" />.
    /// </typeparam>
    /// <param name="endpoints">
    ///     The <see cref="IEndpointRouteBuilder" /> to add the identity endpoints to.
    ///     Call <see cref="EndpointRouteBuilderExtensions.MapGroup(IEndpointRouteBuilder, string)" /> to add a prefix to all
    ///     the endpoints.
    /// </param>
    /// <returns>An <see cref="IEndpointConventionBuilder" /> to further customize the added endpoints.</returns>
    public static IEndpointConventionBuilder MapPaymentApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder routeGroup = endpoints.MapGroup("api/payment");

        routeGroup.MapGet("/", async Task<Results<Ok<List<PaymentEvent>>, NotFound, ValidationProblem>>
                ([FromQuery] string? id, [FromServices] IDocumentStore store) =>
            {
                try
                {
                    using IAsyncDocumentSession session = store.OpenAsyncSession();

                    IRavenQueryable<PaymentEvent> query = session
                        .Query<PaymentEvent>();

                    if (id is not null)
                    {
                        query = query.Where(x => x.ReferenceId == id);
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

        routeGroup.MapGet("/stream", async (HttpContext context, IDocumentStore store) =>
            {
                await foreach (PaymentEvent paymentEvent in StreamEventsAsync(store))
                {
                    await JsonSerializer.SerializeAsync(context.Response.Body, paymentEvent);
                    await context.Response.Body.WriteAsync(s_lineBreak);
                }
            })
            .Produces<List<PaymentEvent>>()
            .ProducesValidationProblem();

        routeGroup.MapPost("/", async Task<Results<Created<PaymentEvent>, ValidationProblem>>
                ([FromBody] PaymentEvent @event, [FromServices] IDocumentStore store) =>
            {
                try
                {
                    using IAsyncDocumentSession session = store.OpenAsyncSession();
                    await session.StoreAsync(@event);
                    await session.SaveChangesAsync();
                    return TypedResults.Created(@event.Id, @event);
                }
                catch (Exception e)
                {
                    return TypedResults.ValidationProblem(
                        new Dictionary<string, string[]> { { "errors", new[] { e.Message } } });
                }
            })
            .Produces<PaymentEvent>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        return routeGroup.WithOpenApi();
    }

    private static async IAsyncEnumerable<PaymentEvent> StreamEventsAsync(IDocumentStore store)
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
}

