using System.Diagnostics.Metrics;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Observability;

namespace RateLimitEngine.UnitTests.Observability;

public sealed class InstrumentedRateLimiterTests
{
    [Fact]
    public async Task EvaluateAsync_ShouldRecordAllowedRequest()
    {
        using var listener = new MeterListener();

        long allowed = 0;
        long rejected = 0;
        long durationMeasurements = 0;

        listener.InstrumentPublished =
            (instrument, meterListener) =>
            {
                if (instrument.Meter.Name ==
                    RateLimitEngineMetrics.MeterName)
                {
                    meterListener.EnableMeasurementEvents(
                        instrument);
                }
            };

        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, _, _) =>
            {
                if (instrument.Name ==
                    "ratelimit.requests.allowed")
                {
                    allowed += measurement;
                }

                if (instrument.Name ==
                    "ratelimit.requests.rejected")
                {
                    rejected += measurement;
                }
            });

        listener.SetMeasurementEventCallback<double>(
            (instrument, _, _, _) =>
            {
                if (instrument.Name ==
                    "ratelimit.evaluation.duration")
                {
                    durationMeasurements++;
                }
            });

        listener.Start();

        var instrumented =
            new InstrumentedRateLimiter(
                new StubRateLimiter(
                    new RateLimitDecision(
                        allowed: true,
                        limit: 10,
                        remaining: 9)),
                RateLimitAlgorithm.FixedWindow,
                RateLimitBackend.InMemory);

        var decision =
            await instrumented.EvaluateAsync(
                new RateLimitRequest("client-1"),
                new RateLimitPolicy(
                    10,
                    TimeSpan.FromMinutes(1)));

        Assert.True(decision.Allowed);
        Assert.Equal(1, allowed);
        Assert.Equal(0, rejected);
        Assert.Equal(1, durationMeasurements);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldRecordRejectedRequest()
    {
        using var listener = new MeterListener();

        long allowed = 0;
        long rejected = 0;

        listener.InstrumentPublished =
            (instrument, meterListener) =>
            {
                if (instrument.Meter.Name ==
                    RateLimitEngineMetrics.MeterName)
                {
                    meterListener.EnableMeasurementEvents(
                        instrument);
                }
            };

        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, _, _) =>
            {
                if (instrument.Name ==
                    "ratelimit.requests.allowed")
                {
                    allowed += measurement;
                }

                if (instrument.Name ==
                    "ratelimit.requests.rejected")
                {
                    rejected += measurement;
                }
            });

        listener.Start();

        var instrumented =
            new InstrumentedRateLimiter(
                new StubRateLimiter(
                    new RateLimitDecision(
                        allowed: false,
                        limit: 10,
                        remaining: 0,
                        retryAfter: TimeSpan.FromSeconds(1))),
                RateLimitAlgorithm.Gcra,
                RateLimitBackend.Redis);

        var decision =
            await instrumented.EvaluateAsync(
                new RateLimitRequest("client-1"),
                new RateLimitPolicy(
                    10,
                    TimeSpan.FromMinutes(1)));

        Assert.False(decision.Allowed);
        Assert.Equal(0, allowed);
        Assert.Equal(1, rejected);
    }

    private sealed class StubRateLimiter : IRateLimiter
    {
        private readonly RateLimitDecision _decision;

        public StubRateLimiter(RateLimitDecision decision)
        {
            _decision = decision;
        }

        public ValueTask<RateLimitDecision> EvaluateAsync(
            RateLimitRequest request,
            RateLimitPolicy policy,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(_decision);
        }
    }
}
