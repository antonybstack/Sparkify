using System.Net;
using System.Xml.XPath;
using HtmlAgilityPack;
using SkiaSharp;

namespace Sparkify.Features.BlogFeatures;

internal sealed class FaviconHttpClient(HttpClient httpClient)
{
    private static readonly HtmlWeb _htmlWeb = new();

    internal async IAsyncEnumerable<FaviconPacket> GetFaviconDataStreamPackets(Uri uri)
    {
        var faviconUrls = await ExtractAllFaviconNodes(uri);
        if (faviconUrls.Count is 0)
        {
            yield break;
        }

        var tasks = faviconUrls.Select(GetFaviconData).ToList();

        while (tasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(tasks);
            tasks.Remove(completedTask);

            var result = await completedTask;
            if (result.HasValue)
            {
                yield return result.Value;
            }
        }
    }

    private async Task<FaviconPacket?> GetFaviconData(Uri faviconUrl)
    {
        try
        {
            var stream = await httpClient.GetByteArrayAsync(faviconUrl);
            var image = SKImage.FromBitmap(SKBitmap.Decode(stream));
            var ms = new MemoryStream();
            image.Encode(SKEncodedImageFormat.Png, 100).SaveTo(ms);
            ms.Position = 0;
            return new FaviconPacket(
                $"{Ulid.NewUlid()}.png",
                image.Width * image.Height,
                ms);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<ICollection<Uri>> ExtractAllFaviconNodes(Uri domain)
    {
        try
        {
            var document = await _htmlWeb.LoadFromWebAsync(domain.AbsoluteUri);
            var headerNode = document.DocumentNode.SelectSingleNode(_xPathExpression);
            if (headerNode is null)
            {
                return Array.Empty<Uri>();
            }
            var uniqueUrls = new HashSet<Uri>();
            foreach (var expre in _faviconExpressions)
            {
                string[]? nodes = headerNode.SelectNodes(expre)
                    ?
                    .Select(static x => WebUtility.HtmlDecode(x.Attributes["href"]?.Value ?? x.Attributes["src"]?.Value))
                    .OfType<string>()
                    .Distinct()
                    .ToArray();

                if (nodes is null || nodes.Length is 0)
                {
                    continue;
                }
                foreach (var node in nodes)
                {
                    uniqueUrls.Add(Uri.IsWellFormedUriString(node, UriKind.Absolute)
                        ? new Uri(node)
                        : new Uri(domain, node));
                }
            }
            return uniqueUrls;
        }
        catch (Exception)
        {
            return Array.Empty<Uri>();
        }
    }

    private static readonly XPathExpression _xPathExpression = XPathExpression.Compile("//head");

    private static readonly string[] _faviconExpressions =
    {
        "//link[translate(@rel,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='icon']",
        "//link[translate(@rel, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='alternate icon']",
        "//link[translate(@rel, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='shortcut icon']",
        "//link[translate(@rel, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='apple-touch-icon']",
        "//link[translate(@rel, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='apple-touch-icon-precomposed']",
        "//meta[translate(@name, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='msapplication-TileImage']",
        "//link[contains(translate(@rel, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), 'icon')]",
        "//link[translate(@type, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='image/svg+xml']",
        "//meta[translate(@itemprop, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='image']",
        "//meta[translate(@property, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='og:image']",
        "//meta[translate(@property, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='twitter:image']",
        "//link[translate(@type, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='image/x-icon']",
        "//link[translate(@type, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='image/png']",
        "//link[contains(translate(@href, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '.ico')]",
        "//img[contains(translate(@src, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), 'logo')]"
    };
}
