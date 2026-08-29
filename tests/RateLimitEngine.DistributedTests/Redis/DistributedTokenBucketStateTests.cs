using RateLimitEngine.Redis.Infrastructure;
using RateLimitEngine.Redis.TokenBucket;
using StackExchange.Redis;

namespace RateLimitEngine.DistributedTests.Redis;

public sealed class DistributedTokenBucketStateTests
{
    [Fact]
    public async Task TwoIndependentInstances_ShouldShareTokenBucketState()
    {
        await using var connectionA =
            await ConnectionMultiplexer.ConnectAsync(
                "localhost:6379");

        await using var connectionB =
            await ConnectionMultiplexer.ConnectAsync(
                "localhost:6379");

        var storeA = new RedisTokenBucketStore(
            new RedisScriptExecutor(
                connectionA.GetDatabase()));

        var storeB = new RedisTokenBucketStore(
            new RedisScriptExecutor(
                connectionB.GetDatabase()));

        var key =
            $"distributed-token-bucket-{Guid.NewGuid():N}";

        const double capacity = 5;
        const double refillRate = 1;
        const int cost = 1;

        var first = await storeA.ConsumeAsync(
            key,
            capacity,
            refillRate,
            cost);

        Assert.True(first.Accepted);
        Assert.InRange(first.RemainingTokens, 3.9, 4.0);

        var second = await storeB.ConsumeAsync(
            key,
            capacity,
            refillRate,
            cost);

        Assert.True(second.Accepted);
        Assert.InRange(second.RemainingTokens, 2.9, 3.1);

        var third = await storeA.ConsumeAsync(
            key,
            capacity,
            refillRate,
            cost);

        Assert.True(third.Accepted);
        Assert.InRange(third.RemainingTokens, 1.9, 2.1);

        var fourth = await storeB.ConsumeAsync(
            key,
            capacity,
            refillRate,
            cost);

        Assert.True(fourth.Accepted);
        Assert.InRange(fourth.RemainingTokens, 0.9, 1.1);

        var fifth = await storeA.ConsumeAsync(
            key,
            capacity,
            refillRate,
            cost);

        Assert.True(fifth.Accepted);
        Assert.InRange(fifth.RemainingTokens, 0.0, 0.2);

        var sixth = await storeB.ConsumeAsync(
            key,
            capacity,
            refillRate,
            cost);

        Assert.False(sixth.Accepted);
        Assert.InRange(sixth.RemainingTokens, 0.0, 0.2);
        Assert.NotNull(sixth.RetryAfter);
        Assert.True(sixth.RetryAfter > TimeSpan.Zero);
    }
}
