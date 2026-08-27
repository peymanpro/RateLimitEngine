using RateLimitEngine.Core.Models;

namespace RateLimitEngine.Core.Abstractions;

public interface IRateLimiterFactory
{
    IRateLimiter Create(
        RateLimitAlgorithm algorithm);
}
