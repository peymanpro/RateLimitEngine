using StackExchange.Redis;

namespace RateLimitEngine.Redis.Infrastructure;

public sealed class RetryingRedisScriptExecutor : IRedisScriptExecutor
{
    private readonly IRedisScriptExecutor _inner;
    private readonly RedisRetryOptions _options;

    public RetryingRedisScriptExecutor(
        IRedisScriptExecutor inner,
        RedisRetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxRetryAttempts < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaxRetryAttempts cannot be negative.");
        }

        if (options.RetryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "RetryDelay cannot be negative.");
        }

        _inner = inner;
        _options = options;
    }

    public async Task<RedisResult> ExecuteAsync(
        string script,
        RedisKey[] keys,
        RedisValue[] values,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await _inner.ExecuteAsync(
                    script,
                    keys,
                    values,
                    cancellationToken);
            }
            catch (RedisConnectionException)
                when (attempt < _options.MaxRetryAttempts)
            {
                if (_options.RetryDelay > TimeSpan.Zero)
                {
                    await Task.Delay(
                        _options.RetryDelay,
                        cancellationToken);
                }
            }
        }
    }
}
