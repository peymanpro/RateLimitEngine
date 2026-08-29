using System.Net;

namespace RateLimitEngine.IntegrationTests.AspNetCore;

public sealed class RedisFailureMiddlewareTests
{
    [Fact]
    public async Task Middleware_ShouldFailOpenWhenRedisIsUnavailable()
    {
        await using var factory =
            new RateLimitEngine.IntegrationTests.RedisFailureTestFactory(
                "FailOpen");

        using var client =
            factory.CreateClient();

        using var response =
            await client.GetAsync("/weatherforecast");

        Assert.NotEqual(
            HttpStatusCode.ServiceUnavailable,
            response.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task Middleware_ShouldFailClosedWhenRedisIsUnavailable()
    {
        await using var factory =
            new RateLimitEngine.IntegrationTests.RedisFailureTestFactory(
                "FailClosed");

        using var client =
            factory.CreateClient();

        using var response =
            await client.GetAsync("/weatherforecast");

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            response.StatusCode);
    }
}
