using RateLimitEngine.Core.Models;

namespace RateLimitEngine.Core.Abstractions;

public interface ISlidingWindowStore
{
    ValueTask<SlidingWindowStoreResult> EvaluateAsync(
        string key,
        TimeSpan window,
        int permitLimit,
        int cost,
        CancellationToken cancellationToken = default);
}
