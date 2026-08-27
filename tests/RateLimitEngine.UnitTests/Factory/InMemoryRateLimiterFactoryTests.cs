using RateLimitEngine.Algorithms;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;

namespace RateLimitEngine.UnitTests.Factory;

public sealed class InMemoryRateLimiterFactoryTests
{
    [Theory]
    [InlineData(RateLimitAlgorithm.FixedWindow)]
    [InlineData(RateLimitAlgorithm.SlidingWindow)]
    [InlineData(RateLimitAlgorithm.TokenBucket)]
    [InlineData(RateLimitAlgorithm.Gcra)]
    public void Create_ShouldReturnRateLimiterForSupportedAlgorithm(
        RateLimitAlgorithm algorithm)
    {
        var clock = new FakeClock(
            new DateTimeOffset(
                2026,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero));

        IRateLimiterFactory factory =
            new InMemoryRateLimiterFactory(clock);

        var limiter = factory.Create(algorithm);

        Assert.NotNull(limiter);
        Assert.IsAssignableFrom<IRateLimiter>(limiter);
    }

    [Fact]
    public void Create_ShouldCreateIndependentLimiterInstances()
    {
        var clock = new FakeClock(
            new DateTimeOffset(
                2026,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero));

        var factory =
            new InMemoryRateLimiterFactory(clock);

        var first =
            factory.Create(RateLimitAlgorithm.FixedWindow);

        var second =
            factory.Create(RateLimitAlgorithm.FixedWindow);

        Assert.NotSame(first, second);
    }

    [Fact]
    public void Constructor_ShouldRejectInvalidTokenBucketCapacity()
    {
        var clock = new FakeClock(
            new DateTimeOffset(
                2026,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero));

        var options = new RateLimiterOptions
        {
            TokenBucketCapacity = 0
        };

        Assert.Throws<ArgumentException>(
            () => new InMemoryRateLimiterFactory(
                clock,
                options));
    }
}
