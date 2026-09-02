using System.Collections.Concurrent;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Time;

namespace RateLimitEngine.Algorithms.InMemory;

internal sealed class InMemorySlidingWindowStore : ISlidingWindowStore
{
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<RateLimitStateKey, WindowState> _states = new();

    public InMemorySlidingWindowStore(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public ValueTask<SlidingWindowStoreResult> EvaluateAsync(
        string key,
        TimeSpan window,
        int permitLimit,
        int cost,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stateKey = new RateLimitStateKey(
            key,
            permitLimit,
            window);

        var state = _states.GetOrAdd(
            stateKey,
            static _ => new WindowState());

        var now = _clock.UtcNow;

        lock (state.SyncRoot)
        {
            var windowStart = now - window;

            while (state.Entries.Count > 0 &&
                   state.Entries.Peek().Timestamp <= windowStart)
            {
                state.Consumed -= state.Entries.Dequeue().Cost;
            }

            var remaining = Math.Max(
                0,
                permitLimit - state.Consumed);

            if (cost > permitLimit)
            {
                return ValueTask.FromResult(
                    new SlidingWindowStoreResult(
                        Accepted: false,
                        Consumed: state.Consumed,
                        Remaining: remaining,
                        RetryAfter: null,
                        ResetAfter: window));
            }

            if (cost <= remaining)
            {
                state.Entries.Enqueue(
                    new WindowEntry(now, cost));

                state.Consumed += cost;

                var resetAfter = state.Entries.Count > 0
                    ? state.Entries.Peek().Timestamp + window - now
                    : window;

                return ValueTask.FromResult(
                    new SlidingWindowStoreResult(
                        Accepted: true,
                        Consumed: state.Consumed,
                        Remaining: permitLimit - state.Consumed,
                        RetryAfter: null,
                        ResetAfter: MaxZero(resetAfter)));
            }

            var required = cost - remaining;
            var retryAfter = GetRetryAfter(
                state,
                now,
                window,
                required);

            var currentResetAfter =
                state.Entries.Count > 0
                    ? state.Entries.Peek().Timestamp + window - now
                    : window;

            return ValueTask.FromResult(
                new SlidingWindowStoreResult(
                    Accepted: false,
                    Consumed: state.Consumed,
                    Remaining: remaining,
                    RetryAfter: retryAfter,
                    ResetAfter: MaxZero(currentResetAfter)));
        }
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

    private static TimeSpan MaxZero(TimeSpan value) =>
        value < TimeSpan.Zero
            ? TimeSpan.Zero
            : value;

    private readonly record struct WindowEntry(
        DateTimeOffset Timestamp,
        int Cost);

    private sealed class WindowState
    {
        public object SyncRoot { get; } = new();

        public Queue<WindowEntry> Entries { get; } = new();

        public int Consumed { get; set; }
    }
}
