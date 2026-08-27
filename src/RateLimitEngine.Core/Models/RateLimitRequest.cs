namespace RateLimitEngine.Core.Models;

public sealed record RateLimitRequest
{
    public RateLimitRequest(
        string key,
        int cost = 1)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "Rate limit key cannot be null, empty, or whitespace.",
                nameof(key));
        }

        if (cost <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cost),
                cost,
                "Request cost must be greater than zero.");
        }

        Key = key;
        Cost = cost;
    }

    public string Key { get; }

    public int Cost { get; }
}
