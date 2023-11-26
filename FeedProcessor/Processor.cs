using System.IO.Hashing;
using System.Net;
using System.Text;
using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;
using HtmlAgilityPack;
using NUglify;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Common;
using Common.Configuration;
using Microsoft.Extensions.Options;

namespace Sparkify.Worker;

public sealed class Processor(ILogger<Processor> logger, IOptions<FeedProcessorAppOptions> config)
{
    internal async Task FetchBlogArticles(IAsyncDocumentSession session,
        Blog blogRecord,
        CancellationToken cancellationToken)
    {
        // var session = DbManager.Store.OpenAsyncSession();
        List<Feed> feeds = new();

        if (config.Value.FetchFromRssArchive)
        {
            blogRecord = await session.LoadAsync<Blog>(blogRecord.Id, cancellationToken);
            var rssBlogFeed = await session.Query<RssBlogFeed>()
                .Where(x => x.BlogId == blogRecord.Id)
                .FirstOrDefaultAsync(cancellationToken);
            var attachments = session.Advanced.Attachments.GetNames(rssBlogFeed);
            foreach (var attachment in attachments)
            {
                var attachmentStream =
                    await session.Advanced.Attachments.GetAsync(rssBlogFeed, attachment.Name, cancellationToken);
                MemoryStream memoryStream = new();
                await attachmentStream.Stream.CopyToAsync(memoryStream, cancellationToken);
                feeds.Add(FeedReader.ReadFromByteArray(memoryStream.GetBuffer()));
            }
        }
        else
        {
            blogRecord = await session.LoadAsync<Blog>(blogRecord.Id, cancellationToken);
            feeds.Add(
                FeedReader.ReadFromByteArray(await Helpers.DownloadBytesAsync(blogRecord.Link, cancellationToken)));
        }

        foreach (var feed in feeds)
        {
            blogRecord.Title ??= feed.Title;
            blogRecord.Description ??= feed.Description;

            var blogTimeSeries = session.TimeSeriesFor(blogRecord, "BlogPosted");
            var articleUids = await session.Query<Article>()
                .Where(x => x.BlogId == blogRecord.Id)
                .Select(static x => x.Uid)
                .ToListAsync(cancellationToken);

            // filter out articles that have already been stored
            var articles = feed.Items
                .Where(x => !articleUids.Contains(XxHash3.HashToUInt64(Encoding.UTF8.GetBytes(x.Id ?? x.Link)).ToString()))
                .ToList();

            foreach (var article in articles)
            {
                var authors = new List<string>
                {
                    article.Author
                };
                switch (article.SpecificItem)
                {
                    case AtomFeedItem atomFeedItem:
                        authors.Add(atomFeedItem.Author.Name);
                        authors.Add(atomFeedItem.Contributor.Name);
                        break;
                    case Rss10FeedItem rss10FeedItem:
                        authors.Add(rss10FeedItem.DC.Creator);
                        authors.Add(rss10FeedItem.DC.Contributor);
                        authors.Add(rss10FeedItem.DC.Publisher);
                        break;
                    case Rss20FeedItem rss20FeedItem:
                        authors.Add(rss20FeedItem.Author);
                        authors.Add(rss20FeedItem.DC.Creator);
                        authors.Add(rss20FeedItem.DC.Contributor);
                        authors.Add(rss20FeedItem.DC.Publisher);
                        break;
                    case MediaRssFeedItem mediaRssFeedItem:
                        authors.Add(mediaRssFeedItem.Author);
                        authors.Add(mediaRssFeedItem.DC.Creator);
                        authors.Add(mediaRssFeedItem.DC.Contributor);
                        authors.Add(mediaRssFeedItem.DC.Publisher);
                        break;
                }

                var articleDate = article.PublishingDate ??
                                  (DateTimeOffset.TryParse(article.PublishingDateString.AsSpan(),
                                      out var articleDatetime)
                                      ? articleDatetime
                                      : (DateTimeOffset?)null) ??
                                  feed.LastUpdatedDate ??
                                  (DateTimeOffset.TryParse(feed.LastUpdatedDateString.AsSpan(), out var feedDatetime)
                                      ? feedDatetime
                                      : (DateTimeOffset?)null);

                if (articleDate is null)
                {
                    logger.LogWarning("No date found for {Title}: {Link}", article.Title, article.Link);
                    continue;
                }

                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(article.Description ?? feed.Description ?? string.Empty);
                var subtitle = htmlDocument.DocumentNode.SelectSingleNode("//p")?.InnerText ??
                               htmlDocument.DocumentNode.SelectSingleNode("//text()")?.InnerText;
                var articleRecord = new Article
                {
                    BlogId = blogRecord.Id,
                    Link = WebUtility.UrlDecode(article.Link.Trim()),
                    Title = WebUtility.HtmlDecode(article.Title).ToTitleCase(),
                    Subtitle =
                        !string.IsNullOrWhiteSpace(subtitle)
                            ? WebUtility.HtmlDecode(Uglify.Html(subtitle).Code).Trim()
                            : null,
                    Date = articleDate.Value.UtcDateTime,
                    Uid = XxHash3.HashToUInt64(Encoding.UTF8.GetBytes(article.Id ?? article.Link)).ToString(),
                    Categories = article.Categories
                        .Select(WebUtility.HtmlDecode)
                        .Select(static x => x?.Trim())
                        .Select(static x => x?.ToTitleCase())
                        .Where(static s => !string.IsNullOrWhiteSpace(s))
                        .OfType<string>()
                        .ToHashSet(),
                    Authors = authors
                        .Select(WebUtility.HtmlDecode)
                        .Select(static x => x?.Trim())
                        .Where(static s => !string.IsNullOrWhiteSpace(s))
                        .OfType<string>()
                        .ToHashSet()
                };

                blogTimeSeries.Append(articleRecord.Date.UtcDateTime, 1, articleRecord.Uid);

                logger.LogInformation("Storing: Blog {BlogTitle} Article {ArticleTitle} - Link: {ArticleLink}",
                    blogRecord.Title,
                    articleRecord.Title,
                    articleRecord.Link);
                await session.StoreAsync(articleRecord, Ulid.NewUlid(articleRecord.Date).ToString(), cancellationToken);

                string? innerHtmlContent = null;
                if (!string.IsNullOrWhiteSpace(article.Content))
                {
                    htmlDocument.LoadHtml(article.Content);
                    if (WebUtility.HtmlDecode(htmlDocument.DocumentNode.InnerText) !=
                        htmlDocument.DocumentNode.InnerHtml)
                    {
                        innerHtmlContent = htmlDocument.DocumentNode.InnerHtml;
                    }
                }

                if (innerHtmlContent is null)
                {
                    var web = new HtmlWeb();
                    var doc = await web.LoadFromWebAsync(articleRecord.Link, cancellationToken);
                    var htmlll = doc.DocumentNode.SelectSingleNode("//article");
                    innerHtmlContent = htmlll?.InnerHtml;
                }

                if (!string.IsNullOrWhiteSpace(article.Description) && innerHtmlContent is null)
                {
                    htmlDocument.LoadHtml(article.Description);
                    if (WebUtility.HtmlDecode(htmlDocument.DocumentNode.InnerText) !=
                        htmlDocument.DocumentNode.InnerHtml)
                    {
                        innerHtmlContent = htmlDocument.DocumentNode.InnerHtml;
                    }
                }

                if (innerHtmlContent is not null)
                {
                    logger.LogInformation("Storing HTML content for {Title}: {Link}",
                        articleRecord.Title,
                        articleRecord.Link);
                    var attachmentStream = new MemoryStream(Encoding.UTF8.GetBytes(Uglify.Html(innerHtmlContent).Code));
                    session.Advanced.Attachments.Store(articleRecord, "content.html", attachmentStream, "text/html");
                }
                else
                {
                    logger.LogWarning("No HTML content found for {Title}: {Link}",
                        articleRecord.Title,
                        articleRecord.Link);
                }

                articleUids.Add(articleRecord.Uid);
            }
        }
        await session.SaveChangesAsync(cancellationToken);
    }

    internal async Task FetchBlogRssFeed(IAsyncDocumentSession session,
        RssBlogFeed rssBlogFeed,
        CancellationToken cancellationToken)
    {
        var blog = await session.LoadAsync<Blog>(rssBlogFeed.BlogId, cancellationToken);
        if (blog is null)
        {
            logger.LogError("Blog {BlogId} not found", rssBlogFeed.BlogId);
            // delete rss blog feed
            session.Delete(rssBlogFeed);
            return;
        }

        var responseBytes = await Helpers.DownloadBytesAsync(blog.Link);
        var feed = FeedReader.ReadFromByteArray(responseBytes);
        var newIdempotencyKey = XxHash3.HashToUInt64(responseBytes).ToString();

        if (feed.LastUpdatedDateString is not null)
        {
            if (session.Advanced.GetMetadataFor(rssBlogFeed)
                    .TryGetValue("Last-Published-Feed", out var lastPublishedFeed) &&
                lastPublishedFeed == feed.LastUpdatedDateString)
            {
                logger.LogInformation("Skipping Rss Archive {BlogLink} because Last-Published-Feed is the same",
                    blog.Link);
                return;
            }
            session.Advanced.GetMetadataFor(rssBlogFeed)["Last-Published-Feed"] = feed.LastUpdatedDateString;
        }

        if (session.Advanced.GetMetadataFor(rssBlogFeed).TryGetValue("Idempotency-Key", out var idempotencyKey) &&
            idempotencyKey == newIdempotencyKey)
        {
            logger.LogInformation("Skipping Rss Archive {BlogLink} because Idempotency-Key is the same", blog.Link);
            return;
        }
        session.Advanced.GetMetadataFor(rssBlogFeed)["Idempotency-Key"] = newIdempotencyKey;
        using var memoryStream = new MemoryStream();
        memoryStream.Write(responseBytes);
        memoryStream.Position = 0;
        session.Advanced.Attachments.Store(rssBlogFeed,
            $"{Ulid.NewUlid(feed.LastUpdatedDate ?? DateTimeOffset.UtcNow).ToString()}.rss",
            memoryStream,
            "application/rss+xml");

        await session.SaveChangesAsync(cancellationToken);
    }
}
