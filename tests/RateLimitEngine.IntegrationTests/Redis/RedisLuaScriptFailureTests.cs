using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.IntegrationTests.Redis;

public sealed class RedisLuaScriptFailureTests
{
    [Fact]
    public async Task RedisScriptExecutor_ShouldPropagateRedisServerException_WhenLuaScriptFails()
    {
        var configuration =
            ConfigurationOptions.Parse(
                "localhost:6382");

        configuration.AbortOnConnectFail = false;
        configuration.ConnectTimeout = 1000;
        configuration.SyncTimeout = 1000;
        configuration.AsyncTimeout = 1000;

        await using var connection =
            await ConnectionMultiplexer.ConnectAsync(
                configuration);

        var database =
            connection.GetDatabase();

        var executor =
            new RedisScriptExecutor(database);

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
