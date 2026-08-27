using RateLimitEngine.Core.Models;

namespace RateLimitEngine.AspNetCore;

public sealed class RateLimitOptions
{
    public RateLimitBackend Backend { get; set; } =
        RateLimitBackend.InMemory;

    public RateLimitAlgorithm Algorithm { get; set; } =
        RateLimitAlgorithm.FixedWindow;

    public int PermitLimit { get; set; } = 100;

    public TimeSpan Window { get; set; } =
        TimeSpan.FromMinutes(1);

    public int Cost { get; set; } = 1;
}
