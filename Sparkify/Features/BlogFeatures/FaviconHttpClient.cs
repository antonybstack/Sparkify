using System.Net;
using System.Xml.XPath;
using HtmlAgilityPack;
using SkiaSharp;

namespace Sparkify.Features.BlogFeatures;

public sealed class FaviconHttpClient(HttpClient httpClient)
{
    private static readonly HtmlWeb HtmlWeb = new();

    public async IAsyncEnumerable<FaviconPacket> GetFaviconDataStreamPackets(Uri uri)
    {
        var faviconUrls = ExtractAllFaviconNodes(uri);
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
            byte[] stream = await httpClient.GetByteArrayAsync(faviconUrl);
            var image = SKImage.FromBitmap(SKBitmap.Decode(stream));
            // if image null, then return
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
            return null; // return null on exception
        }
    }

    // public async Task<ICollection<FaviconPacket>> GetFaviconDataStreamPackets(Uri uri)
    // {
    //     try
    //     {
    //         var faviconUrls = ExtractAllFaviconNodes(uri);
    //         if (faviconUrls.Count is 0)
    //         {
    //             return Array.Empty<FaviconPacket>();
    //         }
    //
    //         var tasks = new List<Task<FaviconPacket?>>();
    //         foreach (var faviconUrl in faviconUrls)
    //         {
    //             tasks.Add(GetFaviconData(faviconUrl));
    //         }
    //
    //         var results = await Task.WhenAll(tasks);
    //         return results.Where(packet => packet.HasValue).Select(packet => packet.Value).ToList();
    //     }
    //     catch (Exception)
    //     {
    //         return Array.Empty<FaviconPacket>();
    //     }
    // }

    // public async Task<ICollection<FaviconPacket>> GetFaviconDataStreamPackets(Uri uri)
    // {
    //     try
    //     {
    //         var faviconUrls = ExtractAllFaviconNodes(uri);
    //         if (faviconUrls.Count is 0)
    //         {
    //             return Array.Empty<FaviconPacket>();
    //         }
    //
    //         var tasks = faviconUrls.Select(async faviconUrl =>
    //         {
    //             try
    //             {
    //                 byte[] stream = await httpClient.GetByteArrayAsync(faviconUrl);
    //                 var image = SKImage.FromBitmap(SKBitmap.Decode(stream));
    //                 // if image null, then return
    //                 var ms = new MemoryStream();
    //                 image.Encode(SKEncodedImageFormat.Png, 100).SaveTo(ms);
    //                 ms.Position = 0;
    //                 return new FaviconPacket(
    //                     $"{Ulid.NewUlid()}.png",
    //                     image.Width * image.Height,
    //                     ms);
    //             }
    //             catch (Exception)
    //             {
    //                 return new FaviconPacket(
    //                     $"{Ulid.NewUlid()}.png",
    //                     0,
    //                     new MemoryStream());
    //             }
    //         });
    //
    //         return await Task.WhenAll(tasks);
    //     }
    //     catch (Exception)
    //     {
    //         return Array.Empty<FaviconPacket>();
    //     }
    // }

    private static ICollection<Uri> ExtractAllFaviconNodes(Uri domain)
    {
        var document = HtmlWeb.Load(domain);
        var headerNode = document.DocumentNode.SelectSingleNode(XPathExpression);
        if (headerNode is null)
        {
            return Array.Empty<Uri>();
        }
        HashSet<Uri> uniqueUrls = new();
        foreach (string expre in FaviconExpressions)
        {
            var nodes = headerNode.SelectNodes(expre)
                ?
                .Select(static x => WebUtility.HtmlDecode(x.Attributes["href"]?.Value))
                .OfType<string>();

            if (nodes is null)
            {
                continue;
            }
            foreach (string node in nodes)
            {
                uniqueUrls.Add(Uri.IsWellFormedUriString(node, UriKind.Absolute)
                    ? new Uri(node)
                    : new Uri(domain, node));
            }
        }
        return uniqueUrls;
    }

    private static readonly XPathExpression XPathExpression = XPathExpression.Compile("//head");

    private static readonly string[] FaviconExpressions =
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
        "//link[contains(translate(@href, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '.ico')]"
    };
}
