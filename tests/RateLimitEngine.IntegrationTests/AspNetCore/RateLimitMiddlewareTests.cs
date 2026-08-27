using System.Net;
using Microsoft.AspNetCore.Http;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;

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

    [Fact]
    public async Task Middleware_ShouldFailOpenWhenLimiterFails()
    {
        var options =
            new RateLimitEngine.AspNetCore.RateLimitOptions
            {
                FailureStrategy =
                    RateLimitEngine.AspNetCore.RateLimitFailureStrategy.FailOpen
            };

        var factory =
            new ThrowingRateLimiterFactory();

        var keyResolver =
            new StaticRateLimitKeyResolver();

        var nextCalled = false;

        var middleware =
            new RateLimitEngine.AspNetCore.RateLimitMiddleware(
                _ =>
                {
                    nextCalled = true;
                    return Task.CompletedTask;
                },
                factory,
                keyResolver,
                options);

        var context =
            new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);

        Assert.NotEqual(
            StatusCodes.Status503ServiceUnavailable,
            context.Response.StatusCode);
    }

    [Fact]
    public async Task Middleware_ShouldFailClosedWhenLimiterFails()
    {
        var options =
            new RateLimitEngine.AspNetCore.RateLimitOptions
            {
                FailureStrategy =
                    RateLimitEngine.AspNetCore.RateLimitFailureStrategy.FailClosed
            };

        var factory =
            new ThrowingRateLimiterFactory();

        var keyResolver =
            new StaticRateLimitKeyResolver();

        var nextCalled = false;

        var middleware =
            new RateLimitEngine.AspNetCore.RateLimitMiddleware(
                _ =>
                {
                    nextCalled = true;
                    return Task.CompletedTask;
                },
                factory,
                keyResolver,
                options);

        var context =
            new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);

        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            context.Response.StatusCode);
    }

    private sealed class ThrowingRateLimiterFactory
        : IRateLimiterFactory
    {
        public IRateLimiter Create(
            RateLimitAlgorithm algorithm)
        {
            return new ThrowingRateLimiter();
        }
    }

    private sealed class ThrowingRateLimiter
        : IRateLimiter
    {
        public ValueTask<RateLimitDecision> EvaluateAsync(
            RateLimitRequest request,
            RateLimitPolicy policy,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Simulated rate limiter failure.");
        }
    }

    private sealed class StaticRateLimitKeyResolver
        : RateLimitEngine.AspNetCore.IRateLimitKeyResolver
    {
        public string Resolve(
            HttpContext context)
        {
            return "integration-test";
        }
    }
}
