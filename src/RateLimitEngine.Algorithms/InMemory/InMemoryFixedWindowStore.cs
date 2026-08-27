using System.Collections.Concurrent;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Time;

namespace RateLimitEngine.Algorithms.InMemory;

public sealed class InMemoryFixedWindowStore : IFixedWindowStore
{
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<RateLimitStateKey, WindowState> _states = new();

    public InMemoryFixedWindowStore(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public ValueTask<FixedWindowStoreResult> IncrementAsync(
        string key,
        TimeSpan window,
        int permitLimit,
        int cost,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = _clock.UtcNow;
        var windowStart = GetWindowStart(now, window);

        var stateKey = new RateLimitStateKey(
            key,
            permitLimit,
            window);

        var state = _states.GetOrAdd(
            stateKey,
            static _ => new WindowState());

        lock (state.SyncRoot)
        {
            if (state.WindowStart != windowStart)
            {
                state.WindowStart = windowStart;
                state.Consumed = 0;
            }

            var remaining = Math.Max(
                0,
                permitLimit - state.Consumed);

            var resetAfter = MaxZero(
                windowStart + window - now);

            if (cost > remaining)
            {
                return ValueTask.FromResult(
                    new FixedWindowStoreResult(
                        Accepted: false,
                        Consumed: state.Consumed,
                        Remaining: remaining,
                        ResetAfter: resetAfter));
            }

            state.Consumed += cost;

            return ValueTask.FromResult(
                new FixedWindowStoreResult(
                    Accepted: true,
                    Consumed: state.Consumed,
                    Remaining: permitLimit - state.Consumed,
                    ResetAfter: resetAfter));
        }
    }

    private static DateTimeOffset GetWindowStart(
        DateTimeOffset timestamp,
        TimeSpan window)
    {
        var elapsedTicks =
            (timestamp - DateTimeOffset.UnixEpoch).Ticks;

        var windowTicks = window.Ticks;
        var windowIndex = elapsedTicks / windowTicks;

        return DateTimeOffset.UnixEpoch.AddTicks(
            windowIndex * windowTicks);
    }

    private static TimeSpan MaxZero(TimeSpan value) =>
        value < TimeSpan.Zero
            ? TimeSpan.Zero
            : value;

    private sealed class WindowState
    {
        public object SyncRoot { get; } = new();

        public DateTimeOffset WindowStart { get; set; }

        public int Consumed { get; set; }
    }
}
