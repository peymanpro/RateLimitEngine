using StackExchange.Redis;
using Xunit;

namespace RateLimitEngine.IntegrationTests.Redis;

[Collection("Redis Timeout Collection")]
public sealed class RedisTimeoutTests
{
    private static async Task PauseRedisAsync(
        IConnectionMultiplexer connection,
        int milliseconds)
    {
        var server =
            connection.GetServer(
                connection.GetEndPoints().Single());

        await server.ExecuteAsync(
            "CLIENT",
            "PAUSE",
            milliseconds,
            "ALL");
    }

    private static async Task<IConnectionMultiplexer>
        CreateAdminConnectionAsync()
    {
        var configuration =
            ConfigurationOptions.Parse(
                "localhost:6382");

        configuration.AbortOnConnectFail = false;
        configuration.ConnectTimeout = 1000;
        configuration.SyncTimeout = 5000;
        configuration.AsyncTimeout = 5000;
        configuration.AllowAdmin = true;

        return await ConnectionMultiplexer.ConnectAsync(
            configuration);
    }

    private static async Task<IConnectionMultiplexer>
        CreateTimeoutConnectionAsync()
    {
        var configuration =
            ConfigurationOptions.Parse(
                "localhost:6382");

        configuration.AbortOnConnectFail = false;
        configuration.ConnectTimeout = 1000;
        configuration.SyncTimeout = 100;
        configuration.AsyncTimeout = 100;

        return await ConnectionMultiplexer.ConnectAsync(
            configuration);
    }

    [Fact]
    public async Task Redis_ShouldThrowRedisTimeoutException_WhenCommandTimesOut()
    {
        await using var blocker =
            await CreateAdminConnectionAsync();

        await using var connection =
            await CreateTimeoutConnectionAsync();

        await connection
            .GetDatabase()
            .PingAsync();

        await PauseRedisAsync(
            blocker,
            milliseconds: 1000);

        var exception =
            await Assert.ThrowsAsync<RedisTimeoutException>(
                async () =>
                    await connection
                        .GetDatabase()
                        .ScriptEvaluateAsync(
                            "return 1"));

        Assert.Contains(
            "Timeout",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }
}
