using RateLimitEngine.Core.Models;

namespace RateLimitEngine.Core.Abstractions;

public interface IFixedWindowStore
{
    ValueTask<FixedWindowStoreResult> IncrementAsync(
        string key,
        TimeSpan window,
        int permitLimit,
        int cost,
        CancellationToken cancellationToken = default);
}
