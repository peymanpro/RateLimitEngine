using Microsoft.AspNetCore.Http;

namespace RateLimitEngine.AspNetCore;

public interface IRateLimitKeyResolver
{
    string Resolve(HttpContext context);
}
