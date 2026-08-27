using RateLimitEngine.Core.Models;

namespace RateLimitEngine.Core.Abstractions;

public interface IGcraStore
{
    ValueTask<GcraStoreResult> EvaluateAsync(
        string key,
        TimeSpan interval,
        TimeSpan burstTolerance,
        int cost,
        CancellationToken cancellationToken = default);
}
