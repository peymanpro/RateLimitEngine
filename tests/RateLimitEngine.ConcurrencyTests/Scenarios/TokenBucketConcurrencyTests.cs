using RateLimitEngine.Algorithms.TokenBucket;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Algorithms.InMemory;
using RateLimitEngine.Testing;

namespace RateLimitEngine.ConcurrencyTests.Scenarios;

public sealed class TokenBucketConcurrencyTests
{
    [Fact]
    public async Task ShouldNeverConsumeMoreThanAvailableTokensUnderConcurrency()
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

        var limiter = new TokenBucketRateLimiter(new InMemoryTokenBucketStore(clock), new TokenBucketOptions(capacity: 250));

        var policy = new RateLimitPolicy(
            permitLimit: 100,
            window: TimeSpan.FromSeconds(1));

        var tasks = Enumerable.Range(0, 10_000)
            .Select(_ =>
                limiter.EvaluateAsync(
                    new RateLimitRequest("client-1"),
                    policy)
                    .AsTask())
            .ToArray();

        var decisions = await Task.WhenAll(tasks);

        Assert.Equal(
            250,
            decisions.Count(static decision => decision.Allowed));

        Assert.Equal(
            9_750,
            decisions.Count(static decision => !decision.Allowed));
    }
}

