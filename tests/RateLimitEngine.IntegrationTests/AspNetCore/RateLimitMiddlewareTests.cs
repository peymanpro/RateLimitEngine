using System.Net;
using RateLimitEngine.IntegrationTests;

namespace RateLimitEngine.IntegrationTests.AspNetCore;

public sealed class RateLimitMiddlewareTests
{
    [Fact]
    public async Task Middleware_ShouldReturn429AfterLimitIsReached()
    {
        await using var factory =
            new AspNetCoreTestFactory();

        using var client =
            factory.CreateClient();

        var responses = new List<HttpResponseMessage>();

        for (var i = 0; i < 6; i++)
        {
            responses.Add(
                await client.GetAsync("/weatherforecast"));
        }

        Assert.All(
            responses.Take(5),
            response =>
                Assert.Equal(
                    HttpStatusCode.OK,
                    response.StatusCode));

        var rejected = responses[5];

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            rejected.StatusCode);

        Assert.Equal(
            "5",
            rejected.Headers.GetValues(
                "X-RateLimit-Limit")
                .Single());

        Assert.Equal(
            "0",
            rejected.Headers.GetValues(
                "X-RateLimit-Remaining")
                .Single());

        Assert.True(
            rejected.Headers.Contains("Retry-After"));
    }
}
