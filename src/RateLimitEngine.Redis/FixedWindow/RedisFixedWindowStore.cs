using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.Redis.FixedWindow;

public sealed class RedisFixedWindowStore : IFixedWindowStore
{
    private const string Script = @"
local windowMilliseconds = tonumber(ARGV[1])
local cost = tonumber(ARGV[2])
local limit = tonumber(ARGV[3])

local redisTime = redis.call('TIME')
local seconds = tonumber(redisTime[1])
local microseconds = tonumber(redisTime[2])

local nowMilliseconds =
    (seconds * 1000) + math.floor(microseconds / 1000)

local windowStart =
    math.floor(nowMilliseconds / windowMilliseconds)
    * windowMilliseconds

local windowKey =
    KEYS[1] .. ':' .. tostring(windowStart)

local current =
    tonumber(redis.call('GET', windowKey) or '0')

if current + cost > limit then
    local ttl = redis.call('PTTL', windowKey)

    return { 0, current, ttl }
end

local updated =
    redis.call('INCRBY', windowKey, cost)

if current == 0 then
    redis.call('PEXPIRE', windowKey, windowMilliseconds)
end

local ttl =
    redis.call('PTTL', windowKey)

return { 1, updated, ttl }
";

    private readonly IRedisScriptExecutor _executor;

    public RedisFixedWindowStore(IRedisScriptExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);
        _executor = executor;
    }

    public async ValueTask<FixedWindowStoreResult> IncrementAsync(
        string key,
        TimeSpan window,
        int permitLimit,
        int cost,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "Redis rate limit key cannot be null or empty.",
                nameof(key));
        }

        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window));
        }

        if (permitLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(permitLimit));
        }

        if (cost <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost));
        }

        var redisKey =
            $"ratelimit:fixed-window:{key}";

        var result = await _executor.ExecuteAsync(
            Script,
            new RedisKey[] { redisKey },
            new RedisValue[]
            {
                checked((long)window.TotalMilliseconds),
                cost,
                permitLimit
            },
            cancellationToken);

        if (result.IsNull)
        {
            throw new InvalidOperationException(
                "Redis rate limit script returned a null result.");
        }

        var values = (RedisResult[])result!;

        if (values.Length != 3)
        {
            throw new InvalidOperationException(
                $"Redis rate limit script returned {values.Length} values; expected 3.");
        }

        var accepted = (long)values[0]! == 1;
        var consumed = (long)values[1]!;
        var ttlMilliseconds = (long)values[2]!;

        return new FixedWindowStoreResult(
            Accepted: accepted,
            Consumed: checked((int)consumed),
            Remaining: Math.Max(
                0,
                permitLimit - checked((int)consumed)),
            ResetAfter: ttlMilliseconds > 0
                ? TimeSpan.FromMilliseconds(ttlMilliseconds)
                : TimeSpan.Zero);
    }
}
