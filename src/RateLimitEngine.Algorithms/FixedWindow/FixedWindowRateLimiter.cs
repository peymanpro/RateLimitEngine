using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;

namespace RateLimitEngine.Algorithms.FixedWindow;

internal sealed class FixedWindowRateLimiter : IRateLimiter
{
    private readonly IFixedWindowStore _store;

    public FixedWindowRateLimiter(IFixedWindowStore store)
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

        var result = await _store.IncrementAsync(
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
                : result.ResetAfter);
    }
}
