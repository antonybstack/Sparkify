using Raven.Client.Documents.Indexes;
using Common;

namespace Sparkify.Indexes;

public sealed class ArticleIndex : AbstractIndexCreationTask<Article, ArticleIndex.Base>
{
    public class Base : IEntity
    {
        public string Id { get; init; }
        public string BlogId { get; init; }
        public string Link { get; set; }
        public string Company { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? Subtitle { get; set; }
        public string LogoName { get; set; }
        public ICollection<string>? Authors { get; set; }
        public ICollection<string>? Categories { get; set; }
        public DateTimeOffset Date { get; set; }
    }

    public sealed class ArticleNoSearchResults : Base
    {
        private new string? Content { get; set; }
    }

    public sealed class ArticleSearchResults : Base
    {
        private new string? Subtitle { get; set; }
        private new string? Content { get; set; }
    }

    public ArticleIndex()
    {
        Map = articles => from a in articles
            let blog = LoadDocument<Blog>(a.BlogId)
            let metadata = MetadataFor(blog)
            let htmlStream = LoadAttachment(a, "content.html").GetContentAsStream()
            select new Base
            {
                Id = a.Id,
                BlogId = a.BlogId,
                Link = a.Link,
                Authors = a.Authors,
                Title = a.Title,
                Date = a.Date,
                Categories = a.Categories,
                Content = htmlStream != null ? HtmlExtensions.HtmlToText(htmlStream) : string.Empty,
                Subtitle = a.Subtitle,
                LogoName = metadata.Value<string>("logo"),
                Company = blog.Title
            };

        // test/benchmark this, was throwing errors before
        // CompoundField(static x => x.Title, static x => x.Content);

        StoreAllFields(FieldStorage.Yes);

        Suggestion(static x => x.Title);
        // Suggestion(static x => x.Content);

        Analyzers.Add(static x => x.Title, "RavenStandardAnalyzer");
        Analyzers.Add(static x => x.Content, "RavenStandardAnalyzer");

        Indexes.Add(static x => x.Title, FieldIndexing.Search);
        Indexes.Add(static x => x.Content, FieldIndexing.Search);

        TermVectors.Add(static x => x.Title, FieldTermVector.WithPositionsAndOffsets);
        TermVectors.Add(static x => x.Content, FieldTermVector.WithPositionsAndOffsets);

        // SearchEngineType = Raven.Client.Documents.Indexes.SearchEngineType.Corax;

        Priority = IndexPriority.High;

        AdditionalAssemblies = new HashSet<AdditionalAssembly>
        {
            AdditionalAssembly.FromNuGet("HtmlAgilityPack",
                "1.11.54",
                usings: new HashSet<string>
                {
                    "System.IO", "System.Text", "System.Net", "System.Text.RegularExpressions", "HtmlAgilityPack"
                })
            // AdditionalAssembly.FromNuGet("NUglify",
            //     "1.21.0",
            //     usings: new HashSet<string>
            //     {
            //         "NUglify", "NUglify.Html"
            //     })
        };

        AdditionalSources = new Dictionary<string, string>
        {
            ["Html.cs"] = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Html.cs"))
        };
    }
    //
    // public static string GetEmbeddedResourceContent(string resourceName, Assembly assembly)
    // {
    //     using var stream = assembly.GetManifestResourceStream(resourceName);
    //     if (stream != null)
    //     {
    //         using var reader = new StreamReader(stream);
    //         return reader.ReadToEnd();
    //     }
    //     return string.Empty;
    // }
}
