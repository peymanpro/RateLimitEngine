using RateLimitEngine.Core.Models;

namespace RateLimitEngine.AspNetCore;

public sealed class RateLimitOptions
{
    public RateLimitBackend Backend { get; set; } =
        RateLimitBackend.InMemory;

    public RateLimitAlgorithm Algorithm { get; set; } =
        RateLimitAlgorithm.FixedWindow;

    public RateLimitFailureStrategy FailureStrategy { get; set; } =
        RateLimitFailureStrategy.FailOpen;

    public int PermitLimit { get; set; } = 100;

    public TimeSpan Window { get; set; } =
        TimeSpan.FromMinutes(1);

    public int Cost { get; set; } = 1;

    public void Validate()
    {
        if (!Enum.IsDefined(Backend))
        {
            throw new InvalidOperationException(
                $"Unsupported rate limit backend '{Backend}'.");
        }

        if (!Enum.IsDefined(Algorithm))
        {
            throw new InvalidOperationException(
                $"Unsupported rate limit algorithm '{Algorithm}'.");
        }

        if (!Enum.IsDefined(FailureStrategy))
        {
            throw new InvalidOperationException(
                $"Unsupported rate limit failure strategy '{FailureStrategy}'.");
        }

        if (PermitLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PermitLimit),
                PermitLimit,
                "PermitLimit must be greater than zero.");
        }

        if (Window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Window),
                Window,
                "Window must be greater than zero.");
        }

        if (Cost <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Cost),
                Cost,
                "Cost must be greater than zero.");
        }
    }
}
