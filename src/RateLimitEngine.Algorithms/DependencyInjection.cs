using Microsoft.Extensions.DependencyInjection;
using RateLimitEngine.Algorithms.TokenBucket;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Time;

namespace RateLimitEngine.Algorithms;

public static class DependencyInjection
{
    public static IServiceCollection AddRateLimitEngineInMemory(
        this IServiceCollection services,
        TokenBucketOptions? tokenBucketOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IClock, SystemClock>();

        services.AddSingleton(
            tokenBucketOptions ?? new TokenBucketOptions(capacity: 100));

        services.AddSingleton<IRateLimiterFactory, InMemoryRateLimiterFactory>();

        return services;
    }
}
