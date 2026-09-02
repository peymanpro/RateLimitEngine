namespace RateLimitEngine.Core.Models;

internal readonly record struct RateLimitStateKey(
    string Key,
    int PermitLimit,
    TimeSpan Window);
