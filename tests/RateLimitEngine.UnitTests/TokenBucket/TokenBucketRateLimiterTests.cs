using RateLimitEngine.Algorithms.TokenBucket;
using RateLimitEngine.Core.Models;

namespace RateLimitEngine.UnitTests.TokenBucket;

public sealed class TokenBucketRateLimiterTests
{
    [Fact]
    public async Task EvaluateAsync_ShouldStartWithFullCapacity()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var options = new TokenBucketOptions(capacity: 5);
        var limiter = new TokenBucketRateLimiter(clock, options);

        var policy = new RateLimitPolicy(
            permitLimit: 5,
            window: TimeSpan.FromSeconds(5));

        var decision = await limiter.EvaluateAsync(
            new RateLimitRequest("client-1"),
            policy);

        Assert.True(decision.Allowed);
        Assert.Equal(5, decision.Limit);
        Assert.Equal(4, decision.Remaining);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldRejectWhenInsufficientTokens()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var options = new TokenBucketOptions(capacity: 2);
        var limiter = new TokenBucketRateLimiter(clock, options);

        var policy = new RateLimitPolicy(
            permitLimit: 2,
            window: TimeSpan.FromSeconds(2));

        await limiter.EvaluateAsync(
            new RateLimitRequest("client-1"),
            policy);

        await limiter.EvaluateAsync(
            new RateLimitRequest("client-1"),
            policy);

        var decision = await limiter.EvaluateAsync(
            new RateLimitRequest("client-1"),
            policy);

        Assert.False(decision.Allowed);
        Assert.Equal(0, decision.Remaining);
        Assert.NotNull(decision.RetryAfter);
        Assert.True(decision.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldRefillTokensOverTime()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var options = new TokenBucketOptions(capacity: 5);
        var limiter = new TokenBucketRateLimiter(clock, options);

        var policy = new RateLimitPolicy(
            permitLimit: 5,
            window: TimeSpan.FromSeconds(2.5));

        await limiter.EvaluateAsync(
            new RateLimitRequest("client-1", cost: 5),
            policy);

        clock.Advance(TimeSpan.FromSeconds(1));

        var decision = await limiter.EvaluateAsync(
            new RateLimitRequest("client-1"),
            policy);

        Assert.True(decision.Allowed);
        Assert.Equal(1, decision.Remaining);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldNeverExceedBucketCapacity()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var options = new TokenBucketOptions(capacity: 5);
        var limiter = new TokenBucketRateLimiter(clock, options);

        var policy = new RateLimitPolicy(
            permitLimit: 5,
            window: TimeSpan.FromMilliseconds(500));

        clock.Advance(TimeSpan.FromSeconds(10));

        var decision = await limiter.EvaluateAsync(
            new RateLimitRequest("client-1"),
            policy);

        Assert.True(decision.Allowed);
        Assert.Equal(4, decision.Remaining);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldRespectRequestCost()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var options = new TokenBucketOptions(capacity: 5);
        var limiter = new TokenBucketRateLimiter(clock, options);

        var policy = new RateLimitPolicy(
            permitLimit: 5,
            window: TimeSpan.FromSeconds(5));

        var decision = await limiter.EvaluateAsync(
            new RateLimitRequest("client-1", cost: 3),
            policy);

        Assert.True(decision.Allowed);
        Assert.Equal(2, decision.Remaining);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldKeepStatesIndependentPerKey()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var options = new TokenBucketOptions(capacity: 1);
        var limiter = new TokenBucketRateLimiter(clock, options);

        var policy = new RateLimitPolicy(
            permitLimit: 1,
            window: TimeSpan.FromSeconds(1));

        var first = await limiter.EvaluateAsync(
            new RateLimitRequest("client-1"),
            policy);

        var second = await limiter.EvaluateAsync(
            new RateLimitRequest("client-2"),
            policy);

        Assert.True(first.Allowed);
        Assert.True(second.Allowed);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldRejectRequestCostGreaterThanCapacity()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var options = new TokenBucketOptions(capacity: 5);
        var limiter = new TokenBucketRateLimiter(clock, options);

        var policy = new RateLimitPolicy(
            permitLimit: 5,
            window: TimeSpan.FromSeconds(5));

        var decision = await limiter.EvaluateAsync(
            new RateLimitRequest("client-1", cost: 6),
            policy);

        Assert.False(decision.Allowed);
        Assert.Equal(5, decision.Remaining);
        Assert.NotNull(decision.RetryAfter);
        Assert.True(decision.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldEnforceCapacityUnderConcurrentRequests()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var options = new TokenBucketOptions(capacity: 100);
        var limiter = new TokenBucketRateLimiter(clock, options);

        var policy = new RateLimitPolicy(
            permitLimit: 100,
            window: TimeSpan.FromSeconds(100));

        var tasks = Enumerable.Range(0, 1000)
            .Select(_ =>
                limiter.EvaluateAsync(
                    new RateLimitRequest("client-1"),
                    policy)
                    .AsTask())
            .ToArray();

        var decisions = await Task.WhenAll(tasks);

        Assert.Equal(100, decisions.Count(static decision => decision.Allowed));
        Assert.Equal(900, decisions.Count(static decision => !decision.Allowed));
    }

    [Fact]
    public async Task EvaluateAsync_ShouldKeepStatesIndependentAcrossPolicies()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var options = new TokenBucketOptions(capacity: 10);
        var limiter = new TokenBucketRateLimiter(clock, options);

        var lowRatePolicy = new RateLimitPolicy(
            permitLimit: 2,
            window: TimeSpan.FromSeconds(2));

        var highRatePolicy = new RateLimitPolicy(
            permitLimit: 10,
            window: TimeSpan.FromSeconds(1));

        var highRateDecision = await limiter.EvaluateAsync(
            new RateLimitRequest("client-1", cost: 10),
            highRatePolicy);

        var lowRateDecision = await limiter.EvaluateAsync(
            new RateLimitRequest("client-1", cost: 1),
            lowRatePolicy);

        Assert.True(highRateDecision.Allowed);
        Assert.True(lowRateDecision.Allowed);
        Assert.Equal(9, lowRateDecision.Remaining);
    }
}

