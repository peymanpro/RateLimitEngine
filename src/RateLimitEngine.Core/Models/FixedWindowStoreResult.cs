namespace RateLimitEngine.Core.Models;

public sealed record FixedWindowStoreResult(
    bool Accepted,
    int Consumed,
    int Remaining,
    TimeSpan ResetAfter);
