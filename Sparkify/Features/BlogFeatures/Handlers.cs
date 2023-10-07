using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using Lucene.Net.Analysis.Core;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Net.Http.Headers;
using Nager.PublicSuffix;
using Raven.Client.Documents;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Queries.Highlighting;
using Raven.Client.Documents.Queries.Suggestions;
using Raven.Client.Documents.Session;
using Shared;

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
    private const string StartTag = "<b style=\"background:rgba(0, 180, 145, 0.5); color: rgba(255, 255, 255, 0.8);\">";
    private const string EndTag = "</b>";
    private const string StartDelimiter = "ßßßßß";
    private const string EndDelimiter = "ΩΩΩΩΩ";
    private const string Ellipsis = "...";
    private static readonly Expression<Func<Article, string>> _titleSelectorString = static x => x.Title;
    private static readonly Expression<Func<Article, object>> _titleSelectorObject = static x => x.Title;
    private static readonly Expression<Func<Article, string>> _contentSelectorString = static x => x.Content;
    private static readonly Expression<Func<Article, object>> _contentSelectorObject = static x => x.Content;
    private static readonly string[] _fields =
    {
        nameof(ArticleDto.Id),
        nameof(ArticleDto.BlogId),
        nameof(ArticleDto.Title),
        nameof(ArticleDto.Content),
        nameof(ArticleDto.Categories),
        nameof(ArticleDto.Authors),
        nameof(ArticleDto.Link),
        nameof(ArticleDto.Date)
    };
    private static readonly string[] _fieldsNoTitle = { nameof(ArticleDto.Id), nameof(ArticleDto.Link), nameof(ArticleDto.Date) };
    private static readonly IEnumerable<SuggestionWithTerm> _suggestions = new[] { new SuggestionWithTerm(TitleFieldName) { Options = new SuggestionOptions { Accuracy = 0.2f, PageSize = 3, Distance = StringDistanceTypes.Levenshtein, SortMode = SuggestionSortMode.None } }, new SuggestionWithTerm(TitleFieldName) { Options = new SuggestionOptions { Accuracy = 0.2f, PageSize = 3, Distance = StringDistanceTypes.JaroWinkler, SortMode = SuggestionSortMode.None } }, new SuggestionWithTerm(TitleFieldName) { Options = new SuggestionOptions { Accuracy = 0.2f, PageSize = 3, Distance = StringDistanceTypes.NGram, SortMode = SuggestionSortMode.None } } };
    private static readonly HighlightingOptions _tagsToUse = new() { PreTags = new[] { StartDelimiter }, PostTags = new[] { EndDelimiter } };

    public static IEndpointConventionBuilder MapBlogsApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var routeGroup = endpoints.MapGroup("api/blog");

        routeGroup.MapGet("/search",
            static async Task<Results<Ok<Payload<IEnumerable<ArticleDto>>>, NotFound, ProblemHttpResult>> (HttpContext context, string query) =>
            {
                long startTime = Stopwatch.GetTimestamp();
                // context.Response.Headers.CacheControl = $"public,max-age={TimeSpan.FromSeconds(60).TotalSeconds.ToString(CultureInfo.InvariantCulture)}";

                using var session = DbManager.Store.OpenAsyncSession();

                QueryStatistics? stats;
                ArticleDto[] articles;
                if (!CleanQueryAndAddWildcard(ref query))
                {
                    IQueryable<ArticleDto>? projectionQuery = from article in session.Query<Article>().Statistics(out stats)
                        let blog = RavenQuery.Load<Blog>(article.BlogId)
                        let contentShort = RavenQuery.Raw(article.Content, "substr(0,480)")
                        let metadata = RavenQuery.Raw<string>("blog['@metadata']['logo']")
                        orderby article.Date descending
                        select new ArticleDto
                        {
                            Id = article.Id,
                            BlogId = article.BlogId,
                            Link = article.Link,
                            Authors = article.Authors,
                            Title = article.Title,
                            Date = article.Date,
                            Categories = article.Categories,
                            Content = contentShort,
                            Logo = metadata,
                            Company = blog.Title
                        };

                    articles = await projectionQuery
                        .Take(6)
                        .ToArrayAsync();
                }
                else
                {
                    string queryWithoutWildCard = string.Create(
                        query.Length - 1,
                        query,
                        static (span, original) => original.AsSpan()[..^1].CopyTo(span));
                    Console.WriteLine($"'{query}'");
                    Console.WriteLine($"{queryWithoutWildCard}'");
                    Console.WriteLine("-----");
                    // custom highlight with RQL https://ravendb.net/docs/article-page/6.0/csharp/client-api/session/querying/text-search/highlight-query-results#highlight---customize-tags
                    // can also stream results: https://ravendb.net/docs/article-page/6.0/Csharp/client-api/session/querying/how-to-stream-query-results#stream-related-documents

                    IAsyncDocumentQuery<Article>? documentQuery;
                    /*
                     You could optimize by aggregating all fields into one field and then searching that field,
                     and include separaters between the fields so that you can highlight the field that matched.
                     */

                    if (query.Contains(' ', StringComparison.OrdinalIgnoreCase))
                    {
                        documentQuery = session.Advanced.AsyncDocumentQuery<Article>()
                            // Title all words && content all words (proximity)
                            .OpenSubclause()
                            .Search(_titleSelectorString, queryWithoutWildCard, SearchOperator.And)
                            .AndAlso()
                            .Search(_contentSelectorString, queryWithoutWildCard)
                            .Proximity(18)
                            .CloseSubclause()
                            .Boost(100M)
                            // Title all words (*) && Content all words (proximity)
                            .OrElse()
                            .OpenSubclause()
                            .Search(_titleSelectorString, query, SearchOperator.And)
                            .AndAlso()
                            .Search(_contentSelectorString, queryWithoutWildCard)
                            .Proximity(18)
                            .CloseSubclause()
                            .Boost(80M)
                            // Title all words (*) || Content all words (proximity)
                            .OrElse()
                            .OpenSubclause()
                            .Search(_titleSelectorString, query, SearchOperator.And)
                            .OrElse()
                            .Search(_contentSelectorString, queryWithoutWildCard)
                            .Proximity(18)
                            .CloseSubclause()
                            .Boost(60M)
                            // Title any words (anywhere) || content any words (anywhere)
                            .OrElse()
                            .OpenSubclause()
                            .Search(_titleSelectorString, query, SearchOperator.Or)
                            .OrElse()
                            .Search(_contentSelectorString, queryWithoutWildCard)
                            .Proximity(18)
                            .CloseSubclause()
                            .Boost(40M)
                            // Content all words (anywhere)
                            .OrElse()
                            .Search(_contentSelectorString, queryWithoutWildCard, SearchOperator.And)
                            .Boost(30M);
                    }
                    else
                    {
                        documentQuery = session.Advanced.AsyncDocumentQuery<Article>()
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

                    articles = await (from e in documentQuery.ToQueryable()
                                .Statistics(out stats)
                                .Highlight(static x => x.Title, 200, 1, _tagsToUse, out var titleHighlights)
                                .Highlight(static x => x.Content, 100, 10, _tagsToUse, out var contentHighlights)
                            let blog = RavenQuery.Load<Blog>(e.BlogId)
                            let contentShort = RavenQuery.Raw(e.Content, "substr(0,480)")
                            let metadata = RavenQuery.Raw<string>("blog['@metadata']['logo']")
                            select new ArticleDto
                            {
                                Id = e.Id,
                                BlogId = e.BlogId,
                                Link = e.Link,
                                Authors = e.Authors,
                                Title = e.Title,
                                Date = e.Date,
                                Categories = e.Categories,
                                Content = contentShort,
                                Logo = metadata,
                                Company = blog.Title
                            })
                        .Take(6)
                        .ToArrayAsync();

                    foreach (var article in articles)
                    {
                        if (titleHighlights.GetFragments(article.Id).Length is not 0)
                        {
                            article.Title = ModifyHighlight(titleHighlights.GetFragments(article.Id).FirstOrDefault(), queryWithoutWildCard) ?? article.Title;
                        }
                        if (contentHighlights.GetFragments(article.Id).Length is not 0)
                        {
                            article.Content = ModifyContentHighlight(
                                string.Join(Ellipsis,
                                    contentHighlights.GetFragments(article.Id)
                                        .Select(static s => s.Trim())),
                                queryWithoutWildCard);
                        }
                    }
                }

                if (articles.Length is 0)
                {
                    var suggestionsTasks = _suggestions
                        .Select(request =>
                        {
                            request.Term = query;
                            return session.Advanced
                                .AsyncDocumentQuery<Article>()
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
                        foreach (string? suggestion in suggestionResult.Suggestions)
                        {
                            freqMap.TryGetValue(suggestion, out int count);
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

                    articles = new ArticleDto[minHeap.Count];
                    int i = 0;
                    while (minHeap.TryDequeue(out var suggestion, out _))
                    {
                        articles[i++] = new ArticleDto { Title = suggestion.suggestion };
                    }
                }
                return TypedResults.Ok(
                    new Payload<IEnumerable<ArticleDto>> { Data = articles, stats = new RequestStatistics { DurationInMs = Stopwatch.GetElapsedTime(startTime).Milliseconds, TotalResults = stats.TotalResults } }
                );
            });

        routeGroup.MapGet("blogs/{id}/image/{name}",
            static async Task<Results<FileStreamHttpResult, NotFound, ProblemHttpResult>> (HttpContext context, string id, string name) =>
            {
                // set cache for 1 day
                using var session = DbManager.Store.OpenAsyncSession();

                var attachment = await session.Advanced.Attachments.GetAsync($"blogs/{id}", name);
                if (attachment is null)
                {
                    return TypedResults.NotFound();
                }
                context.Response.Headers.CacheControl = $"public,max-age={TimeSpan.FromDays(30).TotalSeconds.ToString(CultureInfo.InvariantCulture)}";
                // context.Response.Headers.CacheControl = $"public,max-age={TimeSpan.FromSeconds(1).TotalSeconds.ToString(CultureInfo.InvariantCulture)}";
                var entityTag = new EntityTagHeaderValue($"\"{attachment.Details.ChangeVector}\"");
                return TypedResults.File(attachment.Stream,
                    attachment.Details.ContentType,
                    attachment.Details.Name,
                    entityTag: entityTag);
            });

        routeGroup.MapPost("/",
                static async Task<Results<NoContent, ProblemHttpResult>> ([FromServices] FaviconHttpClient faviconHttpClient,
                    [BindRequired, FromBody] List<string> links) =>
                {
                    DomainParser domainParser = new(new WebTldRuleProvider());
                    try
                    {
                        using var session = DbManager.Store.OpenAsyncSession();
                        string[]? existingBlogLinks = await session.Advanced.AsyncDocumentQuery<Blog>()
                            .WhereIn(static x => x.Link, links)
                            .SelectFields<string>(nameof(Blog.Link))
                            .ToArrayAsync();
                        Dictionary<Blog, Collection<FaviconPacket>> blogStreamMap = new();
                        // links.RemoveAll(x => existingBlogLinks.Contains(x));
                        foreach (string link in links)
                        {
                            var domainInfo = domainParser.Parse(link);
                            if (!domainParser.IsValidDomain(domainInfo.RegistrableDomain))
                            {
                                continue;
                            }
                            string? company = !string.IsNullOrWhiteSpace(domainInfo.Domain)
                                ? domainInfo.Domain.ToTitleCase()
                                : null;
                            Blog blog = new() { Company = company, Link = link };
                            await session.StoreAsync(blog);
                            var baseDomainUri = new UriBuilder("https", domainInfo.RegistrableDomain, 443).Uri;
                            await foreach (var packet in faviconHttpClient.GetFaviconDataStreamPackets(baseDomainUri))
                            {
                                blogStreamMap.TryAdd(blog, new Collection<FaviconPacket>());
                                blogStreamMap[blog].Add(packet);
                            }
                        }
                        foreach (var (blog, packets) in blogStreamMap)
                        {
                            foreach (var packet in packets)
                            {
                                session.Advanced.Attachments.Store(blog, packet.Name, packet.Stream, "image/png");
                            }
                            session.Advanced.GetMetadataFor(blog)["logo"] = packets.MaxBy(static x => x.Size).Name;
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
            .Produces<NoContent>()
            .ProducesValidationProblem();

        return routeGroup.WithOpenApi();
    }

    private static IEnumerable<string> GetSuggestions(Dictionary<string, SuggestionResult> result)
    {
        if (result.TryGetValue(TitleFieldName, out var suggestionResult) && suggestionResult.Suggestions.Count is not 0)
        {
            foreach (string? suggestion in suggestionResult.Suggestions)
            {
                yield return suggestion;
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
        string[] segments = inputSpan.ToString().Split(StartDelimiter);
        foreach (string segment in segments)
        {
            if (segment.Contains(EndDelimiter, StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = segment.Split(EndDelimiter, StringSplitOptions.RemoveEmptyEntries);
                var highlighted = parts.First().AsSpan();
                int index = 0;
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
        int resultRawLength = 0;
        var result = new StringBuilder();
        string[] segments = inputSpan.ToString().Split(StartDelimiter);
        foreach (string segment in segments)
        {
            if (resultRawLength > 480)
            {
                break;
            }
            if (segment.Contains(EndDelimiter, StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = segment.Split(EndDelimiter, StringSplitOptions.RemoveEmptyEntries);
                var highlighted = parts.First().AsSpan();
                int index = 0;
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
        return result.ToString();
    }

    private static bool CleanQueryAndAddWildcard(ref string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        StringBuilder sb = new(query.Length);
        int i = 0;

        // Trim leading whitespace
        while (i < query.Length && char.IsWhiteSpace(query[i]))
        {
            ++i;
        }

        while (i < query.Length)
        {
            if (IsWordStart(query, i))
            {
                int start = i;
                while (i < query.Length && char.IsAsciiLetterOrDigit(query[i]))
                {
                    ++i;
                }
                if (!IsStopWord(query, start, i))
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
                sb.Append(' ');
                ++i;
            }
        }

        if (sb.Length is 0)
        {
            return false;
        }

        if (char.IsWhiteSpace(sb[^1]))
        {
            sb.Length = sb.Length - 1;
        }
        query = sb.Append('*').ToString();

        return sb.Length is not 0;
    }

    private static bool IsWordStart(string str, int index) =>
        index < str.Length && char.IsAsciiLetterOrDigit(str[index]) && (index == 0 || char.IsWhiteSpace(str[index - 1]));

    private static bool IsStopWord(string str, int start, int end)
    {
        string word = str.Substring(start, end - start).ToLowerInvariant();
        return StopAnalyzer.ENGLISH_STOP_WORDS_SET.Contains(word);
    }

    // private static bool CleanQueryAndAddWildcard(ref string query)
    // {
    //     if (string.IsNullOrWhiteSpace(query))
    //     {
    //         return false;
    //     }
    //
    //     StringBuilder sb = new(query.Length);
    //     // trim leading whitespace
    //     int i = 0;
    //     while (char.IsWhiteSpace(query[i]))
    //     {
    //         ++i;
    //     }
    //
    //     // include alphanumeric and whitespace characters
    //     while (i < query.Length)
    //     {
    //         if (char.IsAsciiLetterOrDigit(query[i]))
    //         {
    //             sb.Append(query[i]);
    //         }
    //         else if (char.IsWhiteSpace(query[i]))
    //         {
    //             sb.Append(query[i]);
    //             while (i + 1 < query.Length && char.IsWhiteSpace(query[i + 1]))
    //             {
    //                 ++i;
    //             }
    //         }
    //         else
    //         {
    //             sb.Append(' ');
    //         }
    //         ++i;
    //     }
    //     if (sb.Length is 0)
    //     {
    //         return false;
    //     }
    //     // sb.Append(query);
    //     if (char.IsWhiteSpace(sb[^1]))
    //     {
    //         int len = sb.Length;
    //         while (len is not 0 && char.IsWhiteSpace(sb[len - 1]))
    //         {
    //             len--;
    //         }
    //         sb.Length = len;
    //     }
    //
    //     query = sb.Append('*').ToString();
    //
    //     return sb.Length is not 0;
    // }
}

/*
from 'Articles' where search(Title, $p0, and) and search(Title, $p1, and) or boost(proximity(search(Content, $p2), 12), 10) select id() as Id, BlogId, Title, Content, Categories, Authors, Link, Date include highlight(Title,200,1,$p3),highlight(Content,200,1,$p4)
*/
// var rawRql = await session.Advanced.AsyncRawQuery<ArticleDto>("""
//                                                                    from Articles
//                                                                   where search(Title, $query) or search(Content, $query)
//                                                                   select Title, Content, Link, Authors
//                                                               """)
//     .AddParameter("query", query)
//     .AddParameter("p0", _tagsToUse)
//     // .Projection()
//     .Take(10)
//     .ToArrayAsync();
//
// Article[] rawRql1 = await session.Advanced.AsyncRawQuery<ArticleDto>("""
//                                                                          from Articles
//                                                                          where boost(proximity(search(Content, $query), 12), 10)
//                                                                          select Title, Content, Link
//                                                                      """)
//     .AddParameter("query", queryWithoutWildCard)
//     .Take(10)
//     .ToArrayAsync();

// articles = await documentQuery
//     // .ToAsyncDocumentQuery()
//     .Statistics(out stats)
//     .Highlight(x => x.Title, 200, 1, _tagsToUse, out var titleHighlights)
//     .Highlight(x => x.Content, 200, 1, _tagsToUse, out var contentHighlights);
// let blog = RavenQuery.Load<Blog>(article.BlogId)
// let contentShort = RavenQuery.Raw(article.Content, "substr(0,450)")
// let metadata = RavenQuery.Raw<string>("blog['@metadata']['logo']")
// select new ArticleDto
// {
//     Id = article.Id,
//     Title = article.Title,
//     BlogId = article.BlogId,
//     Link = article.Link,
//     Authors = article.Authors,
//     Date = article.Date,
//     Categories = article.Categories,
//     Content = contentShort,
//     Logo = metadata,
//     Company = blog.Title
// }).ToAsyncDocumentQuery();
// .SelectFields<ArticleDto>(_fields)

/*

                             let blog = RavenQuery.Load<Blog>(article.BlogId)
   let contentShort = RavenQuery.Raw(article.Content, "substr(0,450)")
   let metadata = RavenQuery.Raw<string>("blog['@metadata']['logo']")
   select new ArticleDto
   {
   Id = article.Id,
   Title = article.Title,
   BlogId = article.BlogId,
   Link = article.Link,
   Authors = article.Authors,
   Date = article.Date,
   Categories = article.Categories,
   Content = contentShort,
   Logo = metadata,
   Company = blog.Title
   }).ToAsyncDocumentQuery();
   */

// .IncludeExplanations(out var explanations)
// .SelectFields<ArticleDto>(_fields)
// .Highlight(_titleSelectorObject, 400, 20, _tagsToUse, out var titleHighlights)
// .Highlight(_contentSelectorObject, 400, 20, _tagsToUse, out var contentHighlights);

// documentQuery
//     .Search(_titleSelectorObject, queryWithoutWildCard, 100, SearchOptions.And, SearchOperator.And)
//     .Highlight(_titleSelectorObject, 250, 20, _tagsToUse, out var titleHighlights)
//     .Search(_contentSelectorObject, queryWithoutWildCard, 100, SearchOptions.And, SearchOperator.And)
//     .Highlight(_contentSelectorObject, 450, 40, _tagsToUse, out var contentHighlights)
//     .Search(_titleSelectorObject, query, 50, SearchOptions.And, SearchOperator.And)
//     .Search(_contentSelectorObject, query, 50, SearchOptions.And, SearchOperator.And)
//     .Search(_titleSelectorObject, query, 5, SearchOptions.And, SearchOperator.Or)
//     .Search(_contentSelectorObject, query, 5, SearchOptions.And, SearchOperator.Or);
// .ToAsyncDocumentQuery()
// .OrElse()
// .Search(_titleSelectorObject, queryWithoutWildCard)
// .Proximity(8)
// .Boost(100)
// .OrElse()
// .Search(_contentSelectorObject, queryWithoutWildCard)
// .Proximity(8)
// .Boost(100);

/*
    .Search(_titleSelectorObject, queryWithoutWildCard)
   .Proximity(8)
   .Boost(100)

   .Search(_contentSelectorObject, queryWithoutWildCard)
   .Proximity(8)
   .Boost(100)
   */
// session.Advanced.AsyncRawQuery<Article>(
//         "from Articles where search(Title, $query) or search(Content, $query) select Title, Content, Link")
//     .AddParameter("query", query)
//     .Take(10)
//     .ToString();

// get metadata for each article's blog
// string[] blogIds = articles1.Select(static x => x.BlogId).Distinct().ToArray();
// var blogs = session.Advanced.Lazily.LoadAsync<Blog>(blogIds);
//
// // await session.Advanced.Eagerly.ExecuteAllPendingLazyOperationsAsync();
// foreach ((string? id, var blog) in await blogs.Value)
// {
//     var metadata = session.Advanced.GetMetadataFor(blog);
//     if (!metadata.TryGetValue("logo", out string? logo))
//     {
//         continue;
//     }
//     foreach (var article in articles1.Where(x => x.BlogId == id))
//     {
//         article.Logo = logo;
//         article.Company = blog.Title;
//     }
// }

// var test1 = await session.Advanced.Revisions.GetMetadataForAsync("blogs/2389-A");
//
// //https://ravendb.net/docs/article-page/6.0/csharp/client-api/commands/documents/how-to/get-document-metadata-only
// var command = new GetDocumentsCommand("blogs/2389-A", null, true);
// await session.Advanced.RequestExecutor.ExecuteAsync(command, session.Advanced.Context);
// var test2 = (BlittableJsonReaderObject)command.Result.Results[0];
// var documentMetadata = (BlittableJsonReaderObject)test2["@metadata"];
// documentMetadata.TryGet("logo", out string? logo123);
// var metadata1 = await session.Query<Blog>()
//     .Where(x => x.Id == "blogs/2389-A")
//     .Select(x => RavenQuery.Metadata(x))
//     .ToArrayAsync();

// routeGroup.MapGet("/search",
//     static async Task<IEnumerable> (HttpContext context, string query) =>
//     {
//         context.Response.Headers.CacheControl = $"public,max-age={TimeSpan.FromSeconds(60).TotalSeconds.ToString(CultureInfo.InvariantCulture)}";
//
//         using var session = DbManager.Store.OpenAsyncSession();
//
//         if (query.Length is 0)
//         {
//             return await session.Advanced
//                 .AsyncDocumentQuery<Article>()
//                 .SelectFields<Article>(_fields)
//                 .OrderByDescending(x => x.Date)
//                 .Take(10)
//                 .ToArrayAsync();
//         }
//
//         if (!CleanQueryAndAddWildcard(ref query))
//         {
//             return await session.Advanced
//                 .AsyncDocumentQuery<Article>()
//                 .SelectFields<Article>(_fieldsNoTitle)
//                 .OrderByDescending(x => x.Date)
//                 .Take(10)
//                 .ToArrayAsync();
//         }
//
//         string queryWithoutWildCard = string.Create(
//             query.Length - 1,
//             query,
//             static (span, original) => original.AsSpan()[..^1].CopyTo(span));
//
//         //
//         // var rawRql = session.Advanced.AsyncRawQuery<Article>("""
//         //      from Articles
//         //      where search(Title, $query) or search(Content, $query)
//         //      select Title, Content, Link
//         //  """).AddParameter("query", query)
//         //     .Take(10)
//         //     .ToString();
//         // custom highlight with RQL https://ravendb.net/docs/article-page/6.0/csharp/client-api/session/querying/text-search/highlight-query-results#highlight---customize-tags
//         // can also stream results: https://ravendb.net/docs/article-page/6.0/Csharp/client-api/session/querying/how-to-stream-query-results#stream-related-documents
//         var documentQuery = session.Advanced
//             .AsyncDocumentQuery<Article>()
//             .SelectFields<Article>(_fields)
//             .Highlight(_titleSelectorObject, 200, 10, _tagsToUse, out var titleHighlights)
//             .Search(_titleSelectorString, query, SearchOperator.And)
//             .Boost(100);
//
//         if (query.Contains(' ', StringComparison.OrdinalIgnoreCase))
//         {
//             documentQuery
//                 .Search(_titleSelectorString, query, SearchOperator.And)
//                 .Boost(50)
//                 .Search(_titleSelectorString, query)
//                 .Boost(10);
//             documentQuery
//                 .Search(_titleSelectorString, queryWithoutWildCard, SearchOperator.And)
//                 .Proximity(6)
//                 .Boost(100)
//                 .Search(_titleSelectorString, queryWithoutWildCard)
//                 .Proximity(6)
//                 .Boost(20);
//         }
//         else
//         {
//             documentQuery
//                 .Search(_titleSelectorString, queryWithoutWildCard)
//                 .Boost(100)
//                 .Search(_titleSelectorString, query)
//                 .Boost(30);
//         }
//
//         var articles = await documentQuery.Take(10).ToArrayAsync();
//
//         foreach (var article in articles)
//         {
//             article.Title = ModifyHighlight(titleHighlights.GetFragments(article.Id).FirstOrDefault(), query)
//                 .ToString();
//         }
//
//         if (articles.Length is not 0)
//         {
//             return articles;
//         }
//
//         var suggestionsTasks = _suggestions
//             .Select(request =>
//             {
//                 request.Term = query;
//                 return session.Advanced
//                     .AsyncDocumentQuery<Article>()
//                     .SuggestUsing(request)
//                     .ExecuteLazyAsync();
//             })
//             .ToArray();
//
//         var freqMap = new Dictionary<string, int>();
//         foreach (var resultTask in suggestionsTasks)
//         {
//             if (!(await resultTask.Value).TryGetValue(TitleFieldName, out var suggestionResult))
//             {
//                 continue;
//             }
//             foreach (string? suggestion in suggestionResult.Suggestions)
//             {
//                 freqMap.TryGetValue(suggestion, out int count);
//                 freqMap[suggestion] = count + 1;
//             }
//         }
//         var minHeap = new PriorityQueue<(string suggestion, int freq), int>(3);
//         foreach (var entry in freqMap)
//         {
//             if (minHeap.Count < 3)
//             {
//                 minHeap.Enqueue((entry.Key, entry.Value), entry.Value);
//             }
//             else if (entry.Value > minHeap.Peek().freq)
//             {
//                 minHeap.Dequeue();
//                 minHeap.Enqueue((entry.Key, entry.Value), entry.Value);
//             }
//         }
//
//         articles = new Article[3];
//         int i = 0;
//         while (minHeap.TryDequeue(out var suggestion, out _))
//         {
//             articles[i++] = new Article { Title = suggestion.suggestion };
//         }
//         return articles;
//     });
