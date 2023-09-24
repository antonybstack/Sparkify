using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nager.PublicSuffix;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Queries.Highlighting;
using Raven.Client.Documents.Queries.Suggestions;

namespace Sparkify.Features.BlogFeatures;

internal static class ApiEndpointRouteBuilderExtensions
{
    private const string TitleFieldName = nameof(Article.Title);
    private const string StartTag = "<b style=\"background:rgba(0, 180, 145, 0.5)\">";
    private const string EndTag = "</b>";
    private const string StartDelimiter = "ßßßßß";
    private const string EndDelimiter = "ΩΩΩΩΩ";
    private static readonly Expression<Func<Article, string>> _titleSelectorString = static x => x.Title;
    private static readonly Expression<Func<Article, object>> _titleSelectorObject = static x => x.Title;
    private static readonly string[] _fields =
    {
        nameof(Article.Id), nameof(Article.Title), nameof(Article.Link), nameof(Article.Date)
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
                SortMode = SuggestionSortMode.None
            }
        },
        new SuggestionWithTerm(TitleFieldName)
        {
            Options = new SuggestionOptions
            {
                Accuracy = 0.2f,
                PageSize = 3,
                Distance = StringDistanceTypes.JaroWinkler,
                SortMode = SuggestionSortMode.None
            }
        },
        new SuggestionWithTerm(TitleFieldName)
        {
            Options = new SuggestionOptions
            {
                Accuracy = 0.2f,
                PageSize = 3,
                Distance = StringDistanceTypes.NGram,
                SortMode = SuggestionSortMode.None
            }
        }
    };
    private static readonly HighlightingOptions _tagsToUse = new()
    {
        PreTags = new[] { StartDelimiter }, PostTags = new[] { EndDelimiter }
    };

    public static IEndpointConventionBuilder MapBlogsApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var routeGroup = endpoints.MapGroup("api/blog");
        /* TODO: Features
        Regex Flag
        AND, OR, NOT flags
        Search by author
        Search by tags
         */

        // TODO: limit to 256 chars?
        routeGroup.MapGet("/search",
            static async Task<IEnumerable> (string query) =>
            {
                using var session = DbManager.Store.OpenAsyncSession();
                if (!CleanQueryAndAddWildcard(ref query))
                {
                    return await session.Advanced
                        .AsyncDocumentQuery<Article>()
                        .SelectFields<Article>(_fields)
                        .OrderByDescending(x => x.Date)
                        .Take(10)
                        .ToArrayAsync();
                }

                string queryWithoutWildCard = string.Create(
                    query.Length - 1,
                    query,
                    static (span, original) => original.AsSpan()[..^1].CopyTo(span));

                //
                // var rawRql = session.Advanced.AsyncRawQuery<Article>("""
                //      from Articles
                //      where search(Title, $query) or search(Content, $query)
                //      select Title, Content, Link
                //  """).AddParameter("query", query)
                //     .Take(10)
                //     .ToString();
                // custom highlight with RQL https://ravendb.net/docs/article-page/6.0/csharp/client-api/session/querying/text-search/highlight-query-results#highlight---customize-tags
                // can also stream results: https://ravendb.net/docs/article-page/6.0/Csharp/client-api/session/querying/how-to-stream-query-results#stream-related-documents
                var documentQuery = session.Advanced
                    .AsyncDocumentQuery<Article>()
                    .SelectFields<Article>(_fields)
                    .Highlight(_titleSelectorObject, 200, 10, _tagsToUse, out var titleHighlights)
                    .Search(_titleSelectorString, query, SearchOperator.And)
                    .Boost(100);

                if (query.Contains(' ', StringComparison.OrdinalIgnoreCase))
                {
                    documentQuery
                        .Search(_titleSelectorString, query, SearchOperator.And)
                        .Boost(50)
                        .Search(_titleSelectorString, query)
                        .Boost(10);
                    documentQuery
                        .Search(_titleSelectorString, queryWithoutWildCard, SearchOperator.And)
                        .Proximity(6)
                        .Boost(100)
                        .Search(_titleSelectorString, queryWithoutWildCard)
                        .Proximity(6)
                        .Boost(20);
                }
                else
                {
                    documentQuery
                        .Search(_titleSelectorString, queryWithoutWildCard)
                        .Boost(100)
                        .Search(_titleSelectorString, query)
                        .Boost(30);
                }

                var articles = await documentQuery.Take(10).ToArrayAsync();

                foreach (var article in articles)
                {
                    article.Title = ModifyHighlight(titleHighlights.GetFragments(article.Id).FirstOrDefault(), query)
                        .ToString();
                }

                if (articles.Length is not 0)
                {
                    return articles;
                }

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

                articles = new Article[3];
                int i = 0;
                while (minHeap.TryDequeue(out var suggestion, out _))
                {
                    articles[i++] = new Article { Title = suggestion.suggestion };
                }
                return articles;
            });

        routeGroup.MapPost("/",
                static async Task<Results<NoContent, ProblemHttpResult>> (
                    [FromServices] FaviconHttpClient faviconHttpClient,
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
                        links.RemoveAll(x => existingBlogLinks.Contains(x));
                        foreach (string link in links.Except(existingBlogLinks))
                        {
                            var domainInfo = domainParser.Parse(link);
                            if (!domainParser.IsValidDomain(domainInfo.RegistrableDomain))
                            {
                                continue;
                            }
                            string? company = !string.IsNullOrWhiteSpace(domainInfo.Domain)
                                ? Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(domainInfo.Domain)
                                : null;
                            Blog blog = new() { Company = company, Link = link };
                            await session.StoreAsync(blog);
                            var baseDomainUri = new UriBuilder("https", domainInfo.RegistrableDomain, 443).Uri;
                            foreach (var packet in await faviconHttpClient.GetFaviconDataStreamPackets(baseDomainUri))
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

    private static ReadOnlySpan<char> ModifyHighlight(ReadOnlySpan<char> inputSpan, ReadOnlySpan<char> querySpan)
    {
        if (inputSpan.IsEmpty)
        {
            return null;
        }
        if (querySpan.IsEmpty)
        {
            return inputSpan;
        }
        if (querySpan[^1] is '*')
        {
            querySpan = querySpan[..^1];
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

    private static bool CleanQueryAndAddWildcard(ref string query)
    {
        var sw = new Stopwatch();
        sw.Start();

        if (query.Length is 0)
        {
            return false;
        }

        StringBuilder sb = new(query.Length);
        // trim leading whitespace
        int i = 0;
        while (char.IsWhiteSpace(query[i]))
        {
            ++i;
        }

        // include alphanumeric and whitespace characters
        while (i < query.Length)
        {
            if (char.IsAsciiLetterOrDigit(query[i]))
            {
                sb.Append(query[i]);
            }
            else if (char.IsWhiteSpace(query[i]))
            {
                sb.Append(query[i]);
                while (i + 1 < query.Length && char.IsWhiteSpace(query[i + 1]))
                {
                    ++i;
                }
            }
            ++i;
        }

        if (char.IsWhiteSpace(sb[^1]))
        {
            int len = sb.Length;
            while (sb.Length is not 0 && char.IsWhiteSpace(sb[^1]))
            {
                len--;
            }
            sb.Capacity = len;
        }

        // Trim trailing whitespace
        query = sb.Append('*').ToString();
        if (sb.Length is 0)
        {
            return false;
        }

        sw.Stop();
        Console.WriteLine("Query took {0} ms to process: {1}", sw.ElapsedMilliseconds, query);
        return true;
    }
}
