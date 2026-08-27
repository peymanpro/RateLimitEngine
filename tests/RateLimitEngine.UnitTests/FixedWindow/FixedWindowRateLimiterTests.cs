using RateLimitEngine.Algorithms.FixedWindow;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Algorithms.InMemory;

namespace RateLimitEngine.UnitTests.FixedWindow;

public sealed class FixedWindowRateLimiterTests
{
    [Fact]
    public async Task EvaluateAsync_ShouldAllowRequestsWithinLimit()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var limiter = new FixedWindowRateLimiter(new InMemoryFixedWindowStore(clock));
        var policy = new RateLimitPolicy(
            permitLimit: 3,
            window: TimeSpan.FromMinutes(1));

        var first =
            await limiter.EvaluateAsync(new RateLimitRequest("client-1"), policy);

        var second =
            await limiter.EvaluateAsync(new RateLimitRequest("client-1"), policy);

        var third =
            await limiter.EvaluateAsync(new RateLimitRequest("client-1"), policy);

        Assert.True(first.Allowed);
        Assert.True(second.Allowed);
        Assert.True(third.Allowed);

        Assert.Equal(3, first.Limit);
        Assert.Equal(2, first.Remaining);
        Assert.Equal(1, second.Remaining);
        Assert.Equal(0, third.Remaining);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldRejectRequestWhenLimitIsReached()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var limiter = new FixedWindowRateLimiter(new InMemoryFixedWindowStore(clock));
        var policy = new RateLimitPolicy(
            permitLimit: 2,
            window: TimeSpan.FromMinutes(1));

        await limiter.EvaluateAsync(
            new RateLimitRequest("client-1"),
            policy);

        await limiter.EvaluateAsync(
            new RateLimitRequest("client-1"),
            policy);

        var decision =
            await limiter.EvaluateAsync(
                new RateLimitRequest("client-1"),
                policy);

        Assert.False(decision.Allowed);
        Assert.Equal(0, decision.Remaining);
        Assert.NotNull(decision.RetryAfter);
        Assert.True(decision.RetryAfter >= TimeSpan.Zero);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldResetAtNextWindow()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var limiter = new FixedWindowRateLimiter(new InMemoryFixedWindowStore(clock));
        var policy = new RateLimitPolicy(
            permitLimit: 1,
            window: TimeSpan.FromMinutes(1));

        var first =
            await limiter.EvaluateAsync(
                new RateLimitRequest("client-1"),
                policy);

        Assert.True(first.Allowed);
        Assert.Equal(0, first.Remaining);

        clock.Advance(TimeSpan.FromMinutes(1));

        var afterReset =
            await limiter.EvaluateAsync(
                new RateLimitRequest("client-1"),
                policy);

        Assert.True(afterReset.Allowed);
        Assert.Equal(0, afterReset.Remaining);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldKeepLimitsIndependentPerKey()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var limiter = new FixedWindowRateLimiter(new InMemoryFixedWindowStore(clock));
        var policy = new RateLimitPolicy(
            permitLimit: 1,
            window: TimeSpan.FromMinutes(1));

        var clientOne =
            await limiter.EvaluateAsync(
                new RateLimitRequest("client-1"),
                policy);

        var clientTwo =
            await limiter.EvaluateAsync(
                new RateLimitRequest("client-2"),
                policy);

        Assert.True(clientOne.Allowed);
        Assert.True(clientTwo.Allowed);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldConsumeRequestCost()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var limiter = new FixedWindowRateLimiter(new InMemoryFixedWindowStore(clock));
        var policy = new RateLimitPolicy(
            permitLimit: 5,
            window: TimeSpan.FromMinutes(1));

        var decision =
            await limiter.EvaluateAsync(
                new RateLimitRequest("client-1", cost: 3),
                policy);

        Assert.True(decision.Allowed);
        Assert.Equal(2, decision.Remaining);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldRejectCostGreaterThanRemainingCapacity()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var limiter = new FixedWindowRateLimiter(new InMemoryFixedWindowStore(clock));
        var policy = new RateLimitPolicy(
            permitLimit: 5,
            window: TimeSpan.FromMinutes(1));

        await limiter.EvaluateAsync(
            new RateLimitRequest("client-1", cost: 4),
            policy);

        var decision =
            await limiter.EvaluateAsync(
                new RateLimitRequest("client-1", cost: 2),
                policy);

        Assert.False(decision.Allowed);
        Assert.Equal(1, decision.Remaining);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldRejectCostGreaterThanPermitLimit()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var limiter = new FixedWindowRateLimiter(new InMemoryFixedWindowStore(clock));
        var policy = new RateLimitPolicy(
            permitLimit: 5,
            window: TimeSpan.FromMinutes(1));

        var decision =
            await limiter.EvaluateAsync(
                new RateLimitRequest("client-1", cost: 6),
                policy);

        Assert.False(decision.Allowed);
        Assert.Equal(5, decision.Remaining);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldEnforceLimitUnderConcurrentRequests()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var limiter = new FixedWindowRateLimiter(new InMemoryFixedWindowStore(clock));
        var policy = new RateLimitPolicy(
            permitLimit: 100,
            window: TimeSpan.FromMinutes(1));

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
        Assert.All(
            decisions,
            decision => Assert.InRange(
                decision.Remaining,
                0,
                policy.PermitLimit));
    }
}



