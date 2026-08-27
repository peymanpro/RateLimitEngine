namespace RateLimitEngine.Core.Models;

public sealed record RateLimitPolicy
{
    public RateLimitPolicy(
        int permitLimit,
        TimeSpan window)
    {
        if (permitLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(permitLimit),
                permitLimit,
                "Permit limit must be greater than zero.");
        }

        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(window),
                window,
                "Window must be greater than zero.");
        }

        PermitLimit = permitLimit;
        Window = window;
    }

    public int PermitLimit { get; }

    public TimeSpan Window { get; }
}
