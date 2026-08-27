using Microsoft.Extensions.DependencyInjection;
using RateLimitEngine.Algorithms;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Redis;

namespace RateLimitEngine.AspNetCore;

public sealed class RateLimiterFactoryProvider : IRateLimiterFactoryProvider
{
    private readonly IServiceProvider _services;

    public RateLimiterFactoryProvider(
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _services = services;
    }

    public IRateLimiterFactory GetFactory(
        RateLimitBackend backend)
    {
        return backend switch
        {
            RateLimitBackend.InMemory =>
                _services.GetRequiredService<
                    InMemoryRateLimiterFactory>(),

            RateLimitBackend.Redis =>
                _services.GetRequiredService<
                    RedisRateLimiterFactory>(),

            _ => throw new ArgumentOutOfRangeException(
                nameof(backend),
                backend,
                "Unsupported rate limiter backend.")
        };
    }
}
