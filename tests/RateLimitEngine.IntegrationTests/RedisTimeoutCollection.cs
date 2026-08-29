using Xunit;

namespace RateLimitEngine.IntegrationTests;

[CollectionDefinition(
    "Redis Timeout Collection",
    DisableParallelization = true)]
public sealed class RedisTimeoutCollection
{
}
