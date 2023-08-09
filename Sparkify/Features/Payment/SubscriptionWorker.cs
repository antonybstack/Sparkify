using System.Diagnostics;
using Data;
using Raven.Client.Documents;
using Raven.Client.Documents.Subscriptions;
using Raven.Client.Exceptions.Database;
using Raven.Client.Exceptions.Documents.Subscriptions;
using Raven.Client.Exceptions.Security;

namespace Sparkify.Features.Payment;

public class SubscriptionWorker(IDocumentStore store, IEventChannel channel) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await store.Subscriptions.GetSubscriptionStateAsync("PaymentEventsSubscription", token: stoppingToken);
        }
        catch (SubscriptionDoesNotExistException)
        {
            await store.Subscriptions.CreateAsync(new SubscriptionCreationOptions<PaymentEvent>
            {
                Name = "PaymentEventsSubscription",
            }, token: stoppingToken);
        }

        var subscription = store.Subscriptions.GetSubscriptionWorker<PaymentEvent>(
            new SubscriptionWorkerOptions("PaymentEventsSubscription")
            {
                Strategy = SubscriptionOpeningStrategy.TakeOver,
                TimeToWaitBeforeConnectionRetry = TimeSpan.FromSeconds(1),
                MaxErroneousPeriod = TimeSpan.FromSeconds(5)
            });
        subscription.OnUnexpectedSubscriptionError += exception => Debug.WriteLine(exception.Message);

        try
        {
            await subscription.Run(async batch =>
            {
                foreach (var item in batch.Items)
                {
                    await channel.BroadcastAsync(item.Result);
                }
            }, stoppingToken).ConfigureAwait(false);
        }
        catch (SubscriptionClosedException e)
        {
            Debug.WriteLine(e.Message);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);

            if (e is DatabaseDoesNotExistException ||
                e is SubscriptionDoesNotExistException ||
                e is SubscriptionInvalidStateException ||
                e is AuthorizationException)
                throw;

            if (e is SubscriberErrorException)
            {
                Debug.WriteLine($"Subscriber error in subscription: {e.Message}");
            }

            if (e is SubscriptionInUseException)
            {
                Debug.WriteLine($"Subscription in use: {e.Message}");
            }
        }
        finally
        {
            Debug.WriteLine("Subscription ended");
            await subscription.DisposeAsync();
        }
    }
}
