using RateLimitEngine.Algorithms.FixedWindow;
using RateLimitEngine.Algorithms.Gcra;
using RateLimitEngine.Algorithms.InMemory;
using RateLimitEngine.Algorithms.SlidingWindow;
using RateLimitEngine.Algorithms.TokenBucket;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Time;
using RateLimitEngine.Testing;

namespace RateLimitEngine.UnitTests.Contracts;

public sealed class RateLimiterContractTests
{
    public static IEnumerable<object[]> Limiters()
    {
        yield return
        [
            "fixed-window",
            new Func<IClock, IRateLimiter>(clock =>
                new FixedWindowRateLimiter(clock, new InMemoryFixedWindowStore(clock)))
        ];

        yield return
        [
            "sliding-window",
            new Func<IClock, IRateLimiter>(clock =>
                new SlidingWindowRateLimiter(clock))
        ];

        yield return
        [
            "token-bucket",
            new Func<IClock, IRateLimiter>(clock =>
                new TokenBucketRateLimiter(new InMemoryTokenBucketStore(clock), new TokenBucketOptions(capacity: 5)))
        ];

        yield return
        [
            "gcra",
            new Func<IClock, IRateLimiter>(clock =>
                new GcraRateLimiter(clock))
        ];
    }

    [Theory]
    [MemberData(nameof(Limiters))]
    public async Task EvaluateAsync_ShouldBeIndependentPerKey(
        string algorithm,
        Func<IClock, IRateLimiter> factory)
    {
        var clock = CreateClock();
        var limiter = factory(clock);
        var policy = CreatePolicy();

        var first = await limiter.EvaluateAsync(
            new RateLimitRequest("client-1"),
            policy);

        var second = await limiter.EvaluateAsync(
            new RateLimitRequest("client-2"),
            policy);

        Assert.True(first.Allowed, algorithm);
        Assert.True(second.Allowed, algorithm);
    }

    [Theory]
    [MemberData(nameof(Limiters))]
    public async Task EvaluateAsync_ShouldRejectCostGreaterThanPermitLimit(
        string algorithm,
        Func<IClock, IRateLimiter> factory)
    {
        var clock = CreateClock();
        var limiter = factory(clock);
        var policy = CreatePolicy();

        var decision = await limiter.EvaluateAsync(
            new RateLimitRequest("client-1", cost: 6),
            policy);

        Assert.False(decision.Allowed);
        Assert.Equal(policy.PermitLimit, decision.Limit);
        Assert.NotNull(algorithm);
    }

    [Theory]
    [MemberData(nameof(Limiters))]
    public async Task EvaluateAsync_ShouldHonorCancellation(
        string algorithm,
        Func<IClock, IRateLimiter> factory)
    {
        var clock = CreateClock();
        var limiter = factory(clock);
        var policy = CreatePolicy();

        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var exception =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () =>
                    await limiter.EvaluateAsync(
                        new RateLimitRequest("client-1"),
                        policy,
                        cancellationTokenSource.Token));

        Assert.NotNull(exception);
        Assert.NotNull(algorithm);
    }

    private static FakeClock CreateClock() =>
        new(
            new DateTimeOffset(
                2026,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero));

    private static RateLimitPolicy CreatePolicy() =>
        new(
            permitLimit: 5,
            window: TimeSpan.FromSeconds(5));
}





