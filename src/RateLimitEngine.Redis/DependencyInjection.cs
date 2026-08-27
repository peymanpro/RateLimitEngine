using Microsoft.Extensions.DependencyInjection;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Redis.FixedWindow;
using RateLimitEngine.Redis.Infrastructure;

namespace RateLimitEngine.Redis;

public static class DependencyInjection
{
    public static IServiceCollection AddRateLimitEngineRedis(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IRedisScriptExecutor, RedisScriptExecutor>();
        services.AddSingleton<IFixedWindowStore, RedisFixedWindowStore>();

        return services;
    }
}
