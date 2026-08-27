namespace RateLimitEngine.Core.Models;

public enum RateLimitAlgorithm
{
    FixedWindow = 0,
    SlidingWindow = 1,
    TokenBucket = 2,
    Gcra = 3
}
