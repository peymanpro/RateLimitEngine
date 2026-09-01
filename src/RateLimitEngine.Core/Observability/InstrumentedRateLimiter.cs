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

        using var activity =
            RateLimitEngineDiagnostics.ActivitySource.StartActivity(
                "RateLimitEngine.Evaluate");

        activity?.SetTag(
            "rate_limit.algorithm",
            _algorithm);

        activity?.SetTag(
            "rate_limit.backend",
            _backend);

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

                activity?.SetTag(
                    "rate_limit.allowed",
                    true);
            }
            else
            {
                RateLimitEngineMetrics.RejectedRequests.Add(
                    1,
                    tags);

                activity?.SetTag(
                    "rate_limit.allowed",
                    false);
            }

            return decision;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            RateLimitEngineMetrics.EvaluationFailures.Add(
                1,
                RateLimitEngineMetrics.CreateFailureTags(
                    _algorithm,
                    _backend,
                    exception));

            activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            activity?.AddEvent(
                new ActivityEvent(
                    "exception",
                    tags: new ActivityTagsCollection
                    {
                        { "exception.type", exception.GetType().FullName },
                        { "exception.message", exception.Message }
                    }));

            throw;
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
