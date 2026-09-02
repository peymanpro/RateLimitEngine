using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;

namespace RateLimitEngine.Algorithms.TokenBucket;

internal sealed class TokenBucketRateLimiter : IRateLimiter
{
    private readonly ITokenBucketStore _store;
    private readonly TokenBucketOptions _options;

    public TokenBucketRateLimiter(
        ITokenBucketStore store,
        TokenBucketOptions options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _options = options;
    }

    public async ValueTask<RateLimitDecision> EvaluateAsync(
        RateLimitRequest request,
        RateLimitPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(policy);

        cancellationToken.ThrowIfCancellationRequested();

        var refillRate =
            policy.PermitLimit / policy.Window.TotalSeconds;

        var result = await _store.ConsumeAsync(
            request.Key,
            _options.Capacity,
            refillRate,
            request.Cost,
            cancellationToken);

        return new RateLimitDecision(
            allowed: result.Accepted,
            limit: policy.PermitLimit,
            remaining: Math.Max(
                0,
                (int)Math.Floor(result.RemainingTokens)),
            retryAfter: result.RetryAfter);
    }
}

