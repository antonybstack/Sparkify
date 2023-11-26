using System.Globalization;
using Common.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Raven.Client;
using Raven.Client.Documents.Subscriptions;
using Raven.Client.Exceptions.Database;
using Raven.Client.Exceptions.Documents.Subscriptions;
using Raven.Client.Exceptions.Security;

namespace Sparkify.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    [FromServices]
    Processor processor,
    IOptions<FeedProcessorAppOptions> config)
    : IHostedLifecycleService
{
    private const string BlogsSubscription = "BlogsSubscription";
    private const string RssArchiveSubscription = "RssArchiveSubscription";
    private SubscriptionWorker<Blog> _blogsSubscriptionWorker;
    private SubscriptionWorker<RssBlogFeed> _rssArchiveSubscriptionWorker;

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker starting at: {Time}", DateTimeOffset.UtcNow);

        _blogsSubscriptionWorker = await InitSubscriptionWorker<Blog>(BlogsSubscription, cancellationToken);
        _rssArchiveSubscriptionWorker =
            await InitSubscriptionWorker<RssBlogFeed>(RssArchiveSubscription, cancellationToken);

        ArgumentNullException.ThrowIfNull(_blogsSubscriptionWorker, nameof(_blogsSubscriptionWorker));
        ArgumentNullException.ThrowIfNull(_rssArchiveSubscriptionWorker, nameof(_rssArchiveSubscriptionWorker));
    }

    private async Task<SubscriptionWorker<T>> InitSubscriptionWorker<T>(string subscriptionName,
        CancellationToken cancellationToken) where T : class, IEntity
    {
        try
        {
            await DbManager.Store.Subscriptions.DeleteAsync(subscriptionName, token: cancellationToken);
            await DbManager.Store.Subscriptions.CreateAsync(
                new SubscriptionCreationOptions<T>
                {
                    Name = subscriptionName
                },
                token: cancellationToken);

            var blogsSubscriptionWorker = DbManager.Store.Subscriptions.GetSubscriptionWorker<T>(
                new SubscriptionWorkerOptions(subscriptionName)
                {
                    Strategy = SubscriptionOpeningStrategy.TakeOver,
                    TimeToWaitBeforeConnectionRetry = TimeSpan.FromSeconds(5),
                    MaxErroneousPeriod = TimeSpan.FromSeconds(10)
                });
            blogsSubscriptionWorker.OnUnexpectedSubscriptionError += exception =>
                logger.LogError(exception, "Unexpected subscription error with {SubscriptionName}", subscriptionName);
            return blogsSubscriptionWorker;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error initializing subscription {SubscriptionName}", subscriptionName);
            throw;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var blogsSubscriptionWorkerTask = _blogsSubscriptionWorker.Run(async batch =>
                {
                    using var session = batch.OpenAsyncSession();
                    foreach (var item in batch.Items)
                    {
                        logger.LogInformation("Processing {Title}: {Link}", item.Result.Title, item.Result.Link);
                        if (item.Metadata.TryGetValue(Constants.Documents.Metadata.Refresh, out var value))
                        {
                            if (DateTimeOffset.TryParse(value.AsSpan(),
                                    DateTimeFormatInfo.InvariantInfo,
                                    DateTimeStyles.AdjustToUniversal,
                                    out var refresh) &&
                                refresh > DateTimeOffset.UtcNow)
                            {
                                logger.LogInformation("{Title}: {Link} on cooldown. Skipping...",
                                    item.Result.Title,
                                    item.Result.Link);
                                continue;
                            }
                        }
                        try
                        {
                            await processor.FetchBlogArticles(session, item.Result, cancellationToken);

                            // Set refresh metadata
                            session.Advanced.GetMetadataFor(item.Result)[Constants.Documents.Metadata.Refresh] =
                                // Add a random offset to prevent thundering herd
                                DateTimeOffset.UtcNow.AddSeconds(new Random().Next(
                                    config.Value.BlogRetrievalIntervalSeconds,
                                    config.Value.BlogRetrievalIntervalSeconds + 30));
                            logger.LogInformation("Setting {Title} to fetch articles again at {Refresh}",
                                item.Result.Title,
                                session.Advanced.GetMetadataFor(item.Result)[Constants.Documents.Metadata.Refresh]);
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "Error processing {Title}: {Link}", item.Result.Title, item.Result.Link);
                        }
                    }
                    await session.SaveChangesAsync(cancellationToken);
                },
                cancellationToken);

            var rssArchiveSubscriptionWorkerTask = _rssArchiveSubscriptionWorker.Run(async batch =>
                {
                    using var session = batch.OpenAsyncSession();
                    foreach (var item in batch.Items)
                    {
                        logger.LogInformation("Rss Archive Processing {BlogId}", item.Result.BlogId);
                        try
                        {
                            if (item.Metadata.TryGetValue(Constants.Documents.Metadata.Refresh, out var value))
                            {
                                if (DateTimeOffset.TryParse(value.AsSpan(),
                                        DateTimeFormatInfo.InvariantInfo,
                                        DateTimeStyles.AdjustToUniversal,
                                        out var refresh) &&
                                    refresh > DateTimeOffset.UtcNow)
                                {
                                    logger.LogInformation("{BlogId} on cooldown. Skipping...", item.Result.BlogId);
                                    continue;
                                }
                            }
                            await processor.FetchBlogRssFeed(session, item.Result, cancellationToken);

                            // Set refresh metadata
                            item.Metadata[Constants.Documents.Metadata.Refresh] =
                                DateTimeOffset.UtcNow.AddSeconds(
                                    // Add a random offset to prevent thundering herd
                                    new Random().Next(config.Value.RssArchiveIntervalSeconds,
                                        config.Value.RssArchiveIntervalSeconds + 600));
                            logger.LogInformation("Setting {BlogId} to run Rss Archive again at {Refresh}",
                                item.Result.BlogId,
                                item.Metadata[Constants.Documents.Metadata.Refresh]);
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "Rss Archive {BlogId} processing error", item.Result.BlogId);
                        }
                    }
                    await session.SaveChangesAsync(cancellationToken);
                },
                cancellationToken);

            await Task.WhenAll(blogsSubscriptionWorkerTask, rssArchiveSubscriptionWorkerTask);
        }
        catch (SubscriptionClosedException e)
        {
            logger.LogError(e, "Subscription closed");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error processing subscription");

            switch (e)
            {
                case DatabaseDoesNotExistException:
                case SubscriptionDoesNotExistException:
                case SubscriptionInvalidStateException:
                case AuthorizationException:
                    logger.LogError(e, "Subscription error");
                    throw;
                case SubscriberErrorException:
                    logger.LogError(e, "Subscriber error in subscription");
                    break;
                case SubscriptionInUseException:
                    logger.LogError(e, "Subscription in use");
                    break;
            }
        }
        finally
        {
            logger.LogInformation("Subscription ended");
            await _blogsSubscriptionWorker.DisposeAsync();
        }
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker started at: {Time}", DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker is stopping at: {Time}", DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken) =>
        await _blogsSubscriptionWorker.DisposeAsync();

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker stopped at: {Time}", DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }
}
