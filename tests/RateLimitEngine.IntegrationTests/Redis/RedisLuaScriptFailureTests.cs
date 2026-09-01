using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.IntegrationTests.Redis;

[Collection("Redis Timeout Collection")]
public sealed class RedisLuaScriptFailureTests
{
    [Fact]
    public async Task RedisScriptExecutor_ShouldPropagateRedisServerException_WhenLuaScriptFails()
    {
        var configuration =
            ConfigurationOptions.Parse("localhost:6382");

        configuration.AbortOnConnectFail = false;
        configuration.ConnectTimeout = 3000;
        configuration.SyncTimeout = 5000;
        configuration.AsyncTimeout = 5000;

        await using var connection =
            await ConnectionMultiplexer.ConnectAsync(configuration);

        var executor =
            new RedisScriptExecutor(connection.GetDatabase());

        var exception =
            await Assert.ThrowsAsync<RedisServerException>(
                async () =>
                    await executor.ExecuteAsync(
                        "error('forced lua failure')",
                        Array.Empty<RedisKey>(),
                        Array.Empty<RedisValue>()));

        Assert.Contains(
            "forced lua failure",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }
}
