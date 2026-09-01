using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
        var logger = new TestLogger();

        var middleware =
            new RateLimitEngine.AspNetCore.RateLimitMiddleware(
                _ =>
                {
                    nextCalled = true;
                    return Task.CompletedTask;
                },
                factory,
                keyResolver,
                options,
                logger);

        var context =
            new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);

        Assert.NotEqual(
            StatusCodes.Status503ServiceUnavailable,
            context.Response.StatusCode);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("Failing open", entry.Message);
        Assert.Contains("Algorithm=FixedWindow", entry.Message);
        Assert.Contains("Backend=InMemory", entry.Message);
        Assert.NotNull(entry.Exception);
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
        var logger = new TestLogger();

        var middleware =
            new RateLimitEngine.AspNetCore.RateLimitMiddleware(
                _ =>
                {
                    nextCalled = true;
                    return Task.CompletedTask;
                },
                factory,
                keyResolver,
                options,
                logger);

        var context =
            new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);

        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            context.Response.StatusCode);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("Failing closed", entry.Message);
        Assert.Contains("Algorithm=FixedWindow", entry.Message);
        Assert.Contains("Backend=InMemory", entry.Message);
        Assert.NotNull(entry.Exception);
    }

    [Fact]
    public async Task Middleware_ShouldLogWarningWhenRequestIsRejected()
    {
        var options =
            new RateLimitEngine.AspNetCore.RateLimitOptions();

        var logger = new TestLogger();

        var middleware =
            new RateLimitEngine.AspNetCore.RateLimitMiddleware(
                _ => Task.CompletedTask,
                new RejectingRateLimiterFactory(),
                new StaticRateLimitKeyResolver(),
                options,
                logger);

        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(
            StatusCodes.Status429TooManyRequests,
            context.Response.StatusCode);

        var entry = Assert.Single(logger.Entries);

        Assert.Equal(
            LogLevel.Warning,
            entry.Level);

        Assert.Contains(
            "Rate limit rejected request",
            entry.Message);

        Assert.Contains(
            "Algorithm=FixedWindow",
            entry.Message);

        Assert.Contains(
            "Backend=InMemory",
            entry.Message);

        Assert.Contains(
            "Remaining=0",
            entry.Message);
    }

    private sealed class RejectingRateLimiterFactory
        : IRateLimiterFactory
    {
        public IRateLimiter Create(
            RateLimitAlgorithm algorithm)
        {
            return new RejectingRateLimiter();
        }
    }

    private sealed class RejectingRateLimiter
        : IRateLimiter
    {
        public ValueTask<RateLimitDecision> EvaluateAsync(
            RateLimitRequest request,
            RateLimitPolicy policy,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(
                new RateLimitDecision(
                    allowed: false,
                    limit: 10,
                    remaining: 0,
                    retryAfter: TimeSpan.FromSeconds(1)));
        }
    }

    private sealed class TestLogger
        : ILogger<RateLimitEngine.AspNetCore.RateLimitMiddleware>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(
                new LogEntry(
                    logLevel,
                    formatter(state, exception),
                    exception));
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        Exception? Exception);

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
