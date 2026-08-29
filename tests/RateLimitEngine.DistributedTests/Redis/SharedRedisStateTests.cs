using RateLimitEngine.Redis.FixedWindow;
using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.DistributedTests.Redis;

public sealed class SharedRedisStateTests
{
    [Fact]
    public async Task TwoIndependentInstances_ShouldObserveConsistentSharedState()
    {
        await using var connectionA =
            await ConnectionMultiplexer.ConnectAsync(
                "localhost:6379");

        await using var connectionB =
            await ConnectionMultiplexer.ConnectAsync(
                "localhost:6379");

        var storeA = new RedisFixedWindowStore(
            new RedisScriptExecutor(
                connectionA.GetDatabase()));

        var storeB = new RedisFixedWindowStore(
            new RedisScriptExecutor(
                connectionB.GetDatabase()));

        var key =
            $"distributed-consistency-{Guid.NewGuid():N}";

        const int limit = 5;
        var window = TimeSpan.FromSeconds(30);

        var first = await storeA.IncrementAsync(
            key, window, limit, cost: 1);

        Assert.True(first.Accepted);
        Assert.Equal(1, first.Consumed);
        Assert.Equal(4, first.Remaining);

        var second = await storeB.IncrementAsync(
            key, window, limit, cost: 1);

        Assert.True(second.Accepted);
        Assert.Equal(2, second.Consumed);
        Assert.Equal(3, second.Remaining);

        var third = await storeA.IncrementAsync(
            key, window, limit, cost: 1);

        Assert.True(third.Accepted);
        Assert.Equal(3, third.Consumed);
        Assert.Equal(2, third.Remaining);

        var fourth = await storeB.IncrementAsync(
            key, window, limit, cost: 1);

        Assert.True(fourth.Accepted);
        Assert.Equal(4, fourth.Consumed);
        Assert.Equal(1, fourth.Remaining);

        var fifth = await storeA.IncrementAsync(
            key, window, limit, cost: 1);

        Assert.True(fifth.Accepted);
        Assert.Equal(5, fifth.Consumed);
        Assert.Equal(0, fifth.Remaining);

        var sixth = await storeB.IncrementAsync(
            key, window, limit, cost: 1);

        Assert.False(sixth.Accepted);
        Assert.Equal(5, sixth.Consumed);
        Assert.Equal(0, sixth.Remaining);
    }
}
