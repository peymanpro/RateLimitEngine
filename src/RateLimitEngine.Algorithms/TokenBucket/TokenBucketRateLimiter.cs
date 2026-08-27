using System.Collections.Concurrent;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Time;

namespace RateLimitEngine.Algorithms.TokenBucket;

public sealed class TokenBucketRateLimiter : IRateLimiter
{
    private const int CleanupInterval = 256;

    private readonly IClock _clock;
    private readonly TokenBucketOptions _options;
    private readonly ConcurrentDictionary<RateLimitStateKey, BucketState> _states = new();
    private int _operationsSinceCleanup;

    public TokenBucketRateLimiter(
        IClock clock,
        TokenBucketOptions options)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        _clock = clock;
        _options = options;
    }

    public ValueTask<RateLimitDecision> EvaluateAsync(
        RateLimitRequest request,
        RateLimitPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(policy);

        cancellationToken.ThrowIfCancellationRequested();

        var refillRate = policy.PermitLimit / policy.Window.TotalSeconds;

        var stateKey = new RateLimitStateKey(
            request.Key,
            policy.PermitLimit,
            policy.Window);

        var state = _states.GetOrAdd(
            stateKey,
            _ => new BucketState(
                _options.Capacity,
                _clock.GetTimestamp()));

        RateLimitDecision decision;
        var now = _clock.UtcNow;
        var nowTimestamp = _clock.GetTimestamp();

        lock (state.SyncRoot)
        {
            var elapsed = _clock.GetElapsedTime(state.LastTimestamp);

            if (elapsed > TimeSpan.Zero)
            {
                state.Tokens = Math.Min(
                    _options.Capacity,
                    state.Tokens + elapsed.TotalSeconds * refillRate);

                state.LastTimestamp = nowTimestamp;
            }

            var remaining = Math.Max(
                0,
                (int)Math.Floor(state.Tokens));

            if (request.Cost <= state.Tokens &&
                request.Cost <= _options.Capacity)
            {
                state.Tokens -= request.Cost;
                state.LastActivity = now;

                remaining = Math.Max(
                    0,
                    (int)Math.Floor(state.Tokens));

                decision = new RateLimitDecision(
                    allowed: true,
                    limit: policy.PermitLimit,
                    remaining: remaining);
            }
            else
            {
                var missingTokens = Math.Max(
                    0,
                    request.Cost - state.Tokens);

                var retryAfter = missingTokens == 0
                    ? TimeSpan.Zero
                    : TimeSpan.FromSeconds(
                        missingTokens / refillRate);

                state.LastActivity = now;

                decision = new RateLimitDecision(
                    allowed: false,
                    limit: policy.PermitLimit,
                    remaining: remaining,
                    retryAfter: retryAfter);
            }
        }

        TriggerCleanupIfNeeded(now, nowTimestamp);

        return ValueTask.FromResult(decision);
    }

    private void TriggerCleanupIfNeeded(
        DateTimeOffset now,
        long nowTimestamp)
    {
        if (Interlocked.Increment(ref _operationsSinceCleanup) < CleanupInterval)
        {
            return;
        }

        Interlocked.Exchange(ref _operationsSinceCleanup, 0);

        foreach (var pair in _states)
        {
            var state = pair.Value;

            lock (state.SyncRoot)
            {
                var elapsed = _clock.GetElapsedTime(state.LastTimestamp);

                if (state.Tokens >= _options.Capacity &&
                    elapsed >= pair.Key.Window)
                {
                    _states.TryRemove(
                        new KeyValuePair<RateLimitStateKey, BucketState>(
                            pair.Key,
                            state));
                }
            }
        }
    }

    private sealed class BucketState
    {
        public BucketState(
            int capacity,
            long timestamp)
        {
            Tokens = capacity;
            LastTimestamp = timestamp;
        }

        public object SyncRoot { get; } = new();

        public double Tokens { get; set; }

        public long LastTimestamp { get; set; }

        public DateTimeOffset LastActivity { get; set; }
    }
}
