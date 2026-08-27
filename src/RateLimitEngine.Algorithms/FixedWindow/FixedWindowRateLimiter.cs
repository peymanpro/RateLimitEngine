using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Time;

namespace RateLimitEngine.Algorithms.FixedWindow;

public sealed class FixedWindowRateLimiter : IRateLimiter
{
    private readonly IClock _clock;
    private readonly IFixedWindowStore _store;

    public FixedWindowRateLimiter(
        IClock clock,
        IFixedWindowStore store)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(store);

        _clock = clock;
        _store = store;
    }

    public async ValueTask<RateLimitDecision> EvaluateAsync(
        RateLimitRequest request,
        RateLimitPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(policy);

        cancellationToken.ThrowIfCancellationRequested();

        var now = _clock.UtcNow;
        var windowStart = GetWindowStart(now, policy.Window);

        var result = await _store.IncrementAsync(
            request.Key,
            windowStart,
            policy.Window,
            policy.PermitLimit,
            request.Cost,
            cancellationToken);

        return new RateLimitDecision(
            allowed: result.Accepted,
            limit: policy.PermitLimit,
            remaining: result.Remaining,
            resetAfter: result.ResetAfter,
            retryAfter: result.Accepted
                ? null
                : result.ResetAfter);
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
}
