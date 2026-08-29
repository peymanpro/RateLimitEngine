using RateLimitEngine.Redis.FixedWindow;
using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.DistributedTests.Redis;

public sealed class SharedRedisStateTests
{
    [Fact]
    public async Task TwoIndependentInstances_ShouldShareTheSameRateLimitState()
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
            $"distributed-fixed-window-{Guid.NewGuid():N}";

        const int limit = 5;
        var window = TimeSpan.FromSeconds(30);

        var first = await storeA.IncrementAsync(
            key, window, limit, cost: 1);

        var second = await storeB.IncrementAsync(
            key, window, limit, cost: 1);

        var third = await storeA.IncrementAsync(
            key, window, limit, cost: 1);

        var fourth = await storeB.IncrementAsync(
            key, window, limit, cost: 1);

        var fifth = await storeA.IncrementAsync(
            key, window, limit, cost: 1);

        var sixth = await storeB.IncrementAsync(
            key, window, limit, cost: 1);

        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
        Assert.True(third.Accepted);
        Assert.True(fourth.Accepted);
        Assert.True(fifth.Accepted);

        Assert.False(sixth.Accepted);

        Assert.Equal(5, fifth.Consumed);
        Assert.Equal(0, fifth.Remaining);
        Assert.Equal(0, sixth.Remaining);
    }
}
