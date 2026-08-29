using RateLimitEngine.Redis.FixedWindow;
using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.DistributedTests.Redis;

[Collection("Redis Docker Collection")]
public sealed class MultiInstanceRedisRecoveryTests
{
    private readonly RedisDockerFixture _redis;

    public MultiInstanceRedisRecoveryTests(
        RedisDockerFixture redis)
    {
        _redis = redis;
    }

    [Fact]
    public async Task TwoIndependentConnections_ShouldBothRecoverAfterRedisRestart()
    {
        await using var connectionA =
            await ConnectionMultiplexer.ConnectAsync(
                _redis.ConnectionString);

        await using var connectionB =
            await ConnectionMultiplexer.ConnectAsync(
                _redis.ConnectionString);

        var storeA = new RedisFixedWindowStore(
            new RedisScriptExecutor(
                connectionA.GetDatabase()));

        var storeB = new RedisFixedWindowStore(
            new RedisScriptExecutor(
                connectionB.GetDatabase()));

        var key =
            $"multi-instance-recovery-{Guid.NewGuid():N}";

        const int limit = 4;
        var window = TimeSpan.FromHours(1);

        var first = await storeA.IncrementAsync(
            key,
            window,
            limit,
            cost: 1);

        Assert.True(first.Accepted);
        Assert.Equal(1, first.Consumed);
        Assert.Equal(3, first.Remaining);

        var second = await storeB.IncrementAsync(
            key,
            window,
            limit,
            cost: 1);

        Assert.True(second.Accepted);
        Assert.Equal(2, second.Consumed);
        Assert.Equal(2, second.Remaining);

        await _redis.StopAsync();

        await AssertEventuallyRedisOperationFailsAsync(
            connectionA);

        await AssertEventuallyRedisOperationFailsAsync(
            connectionB);

        await _redis.StartAsync();

        await AssertEventuallyRedisOperationSucceedsAsync(
            connectionA);

        await AssertEventuallyRedisOperationSucceedsAsync(
            connectionB);

        var third = await storeA.IncrementAsync(
            key,
            window,
            limit,
            cost: 1);

        Assert.True(third.Accepted);
        Assert.Equal(3, third.Consumed);
        Assert.Equal(1, third.Remaining);

        var fourth = await storeB.IncrementAsync(
            key,
            window,
            limit,
            cost: 1);

        Assert.True(fourth.Accepted);
        Assert.Equal(4, fourth.Consumed);
        Assert.Equal(0, fourth.Remaining);

        var fifth = await storeA.IncrementAsync(
            key,
            window,
            limit,
            cost: 1);

        Assert.False(fifth.Accepted);
        Assert.Equal(4, fifth.Consumed);
        Assert.Equal(0, fifth.Remaining);
    }

    private static async Task
        AssertEventuallyRedisOperationFailsAsync(
            IConnectionMultiplexer connection)
    {
        var deadline =
            DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await connection
                    .GetDatabase()
                    .PingAsync();

                await Task.Delay(100);
            }
            catch (RedisException)
            {
                return;
            }
            catch (TimeoutException)
            {
                return;
            }
        }

        throw new Xunit.Sdk.XunitException(
            "Redis operation did not fail after the container was stopped.");
    }

    private static async Task
        AssertEventuallyRedisOperationSucceedsAsync(
            IConnectionMultiplexer connection)
    {
        var deadline =
            DateTime.UtcNow.AddSeconds(15);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await connection
                    .GetDatabase()
                    .PingAsync();

                return;
            }
            catch (RedisException)
            {
                await Task.Delay(100);
            }
            catch (TimeoutException)
            {
                await Task.Delay(100);
            }
        }

        throw new Xunit.Sdk.XunitException(
            "Redis connection did not recover after restart.");
    }
}
