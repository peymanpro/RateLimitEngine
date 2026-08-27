using RateLimitEngine.Redis.Gcra;
using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.IntegrationTests.Redis;

public sealed class RedisGcraStoreTests
{
    [Fact]
    public async Task EvaluateAsync_ShouldAllowInitialBurst()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var store = new RedisGcraStore(
            new RedisScriptExecutor(connection.GetDatabase()));

        var key = $"integration-gcra-{Guid.NewGuid():N}";

        var interval = TimeSpan.FromSeconds(1);
        var burstTolerance = TimeSpan.FromSeconds(4);

        var first = await store.EvaluateAsync(
            key,
            interval,
            burstTolerance,
            cost: 1);

        var second = await store.EvaluateAsync(
            key,
            interval,
            burstTolerance,
            cost: 1);

        var third = await store.EvaluateAsync(
            key,
            interval,
            burstTolerance,
            cost: 1);

        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
        Assert.True(third.Accepted);

        Assert.Null(first.RetryAfter);
        Assert.Null(second.RetryAfter);
        Assert.Null(third.RetryAfter);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldRejectWhenOutsideTolerance()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var store = new RedisGcraStore(
            new RedisScriptExecutor(connection.GetDatabase()));

        var key = $"integration-gcra-{Guid.NewGuid():N}";

        var interval = TimeSpan.FromSeconds(1);
        var burstTolerance = TimeSpan.Zero;

        var first = await store.EvaluateAsync(
            key,
            interval,
            burstTolerance,
            cost: 1);

        var second = await store.EvaluateAsync(
            key,
            interval,
            burstTolerance,
            cost: 1);

        Assert.True(first.Accepted);
        Assert.False(second.Accepted);
        Assert.NotNull(second.RetryAfter);
        Assert.True(second.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldAllowAfterRequiredDelay()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var store = new RedisGcraStore(
            new RedisScriptExecutor(connection.GetDatabase()));

        var key = $"integration-gcra-{Guid.NewGuid():N}";

        var interval = TimeSpan.FromMilliseconds(500);
        var burstTolerance = TimeSpan.Zero;

        var first = await store.EvaluateAsync(
            key,
            interval,
            burstTolerance,
            cost: 1);

        Assert.True(first.Accepted);

        await Task.Delay(TimeSpan.FromMilliseconds(650));

        var second = await store.EvaluateAsync(
            key,
            interval,
            burstTolerance,
            cost: 1);

        Assert.True(second.Accepted);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldRespectRequestCost()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var store = new RedisGcraStore(
            new RedisScriptExecutor(connection.GetDatabase()));

        var key = $"integration-gcra-{Guid.NewGuid():N}";

        var interval = TimeSpan.FromSeconds(1);
        var burstTolerance = TimeSpan.FromSeconds(4);

        var decision = await store.EvaluateAsync(
            key,
            interval,
            burstTolerance,
            cost: 3);

        Assert.True(decision.Accepted);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldRemainAtomicAcrossIndependentConnections()
    {
        await using var connectionOne =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        await using var connectionTwo =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var storeOne = new RedisGcraStore(
            new RedisScriptExecutor(connectionOne.GetDatabase()));

        var storeTwo = new RedisGcraStore(
            new RedisScriptExecutor(connectionTwo.GetDatabase()));

        var key = $"integration-gcra-{Guid.NewGuid():N}";

        var interval = TimeSpan.FromMilliseconds(100);
        var burstTolerance = TimeSpan.FromSeconds(10);

        var tasksOne = Enumerable.Range(0, 500)
            .Select(_ =>
                storeOne.EvaluateAsync(
                    key,
                    interval,
                    burstTolerance,
                    cost: 1)
                    .AsTask())
            .ToArray();

        var tasksTwo = Enumerable.Range(0, 500)
            .Select(_ =>
                storeTwo.EvaluateAsync(
                    key,
                    interval,
                    burstTolerance,
                    cost: 1)
                    .AsTask())
            .ToArray();

        var results = await Task.WhenAll(
            tasksOne.Concat(tasksTwo));

        var acceptedCount =
            results.Count(static result => result.Accepted);

        Assert.InRange(
            acceptedCount,
            1,
            101);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldKeepKeysIndependent()
    {
        await using var connection =
            await ConnectionMultiplexer.ConnectAsync("localhost:6379");

        var store = new RedisGcraStore(
            new RedisScriptExecutor(connection.GetDatabase()));

        var firstKey = $"integration-gcra-{Guid.NewGuid():N}";
        var secondKey = $"integration-gcra-{Guid.NewGuid():N}";

        var first = await store.EvaluateAsync(
            firstKey,
            TimeSpan.FromSeconds(1),
            TimeSpan.Zero,
            cost: 1);

        var second = await store.EvaluateAsync(
            secondKey,
            TimeSpan.FromSeconds(1),
            TimeSpan.Zero,
            cost: 1);

        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
    }
}
