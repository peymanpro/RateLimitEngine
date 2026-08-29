using System.Net;

namespace RateLimitEngine.IntegrationTests.AspNetCore;

public sealed class MultiInstanceFailureMiddlewareTests
{
    [Fact]
    public async Task IndependentInstances_ShouldApplyTheirOwnFailureStrategy()
    {
        await using var failOpenFactory =
            new RateLimitEngine.IntegrationTests.RedisFailureTestFactory(
                "FailOpen");

        await using var failClosedFactory =
            new RateLimitEngine.IntegrationTests.RedisFailureTestFactory(
                "FailClosed");

        using var failOpenClient =
            failOpenFactory.CreateClient();

        using var failClosedClient =
            failClosedFactory.CreateClient();

        using var openResponse =
            await failOpenClient.GetAsync(
                "/weatherforecast");

        using var closedResponse =
            await failClosedClient.GetAsync(
                "/weatherforecast");

        Assert.Equal(
            HttpStatusCode.OK,
            openResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            closedResponse.StatusCode);
    }
}
