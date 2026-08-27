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
    private readonly IClock _clock;
    private readonly RateLimiterOptions _options;

    public InMemoryRateLimiterFactory(
        IClock clock,
        RateLimiterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(clock);

        _clock = clock;
        _options = options ?? new RateLimiterOptions();

        if (!_options.Validate())
        {
            throw new ArgumentException(
                "Invalid rate limiter options.",
                nameof(options));
        }
    }

    public IRateLimiter Create(
        RateLimitAlgorithm algorithm)
    {
        return algorithm switch
        {
            RateLimitAlgorithm.FixedWindow =>
                new FixedWindowRateLimiter(
                    new InMemoryFixedWindowStore(_clock)),

            RateLimitAlgorithm.SlidingWindow =>
                new SlidingWindowRateLimiter(
                    new InMemorySlidingWindowStore(_clock)),

            RateLimitAlgorithm.TokenBucket =>
                new TokenBucketRateLimiter(
                    new InMemoryTokenBucketStore(_clock),
                    new TokenBucketOptions(
                        _options.TokenBucketCapacity)),

            RateLimitAlgorithm.Gcra =>
                new GcraRateLimiter(
                    new InMemoryGcraStore(_clock)),

            _ => throw new ArgumentOutOfRangeException(
                nameof(algorithm),
                algorithm,
                "Unsupported rate limiting algorithm.")
        };
    }
}

