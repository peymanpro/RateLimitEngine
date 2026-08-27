namespace RateLimitEngine.Algorithms;

public sealed record RateLimiterOptions
{
    public int TokenBucketCapacity { get; init; } = 100;

    public bool Validate()
    {
        return TokenBucketCapacity > 0;
    }
}
