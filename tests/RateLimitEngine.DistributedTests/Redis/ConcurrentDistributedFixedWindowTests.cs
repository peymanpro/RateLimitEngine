using RateLimitEngine.Redis.FixedWindow;
using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.DistributedTests.Redis;

public sealed class ConcurrentDistributedFixedWindowTests
{
    [Fact]
    public async Task TwoIndependentInstances_ShouldEnforceLimitUnderConcurrentLoad()
    {
        await using var connectionA =
            await ConnectionMultiplexer.ConnectAsync(
                "localhost:6379");

        await using var connectionB =
            await ConnectionMultiplexer.ConnectAsync(
                "localhost:6379");

        var storeA = new RedisFixedWindowStore(
            new RedisScriptExecutor(
                connectionA.GetDatabase()));

        var storeB = new RedisFixedWindowStore(
            new RedisScriptExecutor(
                connectionB.GetDatabase()));

        var key =
            $"distributed-concurrent-fixed-window-{Guid.NewGuid():N}";

        const int limit = 100;
        const int totalRequests = 1000;

        var window = TimeSpan.FromSeconds(30);

        var tasks = Enumerable.Range(0, totalRequests)
            .Select(index =>
                (index % 2 == 0
                    ? storeA.IncrementAsync(
                        key,
                        window,
                        limit,
                        cost: 1)
                    : storeB.IncrementAsync(
                        key,
                        window,
                        limit,
                        cost: 1))
                .AsTask())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        var accepted = results.Count(result => result.Accepted);
        var rejected = results.Count(result => !result.Accepted);

        Assert.Equal(limit, accepted);
        Assert.Equal(totalRequests - limit, rejected);

        Assert.All(
            results.Where(result => !result.Accepted),
            result => Assert.Equal(0, result.Remaining));
    }
}
