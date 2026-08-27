using Microsoft.Extensions.DependencyInjection;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Redis.FixedWindow;
using RateLimitEngine.Redis.Gcra;
using RateLimitEngine.Redis.Infrastructure;
using RateLimitEngine.Redis.SlidingWindow;
using RateLimitEngine.Redis.TokenBucket;

namespace RateLimitEngine.Redis;

public static class DependencyInjection
{
    public static IServiceCollection AddRateLimitEngineRedis(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IRedisScriptExecutor, RedisScriptExecutor>();
        services.AddSingleton<IFixedWindowStore, RedisFixedWindowStore>();
        services.AddSingleton<ITokenBucketStore, RedisTokenBucketStore>();
        services.AddSingleton<ISlidingWindowStore, RedisSlidingWindowStore>();
        services.AddSingleton<IGcraStore, RedisGcraStore>();

        return services;
    }
}


