using StackExchange.Redis;

namespace RateLimitEngine.Redis.Infrastructure;

public interface IRedisScriptExecutor
{
    Task<RedisResult> ExecuteAsync(
        string script,
        RedisKey[] keys,
        RedisValue[] values,
        CancellationToken cancellationToken = default);
}
