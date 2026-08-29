using Xunit;

namespace RateLimitEngine.DistributedTests.Redis;

[CollectionDefinition(
    "Redis Docker Collection",
    DisableParallelization = true)]
public sealed class RedisDockerCollection
    : ICollectionFixture<RedisDockerFixture>
{
}
