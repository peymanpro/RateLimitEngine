using System.Collections.Concurrent;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Time;

namespace RateLimitEngine.Algorithms.FixedWindow;

public sealed class FixedWindowRateLimiter : IRateLimiter
{
    private const int CleanupInterval = 256;

    private readonly IClock _clock;
    private readonly ConcurrentDictionary<RateLimitStateKey, WindowState> _states = new();
    private int _operationsSinceCleanup;

    public FixedWindowRateLimiter(IClock clock)
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
        var windowStart = GetWindowStart(now, policy.Window);

        var stateKey = new RateLimitStateKey(
            request.Key,
            policy.PermitLimit,
            policy.Window);

        var state = _states.GetOrAdd(
            stateKey,
            static _ => new WindowState());

        RateLimitDecision decision;

        lock (state.SyncRoot)
        {
            if (state.WindowStart != windowStart)
            {
                state.WindowStart = windowStart;
                state.Count = 0;
            }

            var resetAfter = windowStart + policy.Window - now;

            if (request.Cost > policy.PermitLimit)
            {
                decision = new RateLimitDecision(
                    allowed: false,
                    limit: policy.PermitLimit,
                    remaining: policy.PermitLimit - state.Count,
                    resetAfter: resetAfter,
                    retryAfter: resetAfter);
            }
            else
            {
                var remaining = policy.PermitLimit - state.Count;

                if (request.Cost <= remaining)
                {
                    state.Count += request.Cost;

                    decision = new RateLimitDecision(
                        allowed: true,
                        limit: policy.PermitLimit,
                        remaining: policy.PermitLimit - state.Count,
                        resetAfter: resetAfter);
                }
                else
                {
                    decision = new RateLimitDecision(
                        allowed: false,
                        limit: policy.PermitLimit,
                        remaining: remaining,
                        resetAfter: resetAfter,
                        retryAfter: resetAfter);
                }
            }
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
                if (state.WindowStart + pair.Key.Window <= now)
                {
                    _states.TryRemove(
                        new KeyValuePair<RateLimitStateKey, WindowState>(
                            pair.Key,
                            state));
                }
            }
        }
    }

    private static DateTimeOffset GetWindowStart(
        DateTimeOffset timestamp,
        TimeSpan window)
    {
        var elapsedTicks = (timestamp - DateTimeOffset.UnixEpoch).Ticks;
        var windowTicks = window.Ticks;
        var windowIndex = elapsedTicks / windowTicks;

        return DateTimeOffset.UnixEpoch.AddTicks(windowIndex * windowTicks);
    }

    private readonly record struct RateLimitStateKey(
        string Key,
        int PermitLimit,
        TimeSpan Window);

    private sealed class WindowState
    {
        public object SyncRoot { get; } = new();

        public DateTimeOffset WindowStart { get; set; }

        public int Count { get; set; }
    }
}
