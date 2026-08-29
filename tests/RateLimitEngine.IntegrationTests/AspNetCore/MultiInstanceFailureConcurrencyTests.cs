using System.Net;

namespace RateLimitEngine.IntegrationTests.AspNetCore;

public sealed class MultiInstanceFailureConcurrencyTests
{
    [Fact]
    public async Task IndependentInstances_ShouldMaintainFailurePolicyUnderConcurrency()
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

        const int requestsPerInstance = 50;

        var failOpenTasks =
            Enumerable
                .Range(0, requestsPerInstance)
                .Select(_ =>
                    failOpenClient.GetAsync(
                        "/weatherforecast"))
                .ToArray();

        var failClosedTasks =
            Enumerable
                .Range(0, requestsPerInstance)
                .Select(_ =>
                    failClosedClient.GetAsync(
                        "/weatherforecast"))
                .ToArray();

        var allTasks =
            failOpenTasks
                .Cast<Task<HttpResponseMessage>>()
                .Concat(failClosedTasks)
                .ToArray();

        var responses =
            await Task.WhenAll(allTasks);

        var failOpenResponses =
            responses.Take(requestsPerInstance);

        var failClosedResponses =
            responses.Skip(requestsPerInstance);

        Assert.All(
            failOpenResponses,
            response =>
                Assert.Equal(
                    HttpStatusCode.OK,
                    response.StatusCode));

        Assert.All(
            failClosedResponses,
            response =>
                Assert.Equal(
                    HttpStatusCode.ServiceUnavailable,
                    response.StatusCode));
    }
}
