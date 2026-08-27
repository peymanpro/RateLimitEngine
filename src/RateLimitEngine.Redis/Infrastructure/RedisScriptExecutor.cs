using StackExchange.Redis;

namespace RateLimitEngine.Redis.Infrastructure;

public sealed class RedisScriptExecutor : IRedisScriptExecutor
{
    private readonly IDatabase _database;

    public RedisScriptExecutor(IDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public async Task<RedisResult> ExecuteAsync(
        string script,
        RedisKey[] keys,
        RedisValue[] values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(values);

        cancellationToken.ThrowIfCancellationRequested();

        return await _database.ScriptEvaluateAsync(
            script,
            keys,
            values);
    }
}
