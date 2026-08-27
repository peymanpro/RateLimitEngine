using RateLimitEngine.Core.Models;

namespace RateLimitEngine.Core.Abstractions;

public interface IRateLimiterFactoryProvider
{
    IRateLimiterFactory GetFactory(
        RateLimitBackend backend);
}
