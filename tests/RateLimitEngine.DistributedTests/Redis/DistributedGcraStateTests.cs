using RateLimitEngine.Redis.Gcra;
using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.DistributedTests.Redis;

public sealed class DistributedGcraStateTests
{
    [Fact]
    public async Task TwoIndependentInstances_ShouldShareGcraState()
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
            $"distributed-gcra-{Guid.NewGuid():N}";

        var interval = TimeSpan.FromSeconds(1);
        var burstTolerance = TimeSpan.FromSeconds(4);

        var first = await storeA.EvaluateAsync(
            key,
            interval,
            burstTolerance,
            cost: 1);

        Assert.True(first.Accepted);
        Assert.Equal(4, first.Remaining);
        Assert.Null(first.RetryAfter);

        var second = await storeB.EvaluateAsync(
            key,
            interval,
            burstTolerance,
            cost: 1);

        Assert.True(second.Accepted);
        Assert.Equal(3, second.Remaining);
        Assert.Null(second.RetryAfter);

        var third = await storeA.EvaluateAsync(
            key,
            interval,
            burstTolerance,
            cost: 1);

        Assert.True(third.Accepted);
        Assert.Equal(2, third.Remaining);
        Assert.Null(third.RetryAfter);

        var fourth = await storeB.EvaluateAsync(
            key,
            interval,
            burstTolerance,
            cost: 1);

        Assert.True(fourth.Accepted);
        Assert.Equal(1, fourth.Remaining);
        Assert.Null(fourth.RetryAfter);

        var fifth = await storeA.EvaluateAsync(
            key,
            interval,
            burstTolerance,
            cost: 1);

        Assert.True(fifth.Accepted);
        Assert.Equal(0, fifth.Remaining);
        Assert.Null(fifth.RetryAfter);

        var sixth = await storeB.EvaluateAsync(
            key,
            interval,
            burstTolerance,
            cost: 1);

        Assert.False(sixth.Accepted);
        Assert.Equal(0, sixth.Remaining);
        Assert.NotNull(sixth.RetryAfter);
        Assert.True(sixth.RetryAfter > TimeSpan.Zero);

        Assert.True(
            sixth.TheoreticalArrivalTime >=
            fifth.TheoreticalArrivalTime);
    }
}
