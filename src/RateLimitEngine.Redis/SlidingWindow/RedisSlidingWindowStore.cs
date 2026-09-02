using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.Redis.SlidingWindow;

internal sealed class RedisSlidingWindowStore : ISlidingWindowStore
{
    private const string Script = @"
local windowMilliseconds = tonumber(ARGV[1])
local limit = tonumber(ARGV[2])
local cost = tonumber(ARGV[3])

local redisTime = redis.call('TIME')
local seconds = tonumber(redisTime[1])
local microseconds = tonumber(redisTime[2])

local nowMilliseconds =
    (seconds * 1000) + math.floor(microseconds / 1000)

local cutoff =
    nowMilliseconds - windowMilliseconds

local entriesKey = KEYS[1] .. ':entries'
local totalKey = KEYS[1] .. ':total'
local sequenceKey = KEYS[1] .. ':sequence'

local expired =
    redis.call('ZRANGEBYSCORE', entriesKey, '-inf', cutoff)

for _, member in ipairs(expired) do
    local separator = string.find(member, ':')
    local memberCost = 1

    if separator ~= nil then
        memberCost = tonumber(string.sub(member, separator + 1))
    end

    if memberCost ~= nil then
        redis.call('DECRBY', totalKey, memberCost)
    end

    redis.call('ZREM', entriesKey, member)
end

local consumed =
    tonumber(redis.call('GET', totalKey) or '0')

if consumed < 0 then
    consumed = 0
    redis.call('SET', totalKey, '0')
end

if cost > limit then
    local oldest =
        redis.call('ZRANGE', entriesKey, 0, 0, 'WITHSCORES')

    if #oldest == 0 then
        return { 0, consumed, 0, 0 }
    end

    local oldestTimestamp = tonumber(oldest[2])

    local resetAfter =
        math.max(
            0,
            oldestTimestamp + windowMilliseconds - nowMilliseconds)

    return { 0, consumed, 0, resetAfter }
end

local remaining = limit - consumed

if cost > remaining then
    local required = cost - remaining
    local released = 0
    local retryAfter = 0

    local active =
        redis.call('ZRANGE', entriesKey, 0, -1, 'WITHSCORES')

    for i = 1, #active, 2 do
        local member = active[i]
        local timestamp = tonumber(active[i + 1])

        local separator = string.find(member, ':')
        local memberCost = 1

        if separator ~= nil then
            memberCost = tonumber(string.sub(member, separator + 1))
        end

        if memberCost ~= nil then
            released = released + memberCost
        end

        if released >= required then
            retryAfter =
                math.max(
                    0,
                    timestamp + windowMilliseconds - nowMilliseconds)

            break
        end
    end

    return { 0, consumed, retryAfter, retryAfter }
end

local sequence =
    redis.call('INCR', sequenceKey)

local member =
    tostring(sequence) .. ':' .. tostring(cost)

redis.call(
    'ZADD',
    entriesKey,
    nowMilliseconds,
    member)

consumed = consumed + cost

redis.call(
    'SET',
    totalKey,
    consumed)

redis.call(
    'PEXPIRE',
    entriesKey,
    math.max(1, windowMilliseconds))

redis.call(
    'PEXPIRE',
    totalKey,
    math.max(1, windowMilliseconds))

redis.call(
    'PEXPIRE',
    sequenceKey,
    math.max(1, windowMilliseconds))

local oldest =
    redis.call('ZRANGE', entriesKey, 0, 0, 'WITHSCORES')

local resetAfter = 0

if #oldest > 0 then
    local oldestTimestamp = tonumber(oldest[2])

    resetAfter =
        math.max(
            0,
            oldestTimestamp + windowMilliseconds - nowMilliseconds)
end

return {
    1,
    consumed,
    0,
    resetAfter
}
";

    private readonly IRedisScriptExecutor _executor;

    public RedisSlidingWindowStore(IRedisScriptExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);
        _executor = executor;
    }

    public async ValueTask<SlidingWindowStoreResult> EvaluateAsync(
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

        var result = await _executor.ExecuteAsync(
            Script,
            new RedisKey[]
            {
                $"ratelimit:sliding-window:{key}"
            },
            new RedisValue[]
            {
                checked((long)window.TotalMilliseconds),
                permitLimit,
                cost
            },
            cancellationToken);

        if (result.IsNull)
        {
            throw new InvalidOperationException(
                "Redis sliding window script returned a null result.");
        }

        var values = (RedisResult[])result!;

        if (values.Length != 4)
        {
            throw new InvalidOperationException(
                $"Redis sliding window script returned {values.Length} values; expected 4.");
        }

        var accepted = (long)values[0]! == 1;
        var consumed = checked((int)(long)values[1]!);
        var retryMilliseconds = (long)values[2]!;
        var resetMilliseconds = (long)values[3]!;

        return new SlidingWindowStoreResult(
            Accepted: accepted,
            Consumed: consumed,
            Remaining: Math.Max(
                0,
                permitLimit - consumed),
            RetryAfter: retryMilliseconds > 0
                ? TimeSpan.FromMilliseconds(retryMilliseconds)
                : null,
            ResetAfter: resetMilliseconds > 0
                ? TimeSpan.FromMilliseconds(resetMilliseconds)
                : TimeSpan.Zero);
    }
}
