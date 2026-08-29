using RateLimitEngine.Redis.Gcra;
using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.DistributedTests.Redis;

public sealed class DistributedGcraCostTests
{
    [Fact]
    public async Task TwoIndependentInstances_ShouldShareWeightedGcraState()
    {
        await using var connectionA =
            await ConnectionMultiplexer.ConnectAsync(
                "localhost:6379");

        await using var connectionB =
            await ConnectionMultiplexer.ConnectAsync(
                "localhost:6379");

        var storeA = new RedisGcraStore(
            new RedisScriptExecutor(
                connectionA.GetDatabase()));

        var storeB = new RedisGcraStore(
            new RedisScriptExecutor(
                connectionB.GetDatabase()));

        var key =
            $"distributed-gcra-cost-{Guid.NewGuid():N}";

        var interval = TimeSpan.FromSeconds(1);
        var burstTolerance = TimeSpan.FromSeconds(3);

        var first = await storeA.EvaluateAsync(
            key,
            interval,
            burstTolerance,
            cost: 2);

        Assert.True(first.Accepted);
        Assert.Equal(2, first.Remaining);
        Assert.Null(first.RetryAfter);

        var second = await storeB.EvaluateAsync(
            key,
            interval,
            burstTolerance,
            cost: 2);

        Assert.True(second.Accepted);
        Assert.Equal(0, second.Remaining);
        Assert.Null(second.RetryAfter);

        var third = await storeA.EvaluateAsync(
            key,
            interval,
            burstTolerance,
            cost: 2);

        Assert.False(third.Accepted);
        Assert.Equal(0, third.Remaining);
        Assert.NotNull(third.RetryAfter);
        Assert.True(third.RetryAfter > TimeSpan.Zero);

        Assert.True(
            third.TheoreticalArrivalTime >=
            second.TheoreticalArrivalTime);
    }
}
