using Sparkify.Features.OmniLog;

namespace Sparkify;

public class RequestMiddleware : IMiddleware
{
    private readonly IOmniLog _omniLog;

    public RequestMiddleware(IOmniLog dbContext)
    {
        _omniLog = dbContext;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        _omniLog.Output(context.Request.QueryString.ToString());
        await next(context);
    }
}