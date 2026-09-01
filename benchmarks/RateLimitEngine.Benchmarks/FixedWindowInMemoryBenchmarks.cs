using BenchmarkDotNet.Attributes;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Observability;
using RateLimitEngine.Algorithms.FixedWindow;
using RateLimitEngine.Algorithms.InMemory;

namespace RateLimitEngine.Benchmarks;

[MemoryDiagnoser]
public class FixedWindowInMemoryBenchmarks
{
    private FixedWindowRateLimiter _rawLimiter = null!;
    private IRateLimiter _instrumentedLimiter = null!;
    private RateLimitRequest _request = null!;
    private RateLimitPolicy _policy = null!;

    [GlobalSetup]
    public void Setup()
    {
        var clock =
            new RateLimitEngine.Core.Time.SystemClock();

        _rawLimiter =
            new FixedWindowRateLimiter(
                new InMemoryFixedWindowStore(clock));

        _instrumentedLimiter =
            new InstrumentedRateLimiter(
                _rawLimiter,
                RateLimitAlgorithm.FixedWindow,
                RateLimitBackend.InMemory);

        _request =
            new RateLimitRequest(
                "benchmark-client",
                cost: 1);

        _policy =
            new RateLimitPolicy(
                permitLimit: int.MaxValue,
                window: TimeSpan.FromMinutes(1));
    }

    [Benchmark(Baseline = true)]
    public ValueTask<RateLimitDecision> EvaluateAsync_Raw()
    {
        return _rawLimiter.EvaluateAsync(
            _request,
            _policy);
    }

    [Benchmark]
    public ValueTask<RateLimitDecision> EvaluateAsync_Instrumented()
    {
        return _instrumentedLimiter.EvaluateAsync(
            _request,
            _policy);
    }
}
