using RateLimitEngine.Algorithms.FixedWindow;
using RateLimitEngine.Algorithms.Gcra;
using RateLimitEngine.Algorithms.SlidingWindow;
using RateLimitEngine.Algorithms.TokenBucket;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;

namespace RateLimitEngine.Redis;

public sealed class RedisRateLimiterFactory : IRateLimiterFactory
{
    private readonly IFixedWindowStore _fixedWindowStore;
    private readonly ISlidingWindowStore _slidingWindowStore;
    private readonly ITokenBucketStore _tokenBucketStore;
    private readonly IGcraStore _gcraStore;
    private readonly TokenBucketOptions _tokenBucketOptions;

    public RedisRateLimiterFactory(
        IFixedWindowStore fixedWindowStore,
        ISlidingWindowStore slidingWindowStore,
        ITokenBucketStore tokenBucketStore,
        IGcraStore gcraStore,
        TokenBucketOptions tokenBucketOptions)
    {
        ArgumentNullException.ThrowIfNull(fixedWindowStore);
        ArgumentNullException.ThrowIfNull(slidingWindowStore);
        ArgumentNullException.ThrowIfNull(tokenBucketStore);
        ArgumentNullException.ThrowIfNull(gcraStore);
        ArgumentNullException.ThrowIfNull(tokenBucketOptions);

        _fixedWindowStore = fixedWindowStore;
        _slidingWindowStore = slidingWindowStore;
        _tokenBucketStore = tokenBucketStore;
        _gcraStore = gcraStore;
        _tokenBucketOptions = tokenBucketOptions;
    }

    public IRateLimiter Create(
        RateLimitAlgorithm algorithm)
    {
        return algorithm switch
        {
            RateLimitAlgorithm.FixedWindow =>
                new FixedWindowRateLimiter(
                    _fixedWindowStore),

            RateLimitAlgorithm.SlidingWindow =>
                new SlidingWindowRateLimiter(
                    _slidingWindowStore),

            RateLimitAlgorithm.TokenBucket =>
                new TokenBucketRateLimiter(
                    _tokenBucketStore,
                    _tokenBucketOptions),

            RateLimitAlgorithm.Gcra =>
                new GcraRateLimiter(
                    _gcraStore),

            _ => throw new ArgumentOutOfRangeException(
                nameof(algorithm),
                algorithm,
                "Unsupported rate limiting algorithm.")
        };
    }
}
