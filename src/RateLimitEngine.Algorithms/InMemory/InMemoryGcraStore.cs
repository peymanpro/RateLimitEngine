using System.Collections.Concurrent;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Time;

namespace RateLimitEngine.Algorithms.InMemory;

public sealed class InMemoryGcraStore : IGcraStore
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
        if (theoreticalArrivalTime <= now)
        {
            return 0;
        }

        var availableTime =
            theoreticalArrivalTime - now + burstTolerance;

        return Math.Max(
            0,
            (int)Math.Floor(
                availableTime.TotalSeconds /
                interval.TotalSeconds));
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
