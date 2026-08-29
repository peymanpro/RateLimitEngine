using RateLimitEngine.Redis.Infrastructure;
using RateLimitEngine.Redis.TokenBucket;
using StackExchange.Redis;

namespace RateLimitEngine.IntegrationTests.Redis;

public sealed class RedisTokenBucketStoreTests
{
    [Fact]
    public async Task ConsumeAsync_ShouldStartWithFullCapacity()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var store = new RedisTokenBucketStore(
            new RedisScriptExecutor(connection.GetDatabase()));

        var key = $"integration-token-bucket-{Guid.NewGuid():N}";

        var result = await store.ConsumeAsync(
            key,
            capacity: 10,
            refillRate: 1,
            cost: 3);

        Assert.True(result.Accepted);
        Assert.Equal(7, result.RemainingTokens, precision: 6);
        Assert.Null(result.RetryAfter);
    }

    [Fact]
    public async Task ConsumeAsync_ShouldRejectWhenTokensAreInsufficient()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var store = new RedisTokenBucketStore(
            new RedisScriptExecutor(connection.GetDatabase()));

        var key = $"integration-token-bucket-{Guid.NewGuid():N}";

        var first = await store.ConsumeAsync(
            key,
            capacity: 5,
            refillRate: 1,
            cost: 5);

        var second = await store.ConsumeAsync(
            key,
            capacity: 5,
            refillRate: 1,
            cost: 1);

        Assert.True(first.Accepted);
        Assert.False(second.Accepted);
        Assert.Equal(0, second.RemainingTokens, precision: 6);
        Assert.NotNull(second.RetryAfter);
        Assert.True(second.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public async Task ConsumeAsync_ShouldRefillTokensOverTime()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var store = new RedisTokenBucketStore(
            new RedisScriptExecutor(connection.GetDatabase()));

        var key = $"integration-token-bucket-{Guid.NewGuid():N}";

        var first = await store.ConsumeAsync(
            key,
            capacity: 10,
            refillRate: 2,
            cost: 10);

        Assert.True(first.Accepted);

        await Task.Delay(TimeSpan.FromSeconds(2));

        var second = await store.ConsumeAsync(
            key,
            capacity: 10,
            refillRate: 2,
            cost: 3);

        Assert.True(second.Accepted);
    }

    [Fact]
    public async Task ConsumeAsync_ShouldRemainAtomicAcrossConcurrentCalls()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var store = new RedisTokenBucketStore(
            new RedisScriptExecutor(connection.GetDatabase()));

        var key = $"integration-token-bucket-{Guid.NewGuid():N}";

        var tasks = Enumerable.Range(0, 1_000)
            .Select(_ =>
                store.ConsumeAsync(
                    key,
                    capacity: 100,
                    refillRate: 0.000001,
                    cost: 1)
                    .AsTask())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(
            100,
            results.Count(static result => result.Accepted));

        Assert.Equal(
            900,
            results.Count(static result => !result.Accepted));
    }

    [Fact]
    public async Task ConsumeAsync_ShouldKeepKeysIndependent()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var store = new RedisTokenBucketStore(
            new RedisScriptExecutor(connection.GetDatabase()));

        var firstKey = $"integration-token-bucket-{Guid.NewGuid():N}";
        var secondKey = $"integration-token-bucket-{Guid.NewGuid():N}";

        var first = await store.ConsumeAsync(
            firstKey,
            capacity: 1,
            refillRate: 1,
            cost: 1);

        var second = await store.ConsumeAsync(
            secondKey,
            capacity: 1,
            refillRate: 1,
            cost: 1);

        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
    }
}
