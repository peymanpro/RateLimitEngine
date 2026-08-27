using RateLimitEngine.Algorithms.SlidingWindow;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Testing;

namespace RateLimitEngine.ConcurrencyTests.Scenarios;

public sealed class SlidingWindowConcurrencyTests
{
    [Fact]
    public async Task ShouldNeverExceedPermitLimitUnderHighConcurrency()
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

        var limiter = new SlidingWindowRateLimiter(clock);

        var policy = new RateLimitPolicy(
            permitLimit: 100,
            window: TimeSpan.FromMinutes(1));

        var tasks = Enumerable.Range(0, 10_000)
            .Select(_ =>
                limiter.EvaluateAsync(
                    new RateLimitRequest("client-1"),
                    policy)
                    .AsTask())
            .ToArray();

        var decisions = await Task.WhenAll(tasks);

        Assert.Equal(
            100,
            decisions.Count(static decision => decision.Allowed));

        Assert.Equal(
            9_900,
            decisions.Count(static decision => !decision.Allowed));
    }
}
