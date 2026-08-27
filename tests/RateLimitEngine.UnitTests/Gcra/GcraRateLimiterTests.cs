using RateLimitEngine.Algorithms.Gcra;
using RateLimitEngine.Core.Models;

namespace RateLimitEngine.UnitTests.Gcra;

public sealed class GcraRateLimiterTests
{
    [Fact]
    public async Task EvaluateAsync_ShouldAllowInitialRequests()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var limiter = new GcraRateLimiter(clock);
        var policy = new RateLimitPolicy(
            permitLimit: 2,
            window: TimeSpan.FromSeconds(2));

        var first =
            await limiter.EvaluateAsync(
                new RateLimitRequest("client-1"),
                policy);

        var second =
            await limiter.EvaluateAsync(
                new RateLimitRequest("client-1"),
                policy);

        Assert.True(first.Allowed);
        Assert.True(second.Allowed);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldEnforceConfiguredRate()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var limiter = new GcraRateLimiter(clock);
        var policy = new RateLimitPolicy(
            permitLimit: 2,
            window: TimeSpan.FromSeconds(2));

        await limiter.EvaluateAsync(
            new RateLimitRequest("client-1"),
            policy);

        await limiter.EvaluateAsync(
            new RateLimitRequest("client-1"),
            policy);

        var third =
            await limiter.EvaluateAsync(
                new RateLimitRequest("client-1"),
                policy);

        Assert.False(third.Allowed);
        Assert.NotNull(third.RetryAfter);
        Assert.True(third.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldAllowAfterRequiredDelay()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var limiter = new GcraRateLimiter(clock);
        var policy = new RateLimitPolicy(
            permitLimit: 2,
            window: TimeSpan.FromSeconds(2));

        await limiter.EvaluateAsync(
            new RateLimitRequest("client-1"),
            policy);

        await limiter.EvaluateAsync(
            new RateLimitRequest("client-1"),
            policy);

        clock.Advance(TimeSpan.FromSeconds(1));

        var decision =
            await limiter.EvaluateAsync(
                new RateLimitRequest("client-1"),
                policy);

        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldKeepKeysIndependent()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var limiter = new GcraRateLimiter(clock);
        var policy = new RateLimitPolicy(
            permitLimit: 1,
            window: TimeSpan.FromSeconds(1));

        var first =
            await limiter.EvaluateAsync(
                new RateLimitRequest("client-1"),
                policy);

        var second =
            await limiter.EvaluateAsync(
                new RateLimitRequest("client-2"),
                policy);

        Assert.True(first.Allowed);
        Assert.True(second.Allowed);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldRespectRequestCost()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var limiter = new GcraRateLimiter(clock);
        var policy = new RateLimitPolicy(
            permitLimit: 5,
            window: TimeSpan.FromSeconds(5));

        var decision =
            await limiter.EvaluateAsync(
                new RateLimitRequest("client-1", cost: 3),
                policy);

        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldRejectCostGreaterThanPermitLimit()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var limiter = new GcraRateLimiter(clock);
        var policy = new RateLimitPolicy(
            permitLimit: 5,
            window: TimeSpan.FromSeconds(5));

        var decision =
            await limiter.EvaluateAsync(
                new RateLimitRequest("client-1", cost: 6),
                policy);

        Assert.False(decision.Allowed);
        Assert.Equal(5, decision.Remaining);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldRemainSafeUnderConcurrentRequests()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var limiter = new GcraRateLimiter(clock);
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
    public async Task EvaluateAsync_ShouldTreatExactToleranceBoundaryAsAllowed()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var limiter = new GcraRateLimiter(clock);
        var policy = new RateLimitPolicy(
            permitLimit: 2,
            window: TimeSpan.FromSeconds(2));

        await limiter.EvaluateAsync(
            new RateLimitRequest("client-1"),
            policy);

        await limiter.EvaluateAsync(
            new RateLimitRequest("client-1"),
            policy);

        clock.Advance(TimeSpan.FromSeconds(1));

        var decision =
            await limiter.EvaluateAsync(
                new RateLimitRequest("client-1"),
                policy);

        Assert.True(decision.Allowed);
    }
}
