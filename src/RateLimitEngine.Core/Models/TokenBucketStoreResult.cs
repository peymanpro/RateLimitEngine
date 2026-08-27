namespace RateLimitEngine.Core.Models;

public sealed record TokenBucketStoreResult(
    bool Accepted,
    double RemainingTokens,
    TimeSpan? RetryAfter);
