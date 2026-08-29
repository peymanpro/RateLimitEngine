using StackExchange.Redis;

namespace RateLimitEngine.IntegrationTests.Redis;

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
                "localhost:6379");

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
                "localhost:6379");

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

        var database =
            connection.GetDatabase();

        await PauseRedisAsync(
            blocker,
            milliseconds: 1000);

        var exception =
            await Assert.ThrowsAsync<RedisTimeoutException>(
                async () =>
                    await database.ScriptEvaluateAsync(
                        "return 1"));

        Assert.Contains(
            "Timeout",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }
}
