using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RateLimitEngine.Core.Observability;

public static class RateLimitEngineMetrics
{
    public const string MeterName = "RateLimitEngine";

    public static readonly Meter Meter =
        new(MeterName);

    public static readonly Counter<long> AllowedRequests =
        Meter.CreateCounter<long>(
            "ratelimit.requests.allowed",
            unit: "{request}",
            description: "Rate-limit requests allowed by the engine.");

    public static readonly Counter<long> RejectedRequests =
        Meter.CreateCounter<long>(
            "ratelimit.requests.rejected",
            unit: "{request}",
            description: "Rate-limit requests rejected by the engine.");

    public static readonly Histogram<double> EvaluationDuration =
        Meter.CreateHistogram<double>(
            "ratelimit.evaluation.duration",
            unit: "ms",
            description: "Rate-limit evaluation duration.");

    public static TagList CreateTags(
        string algorithm,
        string backend)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(backend);

        var tags = new TagList();

        tags.Add("rate_limit.algorithm", algorithm);
        tags.Add("rate_limit.backend", backend);

        return tags;
    }
}

