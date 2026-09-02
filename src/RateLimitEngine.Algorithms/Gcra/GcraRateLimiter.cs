using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;

namespace RateLimitEngine.Algorithms.Gcra;

internal sealed class GcraRateLimiter : IRateLimiter
{
    private readonly IGcraStore _store;

    public GcraRateLimiter(IGcraStore store)
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

        if (request.Cost > policy.PermitLimit)
        {
            return new RateLimitDecision(
                allowed: false,
                limit: policy.PermitLimit,
                remaining: policy.PermitLimit);
        }

        var interval =
            policy.Window / (double)policy.PermitLimit;

        var burstTolerance =
            interval * (policy.PermitLimit - 1);

        var result = await _store.EvaluateAsync(
            request.Key,
            interval,
            burstTolerance,
            request.Cost,
            cancellationToken);

        return new RateLimitDecision(
            allowed: result.Accepted,
            limit: policy.PermitLimit,
            remaining: result.Remaining,
            retryAfter: result.RetryAfter);
    }
}
