using RateLimitEngine.Redis.FixedWindow;
using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.IntegrationTests.Redis;

public sealed class RedisFixedWindowStoreTests
{
    [Fact]
    public async Task IncrementAsync_ShouldEnforceLimitAtomically()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var database = connection.GetDatabase();
        var executor = new RedisScriptExecutor(database);
        var store = new RedisFixedWindowStore(executor);

        var key = $"integration-client-{Guid.NewGuid():N}";
        const int limit = 100;

        var tasks = Enumerable.Range(0, 1_000)
            .Select(_ =>
                store.IncrementAsync(
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

        Assert.Equal(
            900,
            results.Count(static result => !result.Accepted));

        Assert.All(
            results,
            result => Assert.InRange(
                result.Remaining,
                0,
                limit));
    }

    [Fact]
    public async Task IncrementAsync_ShouldPreserveStateAcrossCalls()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var database = connection.GetDatabase();
        var executor = new RedisScriptExecutor(database);
        var store = new RedisFixedWindowStore(executor);

        var key = $"integration-client-{Guid.NewGuid():N}";

        var first = await store.IncrementAsync(
            key,
            TimeSpan.FromMinutes(1),
            permitLimit: 3,
            cost: 1);

        var second = await store.IncrementAsync(
            key,
            TimeSpan.FromMinutes(1),
            permitLimit: 3,
            cost: 1);

        var third = await store.IncrementAsync(
            key,
            TimeSpan.FromMinutes(1),
            permitLimit: 3,
            cost: 1);

        var fourth = await store.IncrementAsync(
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
    public async Task IncrementAsync_ShouldKeepKeysIndependent()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var database = connection.GetDatabase();
        var executor = new RedisScriptExecutor(database);
        var store = new RedisFixedWindowStore(executor);

        var clientOneKey = $"integration-client-{Guid.NewGuid():N}";
        var clientTwoKey = $"integration-client-{Guid.NewGuid():N}";

        var clientOne = await store.IncrementAsync(
            clientOneKey,
            TimeSpan.FromMinutes(1),
            permitLimit: 1,
            cost: 1);

        var clientTwo = await store.IncrementAsync(
            clientTwoKey,
            TimeSpan.FromMinutes(1),
            permitLimit: 1,
            cost: 1);

        Assert.True(clientOne.Accepted);
        Assert.True(clientTwo.Accepted);
    }

    [Fact]
    public async Task IncrementAsync_ShouldRemainAtomicAcrossIndependentConnections()
    {
        await using var connectionOne =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        await using var connectionTwo =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var storeOne =
            new RedisFixedWindowStore(
                new RedisScriptExecutor(connectionOne.GetDatabase()));

        var storeTwo =
            new RedisFixedWindowStore(
                new RedisScriptExecutor(connectionTwo.GetDatabase()));

        var key = $"integration-client-{Guid.NewGuid():N}";
        const int limit = 100;

        var tasksOne = Enumerable.Range(0, 500)
            .Select(_ =>
                storeOne.IncrementAsync(
                    key,
                    TimeSpan.FromMinutes(1),
                    limit,
                    1)
                    .AsTask())
            .ToArray();

        var tasksTwo = Enumerable.Range(0, 500)
            .Select(_ =>
                storeTwo.IncrementAsync(
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
}
