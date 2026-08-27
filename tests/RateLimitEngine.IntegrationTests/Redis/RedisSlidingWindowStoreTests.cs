using RateLimitEngine.Redis.Infrastructure;
using RateLimitEngine.Redis.SlidingWindow;
using StackExchange.Redis;

namespace RateLimitEngine.IntegrationTests.Redis;

public sealed class RedisSlidingWindowStoreTests
{
    [Fact]
    public async Task EvaluateAsync_ShouldPreserveStateAcrossCalls()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var store = new RedisSlidingWindowStore(
            new RedisScriptExecutor(connection.GetDatabase()));

        var key = $"integration-sliding-window-{Guid.NewGuid():N}";

        var first = await store.EvaluateAsync(
            key,
            TimeSpan.FromMinutes(1),
            permitLimit: 3,
            cost: 1);

        var second = await store.EvaluateAsync(
            key,
            TimeSpan.FromMinutes(1),
            permitLimit: 3,
            cost: 1);

        var third = await store.EvaluateAsync(
            key,
            TimeSpan.FromMinutes(1),
            permitLimit: 3,
            cost: 1);

        var fourth = await store.EvaluateAsync(
            key,
            TimeSpan.FromMinutes(1),
            permitLimit: 3,
            cost: 1);

        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
        Assert.True(third.Accepted);
        Assert.False(fourth.Accepted);

        Assert.Equal(2, first.Remaining);
        Assert.Equal(1, second.Remaining);
        Assert.Equal(0, third.Remaining);
        Assert.Equal(0, fourth.Remaining);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldExpireRequestsOutsideSlidingWindow()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var store = new RedisSlidingWindowStore(
            new RedisScriptExecutor(connection.GetDatabase()));

        var key = $"integration-sliding-window-{Guid.NewGuid():N}";

        var first = await store.EvaluateAsync(
            key,
            TimeSpan.FromSeconds(2),
            permitLimit: 1,
            cost: 1);

        Assert.True(first.Accepted);

        await Task.Delay(TimeSpan.FromSeconds(2.2));

        var second = await store.EvaluateAsync(
            key,
            TimeSpan.FromSeconds(2),
            permitLimit: 1,
            cost: 1);

        Assert.True(second.Accepted);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldRespectRequestCost()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var store = new RedisSlidingWindowStore(
            new RedisScriptExecutor(connection.GetDatabase()));

        var key = $"integration-sliding-window-{Guid.NewGuid():N}";

        var first = await store.EvaluateAsync(
            key,
            TimeSpan.FromMinutes(1),
            permitLimit: 5,
            cost: 3);

        var second = await store.EvaluateAsync(
            key,
            TimeSpan.FromMinutes(1),
            permitLimit: 5,
            cost: 3);

        Assert.True(first.Accepted);
        Assert.False(second.Accepted);
        Assert.Equal(2, second.Remaining);
        Assert.NotNull(second.RetryAfter);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldRemainAtomicAcrossConcurrentCalls()
    {
        await using var connectionOne =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        await using var connectionTwo =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var storeOne = new RedisSlidingWindowStore(
            new RedisScriptExecutor(connectionOne.GetDatabase()));

        var storeTwo = new RedisSlidingWindowStore(
            new RedisScriptExecutor(connectionTwo.GetDatabase()));

        var key = $"integration-sliding-window-{Guid.NewGuid():N}";
        const int limit = 100;

        var tasksOne = Enumerable.Range(0, 500)
            .Select(_ =>
                storeOne.EvaluateAsync(
                    key,
                    TimeSpan.FromMinutes(1),
                    limit,
                    1)
                    .AsTask())
            .ToArray();

        var tasksTwo = Enumerable.Range(0, 500)
            .Select(_ =>
                storeTwo.EvaluateAsync(
                    key,
                    TimeSpan.FromMinutes(1),
                    limit,
                    1)
                    .AsTask())
            .ToArray();

        var results = await Task.WhenAll(
            tasksOne.Concat(tasksTwo));

        Assert.Equal(
            limit,
            results.Count(static result => result.Accepted));

        Assert.Equal(
            900,
            results.Count(static result => !result.Accepted));
    }

    [Fact]
    public async Task EvaluateAsync_ShouldKeepKeysIndependent()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var store = new RedisSlidingWindowStore(
            new RedisScriptExecutor(connection.GetDatabase()));

        var firstKey = $"integration-sliding-window-{Guid.NewGuid():N}";
        var secondKey = $"integration-sliding-window-{Guid.NewGuid():N}";

        var first = await store.EvaluateAsync(
            firstKey,
            TimeSpan.FromMinutes(1),
            permitLimit: 1,
            cost: 1);

        var second = await store.EvaluateAsync(
            secondKey,
            TimeSpan.FromMinutes(1),
            permitLimit: 1,
            cost: 1);

        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldKeepEntriesUniqueWhenTimestampsCollide()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var database = connection.GetDatabase();

        var store = new RedisSlidingWindowStore(
            new RedisScriptExecutor(database));

        var key = $"integration-sliding-window-{Guid.NewGuid():N}";
        const int limit = 100;

        var tasks = Enumerable.Range(0, limit)
            .Select(_ =>
                store.EvaluateAsync(
                    key,
                    TimeSpan.FromMinutes(1),
                    limit,
                    1)
                    .AsTask())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(
            limit,
            results.Count(static result => result.Accepted));

        var entriesKey =
            $"ratelimit:sliding-window:{key}:entries";

        var entryCount =
            await database.SortedSetLengthAsync(entriesKey);

        Assert.Equal(limit, entryCount);
    }
}

