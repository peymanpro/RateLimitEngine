using System.Net;
using StackExchange.Redis;

namespace RateLimitEngine.IntegrationTests.AspNetCore;

public sealed class RedisTimeoutMiddlewareTests
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

    [Fact]
    public async Task Middleware_ShouldFailOpenWhenRedisCommandTimesOut()
    {
        await using var factory =
            new RateLimitEngine.IntegrationTests
                .RedisTimeoutTestFactory(
                    "FailOpen");

        using var client =
            factory.CreateClient();

        await using var blocker =
            await CreateAdminConnectionAsync();

        await PauseRedisAsync(
            blocker,
            milliseconds: 1000);

        using var response =
            await client.GetAsync(
                "/weatherforecast");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task Middleware_ShouldFailClosedWhenRedisCommandTimesOut()
    {
        await using var factory =
            new RateLimitEngine.IntegrationTests
                .RedisTimeoutTestFactory(
                    "FailClosed");

        using var client =
            factory.CreateClient();

        await using var blocker =
            await CreateAdminConnectionAsync();

        await PauseRedisAsync(
            blocker,
            milliseconds: 1000);

        using var response =
            await client.GetAsync(
                "/weatherforecast");

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            response.StatusCode);
    }
}
