namespace RateLimitEngine.Redis.Infrastructure;

public sealed class RedisRetryOptions
{
    public int MaxRetryAttempts { get; init; }

    public TimeSpan RetryDelay { get; init; }

    public RedisRetryOptions()
    {
        MaxRetryAttempts = 0;
        RetryDelay = TimeSpan.Zero;
    }
}
