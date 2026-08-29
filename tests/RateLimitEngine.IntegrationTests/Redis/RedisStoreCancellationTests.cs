using RateLimitEngine.Redis.FixedWindow;
using RateLimitEngine.Redis.Gcra;
using RateLimitEngine.Redis.Infrastructure;
using RateLimitEngine.Redis.SlidingWindow;
using RateLimitEngine.Redis.TokenBucket;
using StackExchange.Redis;

namespace RateLimitEngine.IntegrationTests.Redis;

public sealed class RedisStoreCancellationTests
{
    [Fact]
    public async Task FixedWindow_ShouldHonorPreCancelledToken()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync(
                "localhost:6379");

        var store =
            new RedisFixedWindowStore(
                new RedisScriptExecutor(
                    connection.GetDatabase()));

        var key =
            $"cancel-fixed-window-{Guid.NewGuid():N}";

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await store.IncrementAsync(
                    key,
                    TimeSpan.FromMinutes(1),
                    permitLimit: 5,
                    cost: 1,
                    cancellationToken: cancellation.Token));

        var result = await store.IncrementAsync(
            key,
            TimeSpan.FromMinutes(1),
            permitLimit: 5,
            cost: 1);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.Consumed);
    }

    [Fact]
    public async Task SlidingWindow_ShouldHonorPreCancelledToken()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync(
                "localhost:6379");

        var store =
            new RedisSlidingWindowStore(
                new RedisScriptExecutor(
                    connection.GetDatabase()));

        var key =
            $"cancel-sliding-window-{Guid.NewGuid():N}";

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await store.EvaluateAsync(
                    key,
                    TimeSpan.FromMinutes(1),
                    permitLimit: 5,
                    cost: 1,
                    cancellationToken: cancellation.Token));

        var result = await store.EvaluateAsync(
            key,
            TimeSpan.FromMinutes(1),
            permitLimit: 5,
            cost: 1);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.Consumed);
    }

    [Fact]
    public async Task Gcra_ShouldHonorPreCancelledToken()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync(
                "localhost:6379");

        var store =
            new RedisGcraStore(
                new RedisScriptExecutor(
                    connection.GetDatabase()));

        var key =
            $"cancel-gcra-{Guid.NewGuid():N}";

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await store.EvaluateAsync(
                    key,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(3),
                    cost: 1,
                    cancellationToken: cancellation.Token));

        var result = await store.EvaluateAsync(
            key,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(3),
            cost: 1);

        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task TokenBucket_ShouldHonorPreCancelledToken()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync(
                "localhost:6379");

        var store =
            new RedisTokenBucketStore(
                new RedisScriptExecutor(
                    connection.GetDatabase()));

        var key =
            $"cancel-token-bucket-{Guid.NewGuid():N}";

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await store.ConsumeAsync(
                    key,
                    capacity: 5,
                    refillRate: 1,
                    cost: 1,
                    cancellationToken: cancellation.Token));

        var result = await store.ConsumeAsync(
            key,
            capacity: 5,
            refillRate: 1,
            cost: 1);

        Assert.True(result.Accepted);
    }
}
