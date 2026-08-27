using System.Globalization;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.Redis.TokenBucket;

public sealed class RedisTokenBucketStore : ITokenBucketStore
{
    private const string Script = @"
local capacity = tonumber(ARGV[1])
local refillRate = tonumber(ARGV[2])
local cost = tonumber(ARGV[3])

local redisTime = redis.call('TIME')
local seconds = tonumber(redisTime[1])
local microseconds = tonumber(redisTime[2])

local nowMilliseconds =
    (seconds * 1000) + math.floor(microseconds / 1000)

local tokenKey = KEYS[1] .. ':tokens'
local timestampKey = KEYS[1] .. ':timestamp'

local tokens = tonumber(redis.call('GET', tokenKey))
local lastTimestamp = tonumber(redis.call('GET', timestampKey))

if tokens == nil or lastTimestamp == nil then
    tokens = capacity
    lastTimestamp = nowMilliseconds
end

local elapsedMilliseconds =
    math.max(0, nowMilliseconds - lastTimestamp)

local elapsedSeconds =
    elapsedMilliseconds / 1000

tokens = math.min(
    capacity,
    tokens + (elapsedSeconds * refillRate))

lastTimestamp = nowMilliseconds

if cost > capacity then
    local retryMilliseconds =
        math.ceil(((cost - tokens) / refillRate) * 1000)

    return { 0, tokens, retryMilliseconds }
end

if cost <= tokens then
    tokens = tokens - cost

    redis.call('SET', tokenKey, tokens)
    redis.call('SET', timestampKey, lastTimestamp)

    local ttlMilliseconds =
        math.max(
            1,
            math.ceil(((capacity - tokens) / refillRate) * 1000))

    redis.call('PEXPIRE', tokenKey, ttlMilliseconds)
    redis.call('PEXPIRE', timestampKey, ttlMilliseconds)

    return { 1, tokens, 0 }
end

redis.call('SET', tokenKey, tokens)
redis.call('SET', timestampKey, lastTimestamp)

local retryMilliseconds =
    math.max(
        1,
        math.ceil(((cost - tokens) / refillRate) * 1000))

local ttlMilliseconds =
    math.max(
        retryMilliseconds,
        math.ceil(((capacity - tokens) / refillRate) * 1000))

redis.call('PEXPIRE', tokenKey, ttlMilliseconds)
redis.call('PEXPIRE', timestampKey, ttlMilliseconds)

return { 0, tokens, retryMilliseconds }
";

    private readonly IRedisScriptExecutor _executor;

    public RedisTokenBucketStore(IRedisScriptExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);
        _executor = executor;
    }

    public async ValueTask<TokenBucketStoreResult> ConsumeAsync(
        string key,
        double capacity,
        double refillRate,
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

        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        if (refillRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(refillRate));
        }

        if (cost <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost));
        }

        var result = await _executor.ExecuteAsync(
            Script,
            new RedisKey[]
            {
                $"ratelimit:token-bucket:{key}"
            },
            new RedisValue[]
            {
                capacity.ToString(CultureInfo.InvariantCulture),
                refillRate.ToString(CultureInfo.InvariantCulture),
                cost
            },
            cancellationToken);

        if (result.IsNull)
        {
            throw new InvalidOperationException(
                "Redis token bucket script returned a null result.");
        }

        var values = (RedisResult[])result!;

        if (values.Length != 3)
        {
            throw new InvalidOperationException(
                $"Redis token bucket script returned {values.Length} values; expected 3.");
        }

        var accepted = (long)values[0]! == 1;

        var remainingTokens = double.Parse(
            values[1].ToString()!,
            CultureInfo.InvariantCulture);

        var retryMilliseconds = (long)values[2]!;

        return new TokenBucketStoreResult(
            Accepted: accepted,
            RemainingTokens: remainingTokens,
            RetryAfter: retryMilliseconds > 0
                ? TimeSpan.FromMilliseconds(retryMilliseconds)
                : null);
    }
}
