using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;

namespace RateLimitEngine.Algorithms.SlidingWindow;

internal sealed class SlidingWindowRateLimiter : IRateLimiter
{
    private readonly ISlidingWindowStore _store;

    public SlidingWindowRateLimiter(ISlidingWindowStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async ValueTask<RateLimitDecision> EvaluateAsync(
        RateLimitRequest request,
        RateLimitPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(policy);

        cancellationToken.ThrowIfCancellationRequested();

        var result = await _store.EvaluateAsync(
            request.Key,
            policy.Window,
            policy.PermitLimit,
            request.Cost,
            cancellationToken);

        return new RateLimitDecision(
            allowed: result.Accepted,
            limit: policy.PermitLimit,
            remaining: result.Remaining,
            resetAfter: result.ResetAfter,
            retryAfter: result.Accepted
                ? null
                : result.RetryAfter);
    }
}
