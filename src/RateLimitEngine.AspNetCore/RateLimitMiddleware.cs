using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;

namespace RateLimitEngine.AspNetCore;

public sealed class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimiterFactory _factory;
    private readonly IRateLimitKeyResolver _keyResolver;
    private readonly RateLimitOptions _options;
    private readonly ILogger<RateLimitMiddleware> _logger;

    public RateLimitMiddleware(
        RequestDelegate next,
        IRateLimiterFactory factory,
        IRateLimitKeyResolver keyResolver,
        RateLimitOptions options,
        ILogger<RateLimitMiddleware>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(keyResolver);
        ArgumentNullException.ThrowIfNull(options);

        _next = next;
        _factory = factory;
        _keyResolver = keyResolver;
        _options = options;
        _logger = logger ?? NullLogger<RateLimitMiddleware>.Instance;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var limiter = _factory.Create(_options.Algorithm);

        var key = _keyResolver.Resolve(context);

        var request = new RateLimitRequest(
            key,
            _options.Cost);

        var policy = new RateLimitPolicy(
            _options.PermitLimit,
            _options.Window);

        RateLimitDecision decision;

        try
        {
            decision = await limiter.EvaluateAsync(
                request,
                policy,
                context.RequestAborted);
        }
        catch (OperationCanceledException)
            when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (_options.FailureStrategy ==
                RateLimitFailureStrategy.FailOpen)
            {
                _logger.LogError(
                    exception,
                    "Rate limit evaluation failed. Failing open. Algorithm={Algorithm}, Backend={Backend}",
                    _options.Algorithm,
                    _options.Backend);

                await _next(context);
                return;
            }

            _logger.LogError(
                exception,
                "Rate limit evaluation failed. Failing closed. Algorithm={Algorithm}, Backend={Backend}",
                _options.Algorithm,
                _options.Backend);

            context.Response.StatusCode =
                StatusCodes.Status503ServiceUnavailable;

            return;
        }

        WriteHeaders(context.Response, decision);

        if (!decision.Allowed)
        {
            _logger.LogWarning(
                "Rate limit rejected request. Algorithm={Algorithm}, Backend={Backend}, Remaining={Remaining}",
                _options.Algorithm,
                _options.Backend,
                decision.Remaining);

            context.Response.StatusCode =
                StatusCodes.Status429TooManyRequests;

            return;
        }

        await _next(context);
    }

    private static void WriteHeaders(
        HttpResponse response,
        RateLimitDecision decision)
    {
        response.Headers["X-RateLimit-Limit"] =
            decision.Limit.ToString();

        response.Headers["X-RateLimit-Remaining"] =
            decision.Remaining.ToString();

        if (decision.ResetAfter is { } resetAfter)
        {
            response.Headers["X-RateLimit-Reset-After"] =
                Math.Ceiling(
                    resetAfter.TotalSeconds)
                .ToString();
        }

        if (!decision.Allowed &&
            decision.RetryAfter is { } retryAfter)
        {
            response.Headers.RetryAfter =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        retryAfter.TotalSeconds))
                .ToString();
        }
    }
}
