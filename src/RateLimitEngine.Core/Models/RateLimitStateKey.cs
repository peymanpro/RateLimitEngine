namespace RateLimitEngine.Core.Models;

public readonly record struct RateLimitStateKey(
    string Key,
    int PermitLimit,
    TimeSpan Window);
