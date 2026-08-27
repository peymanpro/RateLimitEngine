using RateLimitEngine.Algorithms.FixedWindow;
using RateLimitEngine.Algorithms.Gcra;
using RateLimitEngine.Algorithms.InMemory;
using RateLimitEngine.Algorithms.SlidingWindow;
using RateLimitEngine.Algorithms.TokenBucket;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Time;

namespace RateLimitEngine.Algorithms;

public sealed class InMemoryRateLimiterFactory : IRateLimiterFactory
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
            new FixedWindowRateLimiter(
                new InMemoryFixedWindowStore(clock));

        _slidingWindow =
            new SlidingWindowRateLimiter(
                new InMemorySlidingWindowStore(clock));

        _tokenBucket =
            new TokenBucketRateLimiter(
                new InMemoryTokenBucketStore(clock),
                new TokenBucketOptions(
                    options.TokenBucketCapacity));

        _gcra =
            new GcraRateLimiter(
                new InMemoryGcraStore(clock));
    }

    public IRateLimiter Create(
        RateLimitAlgorithm algorithm)
    {
        return algorithm switch
        {
            RateLimitAlgorithm.FixedWindow => _fixedWindow,
            RateLimitAlgorithm.SlidingWindow => _slidingWindow,
            RateLimitAlgorithm.TokenBucket => _tokenBucket,
            RateLimitAlgorithm.Gcra => _gcra,

            _ => throw new ArgumentOutOfRangeException(
                nameof(algorithm),
                algorithm,
                "Unsupported rate limiting algorithm.")
        };
    }
}

