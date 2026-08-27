namespace RateLimitEngine.Core.Models;

public sealed record GcraStoreResult(
    bool Accepted,
    DateTimeOffset? TheoreticalArrivalTime,
    TimeSpan? RetryAfter,
    int Remaining);
