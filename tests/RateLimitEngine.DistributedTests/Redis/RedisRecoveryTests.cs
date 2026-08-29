using RateLimitEngine.Redis.FixedWindow;
using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.DistributedTests.Redis;

[Collection("Redis Docker Collection")]
public sealed class RedisRecoveryTests
{
    private readonly RedisDockerFixture _redis;

    public RedisRecoveryTests(
        RedisDockerFixture redis)
    {
        _redis = redis;
    }

    [Fact]
    public async Task SameRedisConnection_ShouldRecoverAfterRedisRestart()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync(
                _redis.ConnectionString);

        var store = new RedisFixedWindowStore(
            new RedisScriptExecutor(
                connection.GetDatabase()));

        var key =
            $"redis-recovery-{Guid.NewGuid():N}";

        const int limit = 2;
        var window = TimeSpan.FromHours(1);

        var first = await store.IncrementAsync(
            key,
            window,
            limit,
            cost: 1);

        Assert.True(first.Accepted);
        Assert.Equal(1, first.Consumed);
        Assert.Equal(1, first.Remaining);

        await _redis.StopAsync();

        await AssertEventuallyRedisOperationFailsAsync(
            connection);

        await _redis.StartAsync();

        await AssertEventuallyRedisOperationSucceedsAsync(
            connection);

        var second = await store.IncrementAsync(
            key,
            window,
            limit,
            cost: 1);

        Assert.True(second.Accepted);
        Assert.Equal(2, second.Consumed);
        Assert.Equal(0, second.Remaining);
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
            catch
            {
                await Task.Delay(100);
            }
        }

        throw new Xunit.Sdk.XunitException(
            "The same Redis connection did not recover after restart.");
    }
}
