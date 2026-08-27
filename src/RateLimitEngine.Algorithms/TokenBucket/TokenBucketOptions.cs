namespace RateLimitEngine.Algorithms.TokenBucket;

public sealed record TokenBucketOptions
{
    public TokenBucketOptions(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                "Capacity must be greater than zero.");
        }

        Capacity = capacity;
    }

    public int Capacity { get; }
}
