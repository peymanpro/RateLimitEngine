namespace RateLimitEngine.Core.Models;

public sealed record SlidingWindowStoreResult(
    bool Accepted,
    int Consumed,
    int Remaining,
    TimeSpan? RetryAfter,
    TimeSpan? ResetAfter);
