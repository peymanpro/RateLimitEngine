using RateLimitEngine.Redis.Infrastructure;
using RateLimitEngine.Redis.SlidingWindow;
using StackExchange.Redis;

namespace RateLimitEngine.DistributedTests.Redis;

public sealed class DistributedSlidingWindowStateTests
{
    [Fact]
    public async Task TwoIndependentInstances_ShouldShareSlidingWindowState()
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
            $"distributed-sliding-window-{Guid.NewGuid():N}";

        const int limit = 5;
        var window = TimeSpan.FromSeconds(30);

        var first = await storeA.EvaluateAsync(
            key,
            window,
            limit,
            cost: 1);

        Assert.True(first.Accepted);
        Assert.Equal(1, first.Consumed);
        Assert.Equal(4, first.Remaining);

        var second = await storeB.EvaluateAsync(
            key,
            window,
            limit,
            cost: 1);

        Assert.True(second.Accepted);
        Assert.Equal(2, second.Consumed);
        Assert.Equal(3, second.Remaining);

        var third = await storeA.EvaluateAsync(
            key,
            window,
            limit,
            cost: 1);

        Assert.True(third.Accepted);
        Assert.Equal(3, third.Consumed);
        Assert.Equal(2, third.Remaining);

        var fourth = await storeB.EvaluateAsync(
            key,
            window,
            limit,
            cost: 1);

        Assert.True(fourth.Accepted);
        Assert.Equal(4, fourth.Consumed);
        Assert.Equal(1, fourth.Remaining);

        var fifth = await storeA.EvaluateAsync(
            key,
            window,
            limit,
            cost: 1);

        Assert.True(fifth.Accepted);
        Assert.Equal(5, fifth.Consumed);
        Assert.Equal(0, fifth.Remaining);
        Assert.True(fifth.ResetAfter > TimeSpan.Zero);

        var sixth = await storeB.EvaluateAsync(
            key,
            window,
            limit,
            cost: 1);

        Assert.False(sixth.Accepted);
        Assert.Equal(5, sixth.Consumed);
        Assert.Equal(0, sixth.Remaining);
        Assert.True(sixth.RetryAfter > TimeSpan.Zero);
        Assert.True(sixth.ResetAfter > TimeSpan.Zero);
    }
}
