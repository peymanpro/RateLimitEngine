using Microsoft.Extensions.DependencyInjection;
using RateLimitEngine.Algorithms;
using RateLimitEngine.Algorithms.TokenBucket;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Time;
using RateLimitEngine.Redis;
using StackExchange.Redis;

namespace RateLimitEngine.UnitTests.Factory;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddRateLimitEngineInMemory_ShouldRegisterFactory()
    {
        var services = new ServiceCollection();

        services.AddRateLimitEngineInMemory();

        using var provider = services.BuildServiceProvider();

        var factory =
            provider.GetRequiredService<IRateLimiterFactory>();

        Assert.IsType<InMemoryRateLimiterFactory>(factory);
    }

    [Fact]
    public void AddRateLimitEngineInMemory_ShouldRegisterClock()
    {
        var services = new ServiceCollection();

        services.AddRateLimitEngineInMemory();

        using var provider = services.BuildServiceProvider();

        var clock =
            provider.GetRequiredService<IClock>();

        Assert.IsType<SystemClock>(clock);
    }

    [Fact]
    public void AddRateLimitEngineInMemory_ShouldAllowCustomTokenBucketOptions()
    {
        var services = new ServiceCollection();

        services.AddRateLimitEngineInMemory(
            new TokenBucketOptions(capacity: 500));

        using var provider = services.BuildServiceProvider();

        var options =
            provider.GetRequiredService<TokenBucketOptions>();

        Assert.Equal(500, options.Capacity);
    }

    [Fact]
    public void AddRateLimitEngineRedis_ShouldRegisterFactory()
    {
        var services = new ServiceCollection();

        var multiplexer =
            ConnectionMultiplexer.Connect("localhost:6379");

        var database =
            multiplexer.GetDatabase();

        services.AddRateLimitEngineRedis(database);

        using var provider = services.BuildServiceProvider();

        var factory =
            provider.GetRequiredService<IRateLimiterFactory>();

        Assert.IsType<RedisRateLimiterFactory>(factory);

        multiplexer.Dispose();
    }
}
