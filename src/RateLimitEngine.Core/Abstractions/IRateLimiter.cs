using RateLimitEngine.Core.Models;

namespace RateLimitEngine.Core.Abstractions;

public interface IRateLimiter
{
    ValueTask<RateLimitDecision> EvaluateAsync(
        RateLimitRequest request,
        RateLimitPolicy policy,
        CancellationToken cancellationToken = default);
}
