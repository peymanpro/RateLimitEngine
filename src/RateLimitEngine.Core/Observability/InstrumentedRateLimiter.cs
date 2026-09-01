using System.Diagnostics;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;

namespace RateLimitEngine.Core.Observability;

public sealed class InstrumentedRateLimiter : IRateLimiter
{
    private readonly IRateLimiter _inner;
    private readonly string _algorithm;
    private readonly string _backend;

    public InstrumentedRateLimiter(
        IRateLimiter inner,
        RateLimitAlgorithm algorithm,
        RateLimitBackend backend)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
        _algorithm = algorithm.ToString();
        _backend = backend.ToString();
    }

    public async ValueTask<RateLimitDecision> EvaluateAsync(
        RateLimitRequest request,
        RateLimitPolicy policy,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var decision =
                await _inner.EvaluateAsync(
                    request,
                    policy,
                    cancellationToken);

            var tags =
                RateLimitEngineMetrics.CreateTags(
                    _algorithm,
                    _backend);

            if (decision.Allowed)
            {
                RateLimitEngineMetrics.AllowedRequests.Add(
                    1,
                    tags);
            }
            else
            {
                RateLimitEngineMetrics.RejectedRequests.Add(
                    1,
                    tags);
            }

            return decision;
        }
        finally
        {
            stopwatch.Stop();

            var tags =
                RateLimitEngineMetrics.CreateTags(
                    _algorithm,
                    _backend);

            RateLimitEngineMetrics.EvaluationDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                tags);
        }
    }
}
