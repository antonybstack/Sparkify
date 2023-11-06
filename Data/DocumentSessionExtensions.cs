using Raven.Client;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Queries;

namespace Sparkify;

public static class DocumentSessionExtensions
{
    /// <summary>
    /// Clears the @refresh metadata from all documents
    /// </summary>
    /// <param name="store"></param>
    public static void ClearAllRefreshMetadata(this DocumentStore store)
    {
        var operation = DbManager.Store.Operations.Send(new PatchByQueryOperation(new IndexQuery
        {
            Query = """
                    from @all_docs as doc
                    update {
                        if (doc['@metadata']['@refresh']) {
                            delete doc['@metadata']['@refresh'];
                        }
                    }
                    """
        }));
        operation.WaitForCompletion(); // Optional: Wait for the operation to complete
    }

    /// <summary>
    /// Deletes article documents where the article title and link combination is not unique
    /// </summary>
    /// /// <param name="store"></param>
    public static void DeleteNonUniqueArticles(this DocumentStore store)
    {
        using var session = store.OpenSession();
        var articles = (from article in session.Query<Article>()
            let lastModified =
                session.Advanced.GetMetadataFor(article)[Constants.Documents.Metadata.LastModified] as DateTimeOffset?
            select new
            {
                article.Id, article.Link, LastModified = lastModified
            }).ToList();

        var duplicatesOnLink = articles.GroupBy(static x => x.Link)
            // Order by LastModified and skip the most recent
            .SelectMany(static g => g.OrderBy(static a => a.LastModified).Skip(1))
            .ToList();

        foreach (var dup in duplicatesOnLink)
        {
            session.Delete(dup.Id);
        }
        session.SaveChanges();
    }

    /// <summary>
    /// Initializes all Rss Blog Feeds associated with all existing blogs
    /// </summary>
    /// <param name="store"></param>
    public static void InitializeRssBlogFeeds(this DocumentStore store)
    {
        using var session = store.OpenSession();
        var blogs = session.Query<Blog>().ToList();
        var rssBlogsFeeds = session.Query<RssBlogFeed>().ToList();
        foreach (var blog in blogs)
        {
            if (rssBlogsFeeds.All(x => x.BlogId != blog.Id))
            {
                var rssBlogFeed = new RssBlogFeed
                {
                    BlogId = blog.Id
                };
                session.Store(rssBlogFeed);
            }
        }
        session.SaveChanges();
    }
}
