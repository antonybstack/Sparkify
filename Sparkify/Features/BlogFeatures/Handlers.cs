using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Nager.PublicSuffix;
using OpenTelemetry.Trace;
using Raven.Client.Documents;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Queries.Highlighting;
using Raven.Client.Documents.Queries.Suggestions;
using Raven.Client.Documents.Session;
using Common;
using SkiaSharp;
using Sparkify.Indexes;
using Svg.Skia;

namespace Sparkify.Features.BlogFeatures;

/* TODO: Features
Regex Flag
AND, OR, NOT flags
Search by author
Search by tags
 limit search to 256 chars?
 onclick content makes call to full content and expands
 - use on hover preload
 */

internal static class ApiEndpointRouteBuilderExtensions
{
    private const string TitleFieldName = nameof(Article.Title);
    // private const string StartTag = "<b style=\"background:rgba(0, 180, 145, 0.5); color: rgba(255, 255, 255, 0.8);\">";
    // private const string EndTag = "</b>";
    // private const string StartTag = "<b><mark>";
    // private const string EndTag = "</mark></b>";
    // private const string StartDelimiter = "ßßßßß";
    // private const string EndDelimiter = "ΩΩΩΩΩ";
    private const string StartTag = "...";
    private const string EndTag = "...";
    private const string StartDelimiter = "";
    private const string EndDelimiter = "";
    private const string Ellipsis = "...";
    private static readonly Expression<Func<ArticleIndex.ArticleSearchResults, string>> _titleSelectorString =
        static x => x.Title;
    private static readonly Expression<Func<ArticleIndex.ArticleSearchResults, object>> _titleSelectorObject =
        static x => x.Title;
    private static readonly Expression<Func<ArticleIndex.ArticleSearchResults, string>> _contentSelectorString =
        static x => x.Content;
    private static readonly Expression<Func<ArticleIndex.ArticleSearchResults, object>> _contentSelectorObject =
        static x => x.Content;
    private static readonly string[] _fields =
    {
        nameof(ArticleIndex.ArticleSearchResults.Id),
        nameof(ArticleIndex.ArticleSearchResults.BlogId),
        nameof(ArticleIndex.ArticleSearchResults.Title),
        nameof(ArticleIndex.ArticleSearchResults.Content),
        nameof(ArticleIndex.ArticleSearchResults.Categories),
        nameof(ArticleIndex.ArticleSearchResults.Authors),
        nameof(ArticleIndex.ArticleSearchResults.Link),
        nameof(ArticleIndex.ArticleSearchResults.Date)
    };
    private static readonly string[] _fieldsNoTitle =
    {
        nameof(ArticleIndex.ArticleSearchResults.Id),
        nameof(ArticleIndex.ArticleSearchResults.Link),
        nameof(ArticleIndex.ArticleSearchResults.Date)
    };
    private static readonly IEnumerable<SuggestionWithTerm> _suggestions = new[]
    {
        new SuggestionWithTerm(TitleFieldName)
        {
            Options = new SuggestionOptions
            {
                Accuracy = 0.2f,
                PageSize = 3,
                Distance = StringDistanceTypes.Levenshtein,
                SortMode = SuggestionSortMode.Popularity
            }
        },
        new SuggestionWithTerm(TitleFieldName)
        {
            Options = new SuggestionOptions
            {
                Accuracy = 0.2f,
                PageSize = 3,
                Distance = StringDistanceTypes.JaroWinkler,
                SortMode = SuggestionSortMode.Popularity
            }
        },
        new SuggestionWithTerm(TitleFieldName)
        {
            Options = new SuggestionOptions
            {
                Accuracy = 0.2f,
                PageSize = 3,
                Distance = StringDistanceTypes.NGram,
                SortMode = SuggestionSortMode.Popularity
            }
        }
    };
    private static readonly HighlightingOptions _tagsToUse = new()
    {
        PreTags = new[]
        {
            StartDelimiter
        },
        PostTags = new[]
        {
            EndDelimiter
        }
    };
    private static readonly string _cacheControl12Hour =
        $"public,max-age={TimeSpan.FromHours(12).TotalSeconds.ToString(CultureInfo.InvariantCulture)}";

    private static readonly string _cacheControl5Minute =
        $"public,max-age={TimeSpan.FromMinutes(5).TotalSeconds.ToString(CultureInfo.InvariantCulture)}";

    public static IEndpointConventionBuilder MapBlogsApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var routeGroup = endpoints.MapGroup("api/blog");

        routeGroup.MapGet("/search",
            static async Task<Results<Ok<Payload<IEnumerable<ArticleIndex.Base>>>, NotFound, ProblemHttpResult>> (
                HttpContext context,
                Tracer tracer,
                string query,
                int? page) =>
            {
                context.Response.Headers.CacheControl = _cacheControl5Minute;

                using var searchSpan = tracer.StartActiveSpan("Search");
                var startTime = Stopwatch.GetTimestamp();

                using var session = DbManager.Store.OpenAsyncSession();

                QueryStatistics? stats;
                ArticleIndex.Base[] articles;
                CleanQuery(query, out var queryCleaned);
                var queryProcessed = CleanQueryAndAddWildcard(ref query);
                using var executeQuerySpan = tracer.StartActiveSpan("ExecuteSearchQuery");
                if (!queryProcessed)
                {
                    executeQuerySpan.SetAttribute("Query", string.Empty);
                    articles = await session.Query<ArticleIndex.Base, ArticleIndex>()
                        .Statistics(out stats)
                        .OrderByDescending(static x => x.Id)
                        .Skip(((page ?? 1) - 1) * 10)
                        .Take(10)
                        .ProjectInto<ArticleIndex.ArticleNoSearchResults>()
                        .ToArrayAsync();
                }
                else
                {
                    var queryWithoutWildCard = string.Create(
                        query.Length - 1,
                        query,
                        static (span, original) => original.AsSpan()[..^1].CopyTo(span));
                    Debug.WriteLine($"'{query}'");
                    Debug.WriteLine($"{queryWithoutWildCard}'");
                    Debug.WriteLine("-----");
                    executeQuerySpan.SetAttribute("Query", query);
                    executeQuerySpan.SetAttribute("QueryWithoutWildcard", queryWithoutWildCard);
                    // custom highlight with RQL https://ravendb.net/docs/article-page/6.0/csharp/client-api/session/querying/text-search/highlight-query-results#highlight---customize-tags
                    // can also stream results: https://ravendb.net/docs/article-page/6.0/Csharp/client-api/session/querying/how-to-stream-query-results#stream-related-documents

                    IAsyncDocumentQuery<ArticleIndex.ArticleSearchResults>? documentQuery;
                    /*
                     You could optimize by aggregating all fields into one field and then searching that field,
                     and include separaters between the fields so that you can highlight the field that matched.
                     */

                    if (query.Contains(' ', StringComparison.OrdinalIgnoreCase))
                    {
                        documentQuery = session.Advanced
                            .AsyncDocumentQuery<ArticleIndex.ArticleSearchResults, ArticleIndex>()
                            // Content all words && Title all words
                            .OpenSubclause()
                            .Search(_titleSelectorString, queryWithoutWildCard, SearchOperator.And)
                            .AndAlso()
                            .Search(_contentSelectorString, queryWithoutWildCard, SearchOperator.And)
                            .CloseSubclause()
                            .Boost(100M)
                            .OrElse()
                            // Content all words || Title all words
                            .OpenSubclause()
                            .Search(_titleSelectorString, queryWithoutWildCard, SearchOperator.And)
                            .OrElse()
                            .Search(_contentSelectorString, queryWithoutWildCard, SearchOperator.And)
                            .CloseSubclause()
                            .Boost(95M)
                            .OrElse()
                            // Title all words && content all words (proximity)
                            .OpenSubclause()
                            .Search(_titleSelectorString, queryWithoutWildCard, SearchOperator.And)
                            .AndAlso()
                            .Search(_contentSelectorString, queryWithoutWildCard)
                            .Proximity(8)
                            .CloseSubclause()
                            .Boost(90M)
                            .OrElse()
                            // Title all words (*) && Content all words (proximity)
                            .OpenSubclause()
                            .Search(_titleSelectorString, query, SearchOperator.And)
                            .AndAlso()
                            .Search(_contentSelectorString, queryWithoutWildCard)
                            .Proximity(8)
                            .CloseSubclause()
                            .Boost(80M)
                            .OrElse()
                            // Title all words (proximity) && content all words (proximity)
                            .OpenSubclause()
                            .Search(_titleSelectorString, queryWithoutWildCard)
                            .Proximity(8)
                            .AndAlso()
                            .Search(_contentSelectorString, queryWithoutWildCard)
                            .Proximity(8)
                            .CloseSubclause()
                            .Boost(100M)
                            .OrElse()
                            // Title all words (*) || Content all words (proximity)
                            .OpenSubclause()
                            .Search(_titleSelectorString, query, SearchOperator.And)
                            .OrElse()
                            .Search(_contentSelectorString, queryWithoutWildCard)
                            .Proximity(8)
                            .CloseSubclause()
                            .Boost(60M)
                            .OrElse()
                            // Title all words (proximity) || content all words (proximity)
                            .OpenSubclause()
                            .Search(_titleSelectorString, queryWithoutWildCard)
                            .Proximity(8)
                            .OrElse()
                            .Search(_contentSelectorString, queryWithoutWildCard)
                            .Proximity(8)
                            .CloseSubclause()
                            .Boost(50M)
                            .OrElse()
                            // Title any words (anywhere) || content all words (proximity)
                            .OpenSubclause()
                            .Search(_titleSelectorString, query, SearchOperator.Or)
                            .OrElse()
                            .Search(_contentSelectorString, queryWithoutWildCard)
                            .Proximity(8)
                            .CloseSubclause()
                            .Boost(40M);
                    }
                    else
                    {
                        documentQuery = session.Advanced
                            .AsyncDocumentQuery<ArticleIndex.ArticleSearchResults, ArticleIndex>()
                            .OpenSubclause()
                            .Search(_titleSelectorString, queryWithoutWildCard)
                            .AndAlso()
                            .Search(_contentSelectorString, queryWithoutWildCard)
                            .CloseSubclause()
                            .Boost(100M)
                            //
                            .OrElse()
                            .OpenSubclause()
                            .Search(_titleSelectorString, query)
                            .AndAlso()
                            .Search(_contentSelectorString, query)
                            .CloseSubclause()
                            .Boost(90M)
                            //
                            .OrElse()
                            .OpenSubclause()
                            .Search(_titleSelectorString, queryWithoutWildCard)
                            .OrElse()
                            .Search(_contentSelectorString, queryWithoutWildCard)
                            .CloseSubclause()
                            .Boost(50M)
                            //
                            .OrElse()
                            .OpenSubclause()
                            .Search(_titleSelectorString, query)
                            .OrElse()
                            .Search(_contentSelectorString, query)
                            .CloseSubclause()
                            .Boost(40M);
                    }

                    articles = await documentQuery.ToQueryable()
                        .Statistics(out stats)
                        // .Highlight(static x => x.Title, 200, 4, _tagsToUse, out var titleHighlights)
                        .Highlight(static x => x.Content, 120, 4, _tagsToUse, out var contentHighlights)
                        .ProjectInto<ArticleIndex.ArticleSearchResults>()
                        .Skip(((page ?? 1) - 1) * 10)
                        .Take(10)
                        .ToArrayAsync();

                    foreach (var article in articles)
                    {
                        // if (titleHighlights.GetFragments(article.Id).Length is not 0)
                        // {
                        // article.Title = ModifyHighlight(titleHighlights.GetFragments(article.Id).FirstOrDefault(), queryCleaned) ?? article.Title;
                        article.Title = StringExtensions.HighlightMatches(article.Title, queryCleaned);
                        // }
                        if (contentHighlights.GetFragments(article.Id).Length is not 0)
                        {
                            article.Content = StringExtensions.HighlightMatches(string.Join(Ellipsis,
                                    contentHighlights.GetFragments(article.Id)
                                        .Select(static s => s.Trim())),
                                queryCleaned,
                                true);
                            // article.Content = article.Content.Replace("  ", " ");
                            // article.Content = ModifyContentHighlight(
                            //     string.Join(Ellipsis,
                            //         contentHighlights.GetFragments(article.Id)
                            //             .Select(static s => s.Trim())),
                            //     queryCleaned);
                        }
                        // if (titleHighlights.GetFragments(article.Id).Length is not 0)
                        // {
                        //     article.Title = titleHighlights.GetFragments(article.Id).FirstOrDefault() ?? article.Title;
                        // }
                        // if (contentHighlights.GetFragments(article.Id).Length is not 0)
                        // {
                        //     article.Content =
                        //         string.Join(Ellipsis,
                        //             contentHighlights.GetFragments(article.Id)
                        //                 .Select(static s => s.Trim()));
                        // }
                    }
                }

                if (stats.TotalResults is 0)
                {
                    using var executeSuggestionQuerySpan = tracer.StartActiveSpan("ExecuteSuggestionQuery");
                    var suggestionsTasks = _suggestions
                        .Select(request =>
                        {
                            request.Term = query;
                            return session.Advanced
                                .AsyncDocumentQuery<ArticleIndex.ArticleSearchResults, ArticleIndex>()
                                .SuggestUsing(request)
                                .ExecuteLazyAsync();
                        })
                        .ToArray();

                    var freqMap = new Dictionary<string, int>();
                    foreach (var resultTask in suggestionsTasks)
                    {
                        if (!(await resultTask.Value).TryGetValue(TitleFieldName, out var suggestionResult))
                        {
                            continue;
                        }
                        foreach (var suggestion in suggestionResult.Suggestions)
                        {
                            freqMap.TryGetValue(suggestion, out var count);
                            freqMap[suggestion] = count + 1;
                        }
                    }
                    var minHeap = new PriorityQueue<(string suggestion, int freq), int>(3);
                    foreach (var entry in freqMap)
                    {
                        if (minHeap.Count < 3)
                        {
                            minHeap.Enqueue((entry.Key, entry.Value), entry.Value);
                        }
                        else if (entry.Value > minHeap.Peek().freq)
                        {
                            minHeap.Dequeue();
                            minHeap.Enqueue((entry.Key, entry.Value), entry.Value);
                        }
                    }

                    articles = new ArticleIndex.ArticleSearchResults[minHeap.Count];
                    var i = 0;
                    while (minHeap.TryDequeue(out var suggestion, out _))
                    {
                        articles[i++] = new ArticleIndex.ArticleSearchResults
                        {
                            Title = suggestion.suggestion
                        };
                    }
                }
                // remove empty strings in each authors collection
                // foreach (var article in articles)
                // {
                //     article.Authors = article.Authors?
                //         .Where(static x => !string.IsNullOrWhiteSpace(x))
                //         .ToArray();
                // }
                return TypedResults.Ok(
                    new Payload<IEnumerable<ArticleIndex.Base>>
                    {
                        Data = articles,
                        Stats = new RequestStatistics
                        {
                            DurationInMs = Stopwatch.GetElapsedTime(startTime).Milliseconds,
                            TotalResults = stats.TotalResults
                        }
                    }
                );
            });

        routeGroup.MapGet("blogs/{id}/image/{name}",
            static async Task<Results<FileStreamHttpResult, NotFound, ProblemHttpResult>> (HttpContext context,
                string id,
                string name) =>
            {
                context.Response.Headers.CacheControl = _cacheControl12Hour;
                using var session = DbManager.Store.OpenAsyncSession();

                var attachment = await session.Advanced.Attachments.GetAsync($"blogs/{id}", name);
                if (attachment is null)
                {
                    return TypedResults.NotFound();
                }
                var entityTag = new EntityTagHeaderValue($"\"{attachment.Details.ChangeVector}\"");

                using var memoryStream = new MemoryStream();
                await attachment.Stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                var image = SKBitmap.Decode(memoryStream);
                var aspectRatio = (float)image.Width / image.Height;
                var scaledBitmap = image.Resize(new SKImageInfo(120, (int)(120 / aspectRatio)), SKFilterQuality.High);
                var scaledImage = SKImage.FromBitmap(scaledBitmap);
                var data = scaledImage.Encode(SKEncodedImageFormat.Png, 100);

                return TypedResults.File(new MemoryStream(data.ToArray()),
                    attachment.Details.ContentType,
                    attachment.Details.Name,
                    entityTag: entityTag);
            });

        routeGroup.MapDelete("blogs/{id}/image/{name}",
            static async Task<Results<NoContent, NotFound, ProblemHttpResult>> (string id, string name) =>
            {
                using var session = DbManager.Store.OpenAsyncSession();

                var attachment = await session.Advanced.Attachments.GetAsync($"blogs/{id}", name);
                if (attachment is null)
                {
                    return TypedResults.NotFound();
                }
                session.Advanced.Attachments.Delete($"blogs/{id}", name);
                await session.SaveChangesAsync();
                return TypedResults.NoContent();
            });

        routeGroup.MapPut("blogs/{id}/image/{name}/set",
            static async Task<Results<NoContent, NotFound, ProblemHttpResult>> (string id, string name) =>
            {
                using var session = DbManager.Store.OpenAsyncSession();

                var blog = await session.LoadAsync<Blog>($"blogs/{id}");
                session.Advanced.GetMetadataFor(blog)["logo"] = name;
                await session.SaveChangesAsync();
                return TypedResults.NoContent();
            });

        routeGroup.MapPost("blogs/{id}/image/upload",
                async Task<IActionResult> (string id, IFormFile filee) =>
                {
                    using var session = DbManager.Store.OpenAsyncSession();

                    var blog = await session.LoadAsync<Blog>($"blogs/{id}");
                    var fileStream = filee.OpenReadStream();

                    var ms = new MemoryStream();
                    // get type
                    var fileType = filee.ContentType;

                    if (fileType.Contains("svg", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var fileBytes = new byte[fileStream.Length];
                        await fileStream.ReadExactlyAsync(fileBytes);
                        ms.Write(Svg2Png(fileBytes));
                    }
                    else
                    {
                        var image = SKImage.FromBitmap(SKBitmap.Decode(fileStream));
                        image.Encode(SKEncodedImageFormat.Png, 100).SaveTo(ms);
                    }
                    ms.Position = 0;
                    var fileName = $"{Ulid.NewUlid()}.png";
                    session.Advanced.Attachments.Store(blog, fileName, ms, "image/png");
                    session.Advanced.GetMetadataFor(blog)["logo"] = fileName;
                    await session.SaveChangesAsync();
                    return new OkObjectResult(new
                    {
                        fileName
                    });
                })
            .DisableAntiforgery();

        routeGroup.MapPut("blogs/cleanupimages",
            static async Task<Results<NoContent, NotFound, ProblemHttpResult>> () =>
            {
                using var session = DbManager.Store.OpenAsyncSession();

                // get all blogs, get all attachment names, remove all attachments not reference by meta data "logo"
                var blogs = await session.Advanced.AsyncDocumentQuery<Blog>()
                    .ToArrayAsync();
                foreach (var blog in blogs)
                {
                    var attachments = session.Advanced.Attachments.GetNames(blog);
                    if (!session.Advanced.GetMetadataFor(blog).TryGetValue("logo", out var logo))
                    {
                        continue;
                    }
                    foreach (var attachment in attachments)
                    {
                        if (attachment.Name != logo)
                        {
                            session.Advanced.Attachments.Delete(blog, attachment.Name);
                        }
                    }
                    await session.SaveChangesAsync();
                }
                return TypedResults.NoContent();
            });

        routeGroup.MapPost("/",
                static async Task<Results<Ok<Payload<Blog>>, NoContent, ProblemHttpResult>> (HttpContext context,
                    [FromServices]
                    FaviconHttpClient faviconHttpClient) =>
                {
                    DomainParser domainParser = new(new WebTldRuleProvider());
                    try
                    {
                        var request = await context.Request.BodyReader.ReadAsync();
                        var links = Encoding.UTF8.GetString(request.Buffer)
                            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        using var session = DbManager.Store.OpenAsyncSession();
                        var existingBlogs = await session.Advanced.AsyncDocumentQuery<Blog>()
                            .WhereIn(static x => x.Link, links)
                            .ToArrayAsync();
                        Dictionary<Blog, Collection<FaviconPacket>> blogStreamMap = new();
                        foreach (var link in links)
                        {
                            var domainInfo = domainParser.Parse(link);
                            if (!domainParser.IsValidDomain(domainInfo.RegistrableDomain))
                            {
                                continue;
                            }
                            var company = !string.IsNullOrWhiteSpace(domainInfo.Domain)
                                ? domainInfo.Domain.ToTitleCase()
                                : null;
                            Blog blog = new()
                            {
                                Company = company, Link = link
                            };
                            if (existingBlogs.All(x => x.Link != link))
                            {
                                await session.StoreAsync(blog, existingBlogs.First(x => x.Link == link).Id);
                                blog = await session.LoadAsync<Blog>(blog.Id);
                            }
                            else
                            {
                                blog = existingBlogs.First(x => x.Link == link);
                            }
                            var baseDomainUri = new UriBuilder("https", domainInfo.RegistrableDomain).Uri;
                            await foreach (var packet in faviconHttpClient.GetFaviconDataStreamPackets(baseDomainUri))
                            {
                                blogStreamMap.TryAdd(blog, new Collection<FaviconPacket>());
                                blogStreamMap[blog].Add(packet);
                            }
                            baseDomainUri = new UriBuilder("https", domainInfo.Hostname).Uri;
                            await foreach (var packet in faviconHttpClient.GetFaviconDataStreamPackets(baseDomainUri))
                            {
                                blogStreamMap.TryAdd(blog, new Collection<FaviconPacket>());
                                blogStreamMap[blog].Add(packet);
                            }
                        }
                        foreach (var (blog, packets) in blogStreamMap)
                        {
                            foreach (var packet in packets.OrderByDescending(static x => x.Size).Take(10).ToArray())
                            {
                                session.Advanced.Attachments.Store(blog, packet.Name, packet.Stream, "image/png");
                            }

                            var existingAttachments = session.Advanced.Attachments.GetNames(blog)
                                .Select(static x => new
                                {
                                    x.Name, x.Size
                                });
                            var newAttachments = packets.Select(static x => new
                            {
                                x.Name, x.Size
                            });
                            session.Advanced.GetMetadataFor(blog)["logo"] = existingAttachments
                                .Concat(newAttachments)
                                .MaxBy(static x => x.Size)
                                ?.Name;

                            // try to create RssBlogFeed if doesnt exist
                            var rssBlogFeed = await session.Query<RssBlogFeed>()
                                .FirstOrDefaultAsync(x => x.BlogId == blog.Id);
                            if (rssBlogFeed is null)
                            {
                                rssBlogFeed = new RssBlogFeed
                                {
                                    BlogId = blog.Id
                                };
                                await session.StoreAsync(rssBlogFeed);
                            }
                        }

                        await session.SaveChangesAsync();
                        foreach (var data in blogStreamMap.Values)
                        {
                            foreach (var packet in data)
                            {
                                await packet.Stream.DisposeAsync();
                            }
                        }
                        return TypedResults.NoContent();
                    }
                    catch (Exception e)
                    {
                        return TypedResults.Problem(e.Message);
                    }
                })
            .AddEndpointFilter(static async (context, next) =>
            {
                var ipAddress = context.HttpContext.Request.HttpContext.Connection.RemoteIpAddress;
                // if not local loopback, then return 404
                if (ipAddress is null || !IPAddress.IsLoopback(ipAddress))
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.HttpContext.Response.WriteAsync("Forbidden");
                }
                return await next(context);
            });

        routeGroup.MapPost("/FixBlogAttachments",
            async static () =>
            {
                using var session = DbManager.Store.OpenAsyncSession();
                var blogs = await session.Advanced.AsyncDocumentQuery<Blog>()
                    .ToArrayAsync();

                foreach (var blog in blogs)
                {
                    // session.Advanced.GetMetadataFor(blog).TryGetValue("logo", out var logoMeta);
                    // if (logoMeta is not null && !logoMeta.Contains(".png", StringComparison.InvariantCultureIgnoreCase))
                    // {
                    //     session.Advanced.GetMetadataFor(blog)["logo"] += ".png";
                    // }
                    var attachments = session.Advanced.Attachments.GetNames(blog);
                    foreach (var attachment in attachments)
                    {
                        if (attachment.Name.Contains(".png", StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }
                        session.Advanced.Attachments.Rename(blog, attachment.Name, $"{attachment.Name}.png");
                    }
                }
                await session.SaveChangesAsync();
            });

        // Create endpoint that returns all blogs and their attachments in an html view
        routeGroup.MapGet("/blogs/attachments/{page}",
                static async (HttpContext context, int page) =>
                {
                    context.Response.ContentType = "text/html";
                    try
                    {
                        using var session = DbManager.Store.OpenAsyncSession();
                        var blogs = await session.Advanced.AsyncDocumentQuery<Blog>()
                            .OrderBy(static x => x.Company)
                            .Skip((page - 1) * 10)
                            .Take(page * 10)
                            .ToArrayAsync();
                        var sb = new StringBuilder();
                        foreach (var blog in blogs)
                        {
                            sb.AppendLine($"<h2>{blog.Company}|{blog.Id}</h1>");
                            var attachments = session.Advanced.Attachments.GetNames(blog);
                            session.Advanced.GetMetadataFor(blog).TryGetValue("logo", out var currentLogo);
                            sb.AppendLine(currentLogo is not null
                                ? $"<p>current:<br><img src=\"/api/blog/{
                                    blog.Id
                                }/image/{
                                    currentLogo
                                }\" /><br>------</p>"
                                : "N/A");
                            foreach (var attachment in attachments)
                            {
                                if (attachment.Name == currentLogo)
                                {
                                    continue;
                                }
                                sb.AppendLine($"<p><img src=\"/api/blog/{blog.Id}/image/{attachment.Name}\" /></p>");
                                sb.AppendLine($"<button onclick=\"deleteImage('{
                                    blog.Id
                                }', '{
                                    attachment.Name
                                }')\">Delete</button>");
                                sb.AppendLine($"<button onclick=\"setAsLogo('{
                                    blog.Id
                                }', '{
                                    attachment.Name
                                }')\">Set as Logo</button>");
                                sb.AppendLine("<br>");
                            }
                        }
                        // context.Response.ContentType = "text/html";
                        var htmlContent = $$"""
                                            <script>
                                                function deleteImage(blogId, imageName) {
                                                    fetch(`/api/blog/${blogId}/image/${imageName}`, {
                                                        method: 'DELETE',
                                                        headers: {
                                                            'Content-Type': 'application/x-www-form-urlencoded'
                                                        }
                                                    })
                                                    .then(response => {
                                                        if (response.ok) {
                                                            location.reload(); // or handle this differently, maybe remove the image element from the page
                                                        } else {
                                                            alert('Failed to delete image');
                                                        }
                                                    })
                                                    .catch(error => {
                                                        console.error('There was an error!', error);
                                                    });
                                                }

                                                function setAsLogo(blogId, imageName) {
                                                    fetch(`/api/blog/${blogId}/image/${imageName}/set`, {
                                                        method: 'PUT'
                                                    })
                                                    .then(response => {
                                                        if (response.ok) {
                                                            location.reload();
                                                        } else {
                                                            alert('Failed to set logo');
                                                        }
                                                    })
                                                    .catch(error => {
                                                        console.error('There was an error!', error);
                                                    });
                                                }
                                            </script>
                                            <!DOCTYPE html>
                                            <html lang=""en"">
                                                <head>
                                                    <a href="/api/blog/blogs/attachments/{{
                                                        (page > 1 ? page - 1 : 1)
                                                    }}">Previous</a>
                                                    <a href="/api/blog/blogs/attachments/{{
                                                        page + 1
                                                    }}">Next</a>
                                                </head>
                                                <body>
                                                    <h1>Sparkify</h1>
                                                    <body style=\"background: rgb(43, 42, 51); color: #333;\">{{
                                                        sb
                                                    }}</body>
                                                </body>
                                            </html>
                                            """;

                        await context.Response.WriteAsync(htmlContent);
                    }
                    catch (Exception e)
                    {
                        await context.Response.WriteAsync(e.Message);
                    }
                })
            .AddEndpointFilter(static async (context, next) =>
            {
                var ipAddress = context.HttpContext.Request.HttpContext.Connection.RemoteIpAddress;
                // if not local loopback, then return 404
                if (ipAddress is null || !IPAddress.IsLoopback(ipAddress))
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.HttpContext.Response.WriteAsync("Forbidden");
                }
                return await next(context);
            });

        routeGroup.MapGet("/blogs",
                static async Task<string> () =>
                {
                    try
                    {
                        using var session = DbManager.Store.OpenAsyncSession();
                        var blogs = await session.Advanced.AsyncDocumentQuery<Blog>()
                            .SelectFields<string>(nameof(Blog.Link))
                            .ToArrayAsync();
                        return string.Join("\r\n", blogs);
                    }
                    catch (Exception e)
                    {
                        return e.Message;
                    }
                })
            // .Produces<string>(contentType: "text/plain")
            .AddEndpointFilter(static async (context, next) =>
            {
                var ipAddress = context.HttpContext.Request.HttpContext.Connection.RemoteIpAddress;
                // if not local loopback, then return 404
                if (ipAddress is null || !IPAddress.IsLoopback(ipAddress))
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.HttpContext.Response.WriteAsync("Forbidden");
                }
                return await next(context);
            });

        return routeGroup;
    }

    private static IEnumerable<string> GetSuggestions(Dictionary<string, SuggestionResult> result)
    {
        if (result.TryGetValue(TitleFieldName, out var suggestionResult) && suggestionResult.Suggestions.Count is not 0)
        {
            foreach (var suggestion in suggestionResult.Suggestions)
            {
                yield return suggestion;
            }
        }
    }

    public static byte[] Svg2Png(byte[] svgArray)
    {
        using (var svg = new Svg.Skia.SKSvg())
        using (var svgStream = new MemoryStream(svgArray))
        {
            if (svg.Load(svgStream) is not null)
            {
                using (var stream = new MemoryStream())
                {
                    svg.Picture.ToImage(stream,
                        SKColors.Empty,
                        SKEncodedImageFormat.Png,
                        100,
                        1f,
                        1f,
                        SKImageInfo.PlatformColorType,
                        SKAlphaType.Unpremul,
                        SKColorSpace.CreateRgb(SKColorSpaceTransferFn.Srgb, SKColorSpaceXyz.Srgb));
                    return stream.ToArray();
                }
            }
            else
            {
                throw new Exception("Failed to convert to png");
            }
        }
    }

    private static string? ModifyHighlight(ReadOnlySpan<char> inputSpan, ReadOnlySpan<char> querySpan)
    {
        if (inputSpan.IsEmpty || querySpan.IsEmpty)
        {
            return null;
        }

        var result = new StringBuilder();
        var segments = inputSpan.ToString().Split(StartDelimiter);
        foreach (var segment in segments)
        {
            if (segment.Contains(EndDelimiter, StringComparison.OrdinalIgnoreCase))
            {
                var parts = segment.Split(EndDelimiter, StringSplitOptions.RemoveEmptyEntries);
                var highlighted = parts.First().AsSpan();
                var index = 0;
                var res = StringExtensions.HighlightMatches(highlighted.ToString(), querySpan.ToString());
                // compare two strings, continue to iterate until characters don't match, ignore case and non-alphanumeric characters
                // while (index < querySpan.Length)
                // {
                //     if (querySpan[index] == highlighted[index])
                //     {
                //         index++;
                //     }
                //     else if (querySpan[index] == highlighted[index] && char.IsPunctuation(highlighted[index]))
                //     {
                //         index++;
                //     }
                //     else
                //     {
                //         break;
                //     }
                // }
                // result.Append(StartTag)
                //     .Append(highlighted[..index])
                //     .Append(EndTag)
                //     .Append(highlighted[index..]);
                result.Append(res);
                if (parts.Length > 1)
                {
                    result.Append(parts[1]);
                }
            }
            else
            {
                result.Append(segment);
            }
        }
        return result.ToString();
    }

    private static string? ModifyContentHighlight(ReadOnlySpan<char> inputSpan, ReadOnlySpan<char> querySpan)
    {
        if (inputSpan.IsEmpty || querySpan.IsEmpty)
        {
            return null;
        }
        var resultRawLength = 0;
        var result = new StringBuilder();
        result.Append("<em>\"...");
        var segments = inputSpan.ToString().Split(StartDelimiter, StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (resultRawLength > 360)
            {
                break;
            }
            if (segment.Contains(EndDelimiter, StringComparison.OrdinalIgnoreCase))
            {
                var parts = segment.Split(EndDelimiter, StringSplitOptions.RemoveEmptyEntries);
                var highlighted = parts.First().Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ').AsSpan();
                var index = 0;
                while (index < highlighted.Length)
                {
                    if (querySpan.IndexOf(highlighted[..(index + 1)], StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        index++;
                    }
                    else
                    {
                        break;
                    }
                }
                result.Append(StartTag)
                    .Append(highlighted[..index])
                    .Append(EndTag)
                    .Append(highlighted[index..]);
                resultRawLength += highlighted.Length;
                if (parts.Length > 1)
                {
                    if (char.IsAsciiLetter(parts[1][0]))
                    {
                        result.Append(Ellipsis);
                        result.Append(parts[1].TrimStart());
                        resultRawLength += Ellipsis.Length + parts[1].Length;
                    }
                    else
                    {
                        result.Append(parts[1]);
                        resultRawLength += parts[1].Length;
                    }
                }
            }
            else
            {
                result.Append(segment);
                resultRawLength += segment.Length;
            }
        }
        result.Append("...\"</em>");
        return result.ToString();
    }

    private static bool CleanQueryAndAddWildcard(ref string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        StringBuilder sb = new(query.Length);
        var i = 0;

        while (i < query.Length && char.IsWhiteSpace(query[i]))
        {
            ++i;
        }

        while (i < query.Length)
        {
            if (i < query.Length &&
                (char.IsAsciiLetterOrDigit(query[i]) || char.IsPunctuation(query[i])) &&
                (i == 0 || !char.IsAsciiLetterOrDigit(query[i - 1])))
            {
                var start = i;
                while (i < query.Length && (char.IsAsciiLetterOrDigit(query[i]) || char.IsPunctuation(query[i])))
                {
                    ++i;
                }
                if (!StringExtensions.StopWords.Contains(query.AsSpan(start, i - start)))
                {
                    sb.Append(query, start, i - start);
                    if (i < query.Length)
                    {
                        sb.Append(' ');
                    }
                }
            }
            else if (char.IsWhiteSpace(query[i]))
            {
                while (i + 1 < query.Length && char.IsWhiteSpace(query[i + 1]))
                {
                    ++i;
                }
                ++i;
            }
            else
            {
                if (sb.Length is not 0 && !char.IsWhiteSpace(sb[^1]))
                {
                    sb.Append(' ');
                }
                ++i;
            }
        }

        if (sb.Length is 0)
        {
            return false;
        }

        while (char.IsWhiteSpace(sb[^1]))
        {
            sb.Length--;
        }
        query = sb.Append('*').ToString();

        return sb.Length is not 0;
    }

    private static void CleanQuery(string query, out string queryCleaned)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            queryCleaned = string.Empty;
            return;
        }

        StringBuilder sb = new(query.Length);

        var i = 0;

        // Trim leading whitespace
        while (i < query.Length && char.IsWhiteSpace(query[i]))
        {
            ++i;
        }

        // include alphanumeric and whitespace characters
        while (i < query.Length)
        {
            if (char.IsWhiteSpace(query[i]))
            {
                while (i + 1 < query.Length && char.IsWhiteSpace(query[i + 1]))
                {
                    ++i;
                }
                sb.Append(' ');
            }
            else
            {
                sb.Append(query[i]);
            }
            ++i;
        }

        if (sb.Length is 0)
        {
            queryCleaned = string.Empty;
            return;
        }

        while (char.IsWhiteSpace(sb[^1]))
        {
            sb.Length--;
        }
        queryCleaned = sb.ToString();
    }
}
