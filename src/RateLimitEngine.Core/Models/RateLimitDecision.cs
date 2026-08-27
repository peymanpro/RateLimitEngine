namespace RateLimitEngine.Core.Models;

/// <summary>
/// Describes the outcome of evaluating a request against a rate limit policy.
/// </summary>
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

        if (allowed && retryAfter.HasValue)
        {
            throw new ArgumentException(
                "A successful decision cannot specify RetryAfter.",
                nameof(retryAfter));
        }

        if (!allowed && retryAfter is not null && retryAfter == TimeSpan.Zero)
        {
            throw new ArgumentException(
                "A rejected decision with RetryAfter must specify a positive duration.",
                nameof(retryAfter));
        }

        Allowed = allowed;
        Limit = limit;
        Remaining = remaining;
        ResetAfter = resetAfter;
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// Gets a value indicating whether the request is allowed.
    /// </summary>
    public bool Allowed { get; }

    /// <summary>
    /// Gets the configured permit limit.
    /// </summary>
    public int Limit { get; }

    /// <summary>
    /// Gets the permits that can currently be accepted immediately.
    /// </summary>
    public int Remaining { get; }

    /// <summary>
    /// Gets the duration until a meaningful reset or recovery point, when available.
    /// </summary>
    public TimeSpan? ResetAfter { get; }

    /// <summary>
    /// Gets the minimum duration before retrying a rejected request, when available.
    /// </summary>
    public TimeSpan? RetryAfter { get; }
}
