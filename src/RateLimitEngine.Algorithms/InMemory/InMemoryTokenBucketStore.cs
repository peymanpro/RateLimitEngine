using System.Collections.Concurrent;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Time;

namespace RateLimitEngine.Algorithms.InMemory;

public sealed class InMemoryTokenBucketStore : ITokenBucketStore
{
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<TokenBucketStateKey, BucketState> _states = new();

    public InMemoryTokenBucketStore(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public ValueTask<TokenBucketStoreResult> ConsumeAsync(
        string key,
        double capacity,
        double refillRate,
        int cost,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stateKey = new TokenBucketStateKey(
            key,
            capacity,
            refillRate);

        var state = _states.GetOrAdd(
            stateKey,
            _ => new BucketState(
                capacity,
                _clock.GetTimestamp()));

        lock (state.SyncRoot)
        {
            var elapsed = _clock.GetElapsedTime(state.LastTimestamp);

            if (elapsed > TimeSpan.Zero)
            {
                state.Tokens = Math.Min(
                    capacity,
                    state.Tokens + elapsed.TotalSeconds * refillRate);

                state.LastTimestamp = _clock.GetTimestamp();
            }

            if (cost <= state.Tokens &&
                cost <= capacity)
            {
                state.Tokens -= cost;

                return ValueTask.FromResult(
                    new TokenBucketStoreResult(
                        Accepted: true,
                        RemainingTokens: state.Tokens,
                        RetryAfter: null));
            }

            var missingTokens = Math.Max(
                0,
                cost - state.Tokens);

            var retryAfter = missingTokens > 0
                ? TimeSpan.FromSeconds(
                    missingTokens / refillRate)
                : TimeSpan.Zero;

            return ValueTask.FromResult(
                new TokenBucketStoreResult(
                    Accepted: false,
                    RemainingTokens: state.Tokens,
                    RetryAfter: retryAfter));
        }
    }

    private readonly record struct TokenBucketStateKey(
        string Key,
        double Capacity,
        double RefillRate);

    private sealed class BucketState
    {
        public BucketState(
            double capacity,
            long timestamp)
        {
            Tokens = capacity;
            LastTimestamp = timestamp;
        }

        public object SyncRoot { get; } = new();

        public double Tokens { get; set; }

        public long LastTimestamp { get; set; }
    }
}
