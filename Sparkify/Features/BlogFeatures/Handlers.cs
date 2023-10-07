using System.Collections.ObjectModel;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Nager.PublicSuffix;
using Raven.Client.Documents;
using Common;
using SkiaSharp;
using Svg.Skia;

namespace Sparkify.Features.BlogFeatures;

internal static class ApiEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapBlogsApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var routeGroup = endpoints.MapGroup("api/blog");

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

        return routeGroup;
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
}
