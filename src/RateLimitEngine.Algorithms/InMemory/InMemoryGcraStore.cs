using System.Collections.Concurrent;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Time;

namespace RateLimitEngine.Algorithms.InMemory;

internal sealed class InMemoryGcraStore : IGcraStore
{
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<GcraStateKey, GcraState> _states = new();

    public InMemoryGcraStore(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public ValueTask<GcraStoreResult> EvaluateAsync(
        string key,
        TimeSpan interval,
        TimeSpan burstTolerance,
        int cost,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stateKey = new GcraStateKey(
            key,
            interval,
            burstTolerance);

        var state = _states.GetOrAdd(
            stateKey,
            static _ => new GcraState());

        var now = _clock.UtcNow;

        lock (state.SyncRoot)
        {
            if (state.TheoreticalArrivalTime == default)
            {
                state.TheoreticalArrivalTime = now;
            }

            var increment = TimeSpan.FromTicks(
                (long)(interval.Ticks * cost));

            var candidate =
                state.TheoreticalArrivalTime + increment;

            var earliestAllowedTime =
                state.TheoreticalArrivalTime - burstTolerance;

            if (now < earliestAllowedTime)
            {
                return ValueTask.FromResult(
                    new GcraStoreResult(
                        Accepted: false,
                        TheoreticalArrivalTime: state.TheoreticalArrivalTime,
                        RetryAfter: earliestAllowedTime - now,
                        Remaining: 0));
            }

            state.TheoreticalArrivalTime =
                candidate > now
                    ? candidate
                    : now + increment;

            var remaining = CalculateRemaining(
                state.TheoreticalArrivalTime,
                now,
                interval,
                burstTolerance);

            return ValueTask.FromResult(
                new GcraStoreResult(
                    Accepted: true,
                    TheoreticalArrivalTime: state.TheoreticalArrivalTime,
                    RetryAfter: null,
                    Remaining: remaining));
        }
    }

    private static int CalculateRemaining(
        DateTimeOffset theoreticalArrivalTime,
        DateTimeOffset now,
        TimeSpan interval,
        TimeSpan burstTolerance)
    {
        var leadTime =
            theoreticalArrivalTime - now;

        var availableBurst =
            burstTolerance - leadTime;

        if (availableBurst < TimeSpan.Zero)
        {
            return 0;
        }

        return Math.Max(
            0,
            (int)Math.Floor(
                availableBurst.Ticks /
                (double)interval.Ticks) + 1);
    }

    private readonly record struct GcraStateKey(
        string Key,
        TimeSpan Interval,
        TimeSpan BurstTolerance);

    private sealed class GcraState
    {
        public object SyncRoot { get; } = new();

        public DateTimeOffset TheoreticalArrivalTime { get; set; }
    }
}

