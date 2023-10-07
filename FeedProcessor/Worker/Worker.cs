using System.Diagnostics;
using Raven.Client;
using Raven.Client.Documents.Subscriptions;
using Raven.Client.Exceptions.Database;
using Raven.Client.Exceptions.Documents.Subscriptions;
using Raven.Client.Exceptions.Security;

namespace Sparkify.Worker;

public sealed class Worker(ILogger<Worker> logger) : IHostedLifecycleService
{
    private SubscriptionWorker<Blog> SubscriptionWorker;

    private const string BlogsSubscription = "BlogsSubscription";

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker starting at: {Time}", DateTimeOffset.Now);

        try
        {
            await DbManager.Store.Subscriptions.GetSubscriptionStateAsync(BlogsSubscription, token: cancellationToken);
        }
        catch (SubscriptionDoesNotExistException)
        {
            await DbManager.Store.Subscriptions.CreateAsync(
                new SubscriptionCreationOptions<Blog> { Name = BlogsSubscription },
                token: cancellationToken);
        }

        SubscriptionWorker = DbManager.Store.Subscriptions.GetSubscriptionWorker<Blog>(
            new SubscriptionWorkerOptions(BlogsSubscription) { Strategy = SubscriptionOpeningStrategy.Concurrent, TimeToWaitBeforeConnectionRetry = TimeSpan.FromSeconds(1), MaxErroneousPeriod = TimeSpan.FromSeconds(5) });
        SubscriptionWorker.OnUnexpectedSubscriptionError += exception => Debug.WriteLine(exception.Message);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(SubscriptionWorker, nameof(SubscriptionWorker));

            await SubscriptionWorker.Run(async batch =>
                {
                    foreach (var item in batch.Items)
                    {
                        if (item.Metadata.ContainsKey(Constants.Documents.Metadata.Refresh))
                        {
                            logger.LogInformation("Processing {Title}: {Link}", item.Result.Title, item.Result.Link);
                            continue;
                        }
                        await Processor.Blog(item.Result, cancellationToken);
                    }
                },
                cancellationToken);
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
            {
                throw;
            }

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
            await SubscriptionWorker.DisposeAsync();
        }
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker started at: {Time}", DateTimeOffset.Now);
        return Task.CompletedTask;
    }

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker is stopping at: {Time}", DateTimeOffset.Now);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken) =>
        await SubscriptionWorker.DisposeAsync();

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker stopped at: {Time}", DateTimeOffset.Now);
        return Task.CompletedTask;
    }
}
