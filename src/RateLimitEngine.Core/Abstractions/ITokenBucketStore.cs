using RateLimitEngine.Core.Models;

namespace RateLimitEngine.Core.Abstractions;

public interface ITokenBucketStore
{
    ValueTask<TokenBucketStoreResult> ConsumeAsync(
        string key,
        double capacity,
        double refillRate,
        int cost,
        CancellationToken cancellationToken = default);
}
