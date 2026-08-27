using Microsoft.AspNetCore.Http;

namespace RateLimitEngine.AspNetCore;

public sealed class RemoteIpRateLimitKeyResolver : IRateLimitKeyResolver
{
    public string Resolve(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
    }
}
