using RateLimitEngine.Redis.Infrastructure;
using RateLimitEngine.Redis.SlidingWindow;
using StackExchange.Redis;

namespace RateLimitEngine.DistributedTests.Redis;

public sealed class DistributedSlidingWindowCostTests
{
    [Fact]
    public async Task TwoIndependentInstances_ShouldShareWeightedSlidingWindowState()
    {
        await using var connectionA =
            await ConnectionMultiplexer.ConnectAsync(
                "localhost:6379");

        await using var connectionB =
            await ConnectionMultiplexer.ConnectAsync(
                "localhost:6379");

        var storeA = new RedisSlidingWindowStore(
            new RedisScriptExecutor(
                connectionA.GetDatabase()));

        var storeB = new RedisSlidingWindowStore(
            new RedisScriptExecutor(
                connectionB.GetDatabase()));

        var key =
            $"distributed-sliding-window-cost-{Guid.NewGuid():N}";

        const int limit = 5;
        var window = TimeSpan.FromSeconds(30);

        var first = await storeA.EvaluateAsync(
            key,
            window,
            limit,
            cost: 2);

        Assert.True(first.Accepted);
        Assert.Equal(2, first.Consumed);
        Assert.Equal(3, first.Remaining);

        var second = await storeB.EvaluateAsync(
            key,
            window,
            limit,
            cost: 2);

        Assert.True(second.Accepted);
        Assert.Equal(4, second.Consumed);
        Assert.Equal(1, second.Remaining);

        var third = await storeA.EvaluateAsync(
            key,
            window,
            limit,
            cost: 2);

        Assert.False(third.Accepted);
        Assert.Equal(4, third.Consumed);
        Assert.Equal(1, third.Remaining);
        Assert.NotNull(third.RetryAfter);
        Assert.True(third.RetryAfter > TimeSpan.Zero);
        Assert.NotNull(third.ResetAfter);
        Assert.True(third.ResetAfter > TimeSpan.Zero);
    }
}
