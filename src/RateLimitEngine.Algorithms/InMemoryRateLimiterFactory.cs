using RateLimitEngine.Algorithms.FixedWindow;
using RateLimitEngine.Algorithms.Gcra;
using RateLimitEngine.Algorithms.InMemory;
using RateLimitEngine.Algorithms.SlidingWindow;
using RateLimitEngine.Algorithms.TokenBucket;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Observability;
using RateLimitEngine.Core.Time;

namespace RateLimitEngine.Algorithms;

internal sealed class InMemoryRateLimiterFactory : IRateLimiterFactory
{
    private readonly IRateLimiter _fixedWindow;
    private readonly IRateLimiter _slidingWindow;
    private readonly IRateLimiter _tokenBucket;
    private readonly IRateLimiter _gcra;

    public InMemoryRateLimiterFactory(
        IClock clock,
        RateLimiterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(clock);

        options ??= new RateLimiterOptions();

        if (!options.Validate())
        {
            throw new ArgumentException(
                "Invalid rate limiter options.",
                nameof(options));
        }

        _fixedWindow =
            new InstrumentedRateLimiter(
                new FixedWindowRateLimiter(
                    new InMemoryFixedWindowStore(clock)),
                RateLimitAlgorithm.FixedWindow,
                RateLimitBackend.InMemory);

        _slidingWindow =
            new InstrumentedRateLimiter(
                new SlidingWindowRateLimiter(
                    new InMemorySlidingWindowStore(clock)),
                RateLimitAlgorithm.SlidingWindow,
                RateLimitBackend.InMemory);

        _tokenBucket =
            new InstrumentedRateLimiter(
                new TokenBucketRateLimiter(
                    new InMemoryTokenBucketStore(clock),
                    new TokenBucketOptions(
                        options.TokenBucketCapacity)),
                RateLimitAlgorithm.TokenBucket,
                RateLimitBackend.InMemory);

        _gcra =
            new InstrumentedRateLimiter(
                new GcraRateLimiter(
                    new InMemoryGcraStore(clock)),
                RateLimitAlgorithm.Gcra,
                RateLimitBackend.InMemory);
    }

    public IRateLimiter Create(
        RateLimitAlgorithm algorithm)
    {
        return algorithm switch
        {
            RateLimitAlgorithm.FixedWindow =>
                _fixedWindow,

            RateLimitAlgorithm.SlidingWindow =>
                _slidingWindow,

            RateLimitAlgorithm.TokenBucket =>
                _tokenBucket,

            RateLimitAlgorithm.Gcra =>
                _gcra,

            _ => throw new ArgumentOutOfRangeException(
                nameof(algorithm),
                algorithm,
                "Unsupported rate limiting algorithm.")
        };
    }
}
