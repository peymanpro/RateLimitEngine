using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.Redis.Gcra;

public sealed class RedisGcraStore : IGcraStore
{
    private const string Script = @"
local intervalMilliseconds = tonumber(ARGV[1])
local burstToleranceMilliseconds = tonumber(ARGV[2])
local cost = tonumber(ARGV[3])

local redisTime = redis.call('TIME')
local seconds = tonumber(redisTime[1])
local microseconds = tonumber(redisTime[2])

local nowMilliseconds =
    (seconds * 1000) + math.floor(microseconds / 1000)

local tatKey = KEYS[1] .. ':tat'

local tat =
    tonumber(redis.call('GET', tatKey))

if tat == nil then
    tat = nowMilliseconds
end

local incrementMilliseconds =
    intervalMilliseconds * cost

local earliestAllowed =
    tat - burstToleranceMilliseconds

if nowMilliseconds < earliestAllowed then
    local retryMilliseconds =
        math.max(
            1,
            math.ceil(earliestAllowed - nowMilliseconds))

    return {
        0,
        tat,
        retryMilliseconds,
        0
    }
end

local baseTat =
    math.max(tat, nowMilliseconds)

local newTat =
    baseTat + incrementMilliseconds

redis.call(
    'SET',
    tatKey,
    newTat)

local ttlMilliseconds =
    math.max(
        1,
        math.ceil(
            math.max(
                0,
                newTat + burstToleranceMilliseconds - nowMilliseconds)))

redis.call(
    'PEXPIRE',
    tatKey,
    ttlMilliseconds)

local remaining = 0

local leadTime =
    newTat - nowMilliseconds

local availableBurst =
    burstToleranceMilliseconds - leadTime

if availableBurst >= 0 then
    remaining =
        math.floor(
            availableBurst /
            intervalMilliseconds) + 1
end

return {
    1,
    newTat,
    0,
    remaining
}
";

    private readonly IRedisScriptExecutor _executor;

    public RedisGcraStore(IRedisScriptExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);
        _executor = executor;
    }

    public async ValueTask<GcraStoreResult> EvaluateAsync(
        string key,
        TimeSpan interval,
        TimeSpan burstTolerance,
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

        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }

        if (burstTolerance < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(burstTolerance));
        }

        if (cost <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost));
        }

        var result = await _executor.ExecuteAsync(
            Script,
            new RedisKey[]
            {
                $"ratelimit:gcra:{key}"
            },
            new RedisValue[]
            {
                checked((long)Math.Ceiling(interval.TotalMilliseconds)),
                checked((long)Math.Ceiling(burstTolerance.TotalMilliseconds)),
                cost
            },
            cancellationToken);

        if (result.IsNull)
        {
            throw new InvalidOperationException(
                "Redis GCRA script returned a null result.");
        }

        var values = (RedisResult[])result!;

        if (values.Length != 4)
        {
            throw new InvalidOperationException(
                $"Redis GCRA script returned {values.Length} values; expected 4.");
        }

        var accepted = (long)values[0]! == 1;
        var tatMilliseconds = (long)values[1]!;
        var retryMilliseconds = (long)values[2]!;
        var remaining = checked((int)(long)values[3]!);

        return new GcraStoreResult(
            Accepted: accepted,
            TheoreticalArrivalTime:
                DateTimeOffset.UnixEpoch.AddMilliseconds(tatMilliseconds),
            RetryAfter: retryMilliseconds > 0
                ? TimeSpan.FromMilliseconds(retryMilliseconds)
                : null,
            Remaining: remaining);
    }
}

