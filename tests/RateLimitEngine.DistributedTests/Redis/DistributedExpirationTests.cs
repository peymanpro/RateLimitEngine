using RateLimitEngine.Redis.FixedWindow;
using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.DistributedTests.Redis;

public sealed class DistributedExpirationTests
{
    [Fact]
    public async Task TwoIndependentInstances_ShouldObserveNewStateAfterWindowReset()
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
            $"distributed-expiration-{Guid.NewGuid():N}";

        const int limit = 1;
        var window = TimeSpan.FromSeconds(1);

        var first = await storeA.IncrementAsync(
            key,
            window,
            limit,
            cost: 1);

        Assert.True(first.Accepted);
        Assert.Equal(1, first.Consumed);
        Assert.Equal(0, first.Remaining);
        Assert.True(first.ResetAfter > TimeSpan.Zero);

        var blocked = await storeB.IncrementAsync(
            key,
            window,
            limit,
            cost: 1);

        Assert.False(blocked.Accepted);
        Assert.Equal(1, blocked.Consumed);
        Assert.Equal(0, blocked.Remaining);

        await Task.Delay(
            first.ResetAfter + TimeSpan.FromMilliseconds(100));

        var afterReset = await storeB.IncrementAsync(
            key,
            window,
            limit,
            cost: 1);

        Assert.True(afterReset.Accepted);
        Assert.Equal(1, afterReset.Consumed);
        Assert.Equal(0, afterReset.Remaining);
    }
}
