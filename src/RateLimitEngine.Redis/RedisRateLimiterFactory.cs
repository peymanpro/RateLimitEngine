using RateLimitEngine.Algorithms.FixedWindow;
using RateLimitEngine.Algorithms.Gcra;
using RateLimitEngine.Algorithms.SlidingWindow;
using RateLimitEngine.Algorithms.TokenBucket;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Observability;

namespace RateLimitEngine.Redis;

internal sealed class RedisRateLimiterFactory : IRateLimiterFactory
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
                new InstrumentedRateLimiter(
                    new FixedWindowRateLimiter(
                        _fixedWindowStore),
                    RateLimitAlgorithm.FixedWindow,
                    RateLimitBackend.Redis),

            RateLimitAlgorithm.SlidingWindow =>
                new InstrumentedRateLimiter(
                    new SlidingWindowRateLimiter(
                        _slidingWindowStore),
                    RateLimitAlgorithm.SlidingWindow,
                    RateLimitBackend.Redis),

            RateLimitAlgorithm.TokenBucket =>
                new InstrumentedRateLimiter(
                    new TokenBucketRateLimiter(
                        _tokenBucketStore,
                        _tokenBucketOptions),
                    RateLimitAlgorithm.TokenBucket,
                    RateLimitBackend.Redis),

            RateLimitAlgorithm.Gcra =>
                new InstrumentedRateLimiter(
                    new GcraRateLimiter(
                        _gcraStore),
                    RateLimitAlgorithm.Gcra,
                    RateLimitBackend.Redis),

            _ => throw new ArgumentOutOfRangeException(
                nameof(algorithm),
                algorithm,
                "Unsupported rate limiting algorithm.")
        };
    }
}
