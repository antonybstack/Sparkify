using System.Collections.ObjectModel;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Nager.PublicSuffix;
using Raven.Client.Documents;
using Common;

namespace Sparkify.Features.BlogFeatures;

internal static class ApiEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapBlogsApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var routeGroup = endpoints.MapGroup("api/blog");

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

        return routeGroup;
    }

}
