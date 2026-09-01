using Microsoft.Extensions.DependencyInjection;
using RateLimitEngine.Algorithms.TokenBucket;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Redis.FixedWindow;
using RateLimitEngine.Redis.Gcra;
using RateLimitEngine.Redis.Infrastructure;
using RateLimitEngine.Redis.SlidingWindow;
using RateLimitEngine.Redis.TokenBucket;
using StackExchange.Redis;

namespace RateLimitEngine.Redis;

public static class DependencyInjection
{
    public static IServiceCollection AddRateLimitEngineRedis(
        this IServiceCollection services,
        IDatabase database,
        TokenBucketOptions? tokenBucketOptions = null,
        RedisRetryOptions? retryOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(database);

        services.AddSingleton(database);

        services.AddSingleton(
            retryOptions ?? new RedisRetryOptions());

        services.AddSingleton<RedisScriptExecutor>();

        services.AddSingleton<IRedisScriptExecutor>(
            provider =>
                new RetryingRedisScriptExecutor(
                    provider.GetRequiredService<RedisScriptExecutor>(),
                    provider.GetRequiredService<RedisRetryOptions>()));

        services.AddSingleton<IFixedWindowStore,
            RedisFixedWindowStore>();

        services.AddSingleton<ITokenBucketStore,
            RedisTokenBucketStore>();

        services.AddSingleton<ISlidingWindowStore,
            RedisSlidingWindowStore>();

        services.AddSingleton<IGcraStore,
            RedisGcraStore>();

        services.AddSingleton(
            tokenBucketOptions ??
            new TokenBucketOptions(capacity: 100));

        services.AddSingleton<RedisRateLimiterFactory>();

        services.AddSingleton<IRateLimiterFactory>(
            provider =>
                provider.GetRequiredService<
                    RedisRateLimiterFactory>());

        return services;
    }
}
