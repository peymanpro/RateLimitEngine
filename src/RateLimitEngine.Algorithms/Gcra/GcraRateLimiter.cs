using System.Collections.Concurrent;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Time;

namespace RateLimitEngine.Algorithms.Gcra;

public sealed class GcraRateLimiter : IRateLimiter
{
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<GcraStateKey, GcraState> _states = new();

    public GcraRateLimiter(IClock clock)
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

        if (request.Cost > policy.PermitLimit)
        {
            return ValueTask.FromResult(
                new RateLimitDecision(
                    allowed: false,
                    limit: policy.PermitLimit,
                    remaining: policy.PermitLimit));
        }

        var interval = policy.Window / (double)policy.PermitLimit;
        var burstTolerance = interval * (policy.PermitLimit - 1);

        var stateKey = new GcraStateKey(
            request.Key,
            policy.PermitLimit,
            policy.Window);

        var state = _states.GetOrAdd(
            stateKey,
            static _ => new GcraState());

        lock (state.SyncRoot)
        {
            var now = _clock.UtcNow;

            if (state.TheoreticalArrivalTime == default)
            {
                state.TheoreticalArrivalTime = now;
            }

            var increment = TimeSpan.FromTicks(
                (long)(interval.Ticks * request.Cost));

            var candidate = state.TheoreticalArrivalTime + increment;
            var earliestAllowedTime =
                state.TheoreticalArrivalTime - burstTolerance;

            if (now < earliestAllowedTime)
            {
                var retryAfter = earliestAllowedTime - now;

                return ValueTask.FromResult(
                    new RateLimitDecision(
                        allowed: false,
                        limit: policy.PermitLimit,
                        remaining: 0,
                        retryAfter: retryAfter));
            }

            state.TheoreticalArrivalTime =
                candidate > now
                    ? candidate
                    : now + increment;

            var nextAvailableAt =
                state.TheoreticalArrivalTime - increment;

            var resetAfter =
                nextAvailableAt > now
                    ? nextAvailableAt - now
                    : TimeSpan.Zero;

            return ValueTask.FromResult(
                new RateLimitDecision(
                    allowed: true,
                    limit: policy.PermitLimit,
                    remaining: CalculateRemaining(
                        state.TheoreticalArrivalTime,
                        now,
                        interval,
                        burstTolerance),
                    resetAfter: resetAfter));
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
            (int)Math.Floor(availableTime.TotalSeconds / interval.TotalSeconds));
    }

    private readonly record struct GcraStateKey(
        string Key,
        int PermitLimit,
        TimeSpan Window);

    private sealed class GcraState
    {
        public object SyncRoot { get; } = new();

        public DateTimeOffset TheoreticalArrivalTime { get; set; }
    }
}


