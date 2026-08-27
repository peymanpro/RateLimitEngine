namespace RateLimitEngine.Core.Models;

public sealed record RateLimitDecision
{
    public RateLimitDecision(
        bool allowed,
        int limit,
        int remaining,
        TimeSpan? resetAfter = null,
        TimeSpan? retryAfter = null)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                "Rate limit must be greater than zero.");
        }

        if (remaining < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(remaining),
                remaining,
                "Remaining capacity cannot be negative.");
        }

        if (resetAfter.HasValue && resetAfter.Value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resetAfter),
                resetAfter,
                "Reset duration cannot be negative.");
        }

        if (retryAfter.HasValue && retryAfter.Value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryAfter),
                retryAfter,
                "Retry duration cannot be negative.");
        }

        Allowed = allowed;
        Limit = limit;
        Remaining = remaining;
        ResetAfter = resetAfter;
        RetryAfter = retryAfter;
    }

    public bool Allowed { get; }

    public int Limit { get; }

    public int Remaining { get; }

    public TimeSpan? ResetAfter { get; }

    public TimeSpan? RetryAfter { get; }
}
