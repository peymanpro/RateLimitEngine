using RateLimitEngine.Redis.FixedWindow;
using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.DistributedTests.Redis;

public sealed class ConcurrentSharedRedisStateTests
{
    [Fact]
    public async Task TwoIndependentInstances_ShouldEnforceLimitAtomicallyUnderConcurrency()
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
            $"distributed-concurrent-{Guid.NewGuid():N}";

        const int limit = 100;
        var window = TimeSpan.FromSeconds(30);
        const int totalRequests = 1000;

        var tasks = Enumerable
            .Range(0, totalRequests)
            .Select(async index =>
            {
                var store = index % 2 == 0
                    ? storeA
                    : storeB;

                return await store.IncrementAsync(
                    key,
                    window,
                    limit,
                    cost: 1);
            })
            .ToArray();

        var results = await Task.WhenAll(tasks);

        var acceptedCount =
            results.Count(result => result.Accepted);

        var rejectedCount =
            results.Count(result => !result.Accepted);

        Assert.Equal(limit, acceptedCount);
        Assert.Equal(
            totalRequests - limit,
            rejectedCount);

        Assert.All(
            results.Where(result => !result.Accepted),
            result => Assert.Equal(0, result.Remaining));
    }
}
