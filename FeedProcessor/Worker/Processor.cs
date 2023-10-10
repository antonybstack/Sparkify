using System.Globalization;
using System.IO.Hashing;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;
using HtmlAgilityPack;
using Microsoft.Extensions.ObjectPool;
using Raven.Client;
using Raven.Client.Documents;
using Shared;

namespace Sparkify.Worker;

internal static class Processor
{
    private static readonly ObjectPool<StringBuilder> StringBuilderPool =
        new DefaultObjectPoolProvider().CreateStringBuilderPool();

    private static DateTime LastRefreshSetCache = DateTime.MinValue;

    internal static async Task Blog(Blog blog, CancellationToken cancellationToken)
    {
        try
        {
            var rawHtmlContent = StringBuilderPool.Get();
            var session = DbManager.Store.OpenAsyncSession();
            blog = await session.LoadAsync<Blog>(blog.Id, cancellationToken);
            var feed = await FeedReader.ReadAsync(blog.Link, cancellationToken);

            if (blog is { Title: null } or { Description: null })
            {
                blog.Title = feed.Title;
                blog.Description = feed.Description;
            }

            var blogTimeSeries = session.TimeSeriesFor(blog, "BlogPosted");
            string[]? articleUids = await session.Query<Article>()
                .Where(x => x.BlogId == blog.Id)
                .Select(static x => x.Uid)
                .ToArrayAsync(cancellationToken);

            foreach (var article in feed.Items.ExceptBy(articleUids,
                         static feedItem => XxHash3.HashToUInt64(Encoding.UTF8.GetBytes(feedItem.Id)).ToString()))
            {
                var doc = new HtmlDocument();
                string? htmlContent = article.Content ?? article.Description;
                if (htmlContent is not null)
                {
                    try
                    {
                        doc.LoadHtml(article.Content ?? article.Description);
                        foreach (var node in doc.DocumentNode.SelectNodes("//text()"))
                        {
                            rawHtmlContent.AppendLine(node.InnerText.Trim());
                        }
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }

                var record = new Article
                {
                    BlogId = blog.Id,
                    Link = article.Link.Trim(),
                    Title = WebUtility.HtmlDecode(article.Title.Trim().ToTitleCase()),
                    Date = article.PublishingDate ??
                           (DateTime.TryParse(article.PublishingDateString, out var datetime) ? datetime : null),
                    Uid = XxHash3.HashToUInt64(Encoding.UTF8.GetBytes(article.Id)).ToString(),
                    Categories = article.Categories,
                    Content = WebUtility.HtmlDecode(rawHtmlContent.ToString().Trim())
                };

                var authors = new List<string> { article.Author };

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

                record.Authors = authors
                    .Select(WebUtility.HtmlDecode)
                    .Where(static s => !string.IsNullOrWhiteSpace(s))
                    .OfType<string>()
                    .ToHashSet();

                var blogPostedDateTime = article.PublishingDate ??
                                         (DateTime.TryParse(article.PublishingDateString, out var dt) ? dt : null);
                if (blogPostedDateTime is not null)
                {
                    blogTimeSeries.Append(blogPostedDateTime.Value, 1);
                }
                Console.WriteLine($"Storing: Blog \"{blog.Title}\" Article \"{record.Title}\" - Link: {record.Link}");

                await session.StoreAsync(record, cancellationToken);

                if (htmlContent is not null)
                {
                    string attachmentName = $"{record.Uid}.html";
                    var attachmentStream = new MemoryStream(Encoding.UTF8.GetBytes(htmlContent));
                    session.Advanced.Attachments.Store(record, attachmentName, attachmentStream, "text/html");
                }

                rawHtmlContent.Clear();
            }
            var now = DateTime.UtcNow.AddMinutes(5);
            LastRefreshSetCache = LastRefreshSetCache.AddSeconds(1).CompareTo(now) > 0
                ? LastRefreshSetCache.AddSeconds(1)
                : now;
            session.Advanced.GetMetadataFor(blog)[Constants.Documents.Metadata.Refresh] = LastRefreshSetCache;
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
