using System.Collections.Concurrent;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Time;

namespace RateLimitEngine.Algorithms.SlidingWindow;

public sealed class SlidingWindowRateLimiter : IRateLimiter
{
    private const int CleanupInterval = 256;

    private readonly IClock _clock;
    private readonly ConcurrentDictionary<RateLimitStateKey, WindowState> _states = new();
    private int _operationsSinceCleanup;

    public SlidingWindowRateLimiter(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public ValueTask<RateLimitDecision> EvaluateAsync(
        RateLimitRequest request,
        RateLimitPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(policy);

        cancellationToken.ThrowIfCancellationRequested();

        var now = _clock.UtcNow;
        var stateKey = new RateLimitStateKey(
            request.Key,
            policy.PermitLimit,
            policy.Window);

        var state = _states.GetOrAdd(
            stateKey,
            static _ => new WindowState());

        RateLimitDecision decision;
        bool stateIsEmpty;

        lock (state.SyncRoot)
        {
            var windowStart = now - policy.Window;

            while (state.Entries.Count > 0 &&
                   state.Entries.Peek().Timestamp <= windowStart)
            {
                state.ConsumedPermits -= state.Entries.Dequeue().Cost;
            }

            stateIsEmpty = state.Entries.Count == 0;

            if (request.Cost > policy.PermitLimit)
            {
                decision = new RateLimitDecision(
                    allowed: false,
                    limit: policy.PermitLimit,
                    remaining: Math.Max(
                        0,
                        policy.PermitLimit - state.ConsumedPermits),
                    resetAfter: policy.Window);
            }
            else
            {
                var remaining =
                    Math.Max(0, policy.PermitLimit - state.ConsumedPermits);

                if (request.Cost <= remaining)
                {
                    state.Entries.Enqueue(
                        new WindowEntry(now, request.Cost));

                    state.ConsumedPermits += request.Cost;
                    stateIsEmpty = false;

                    decision = new RateLimitDecision(
                        allowed: true,
                        limit: policy.PermitLimit,
                        remaining: policy.PermitLimit - state.ConsumedPermits,
                        resetAfter: GetResetAfter(state, now, policy.Window));
                }
                else
                {
                    decision = new RateLimitDecision(
                        allowed: false,
                        limit: policy.PermitLimit,
                        remaining: remaining,
                        resetAfter: GetResetAfter(state, now, policy.Window),
                        retryAfter: GetRetryAfter(
                            state,
                            now,
                            policy.Window,
                            request.Cost - remaining));
                }
            }
        }

        if (stateIsEmpty)
        {
            _states.TryRemove(
                new KeyValuePair<RateLimitStateKey, WindowState>(
                    stateKey,
                    state));
        }

        TriggerCleanupIfNeeded(now);

        return ValueTask.FromResult(decision);
    }

    private void TriggerCleanupIfNeeded(DateTimeOffset now)
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
                while (state.Entries.Count > 0 &&
                       state.Entries.Peek().Timestamp <= now - pair.Key.Window)
                {
                    state.ConsumedPermits -= state.Entries.Dequeue().Cost;
                }

                if (state.Entries.Count == 0)
                {
                    _states.TryRemove(
                        new KeyValuePair<RateLimitStateKey, WindowState>(
                            pair.Key,
                            state));
                }
            }
        }
    }

    private static TimeSpan GetResetAfter(
        WindowState state,
        DateTimeOffset now,
        TimeSpan window)
    {
        if (state.Entries.Count == 0)
        {
            return window;
        }

        var expiresAt = state.Entries.Peek().Timestamp + window;

        return expiresAt > now
            ? expiresAt - now
            : TimeSpan.Zero;
    }

    private static TimeSpan GetRetryAfter(
        WindowState state,
        DateTimeOffset now,
        TimeSpan window,
        int requiredPermits)
    {
        var remainingToRelease = requiredPermits;

        foreach (var entry in state.Entries)
        {
            remainingToRelease -= entry.Cost;

            if (remainingToRelease <= 0)
            {
                var availableAt = entry.Timestamp + window;

                return availableAt > now
                    ? availableAt - now
                    : TimeSpan.Zero;
            }
        }

        return TimeSpan.Zero;
    }

    private readonly record struct WindowEntry(
        DateTimeOffset Timestamp,
        int Cost);

    private sealed class WindowState
    {
        public object SyncRoot { get; } = new();

        public Queue<WindowEntry> Entries { get; } = new();

        public int ConsumedPermits { get; set; }
    }
}


