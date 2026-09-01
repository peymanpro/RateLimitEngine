using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using RateLimitEngine.Algorithms.Gcra;
using RateLimitEngine.Algorithms.InMemory;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Observability;

namespace RateLimitEngine.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(Config))]
public class GcraInMemoryBenchmarks
{
    private GcraRateLimiter _rawLimiter = null!;
    private IRateLimiter _instrumentedLimiter = null!;
    private RateLimitRequest _request = null!;
    private RateLimitPolicy _policy = null!;

    [GlobalSetup]
    public void Setup()
    {
        var clock =
            new RateLimitEngine.Core.Time.SystemClock();

        _rawLimiter =
            new GcraRateLimiter(
                new InMemoryGcraStore(clock));

        _instrumentedLimiter =
            new InstrumentedRateLimiter(
                _rawLimiter,
                RateLimitAlgorithm.Gcra,
                RateLimitBackend.InMemory);

        _request =
            new RateLimitRequest(
                "benchmark-client",
                cost: 1);

        _policy =
            new RateLimitPolicy(
                permitLimit: 1_000_000,
                window: TimeSpan.FromMinutes(1));
    }

    [Benchmark(Baseline = true)]
    public ValueTask<RateLimitDecision> EvaluateAsync_Raw()
    {
        return _rawLimiter.EvaluateAsync(_request, _policy);
    }

    [Benchmark]
    public ValueTask<RateLimitDecision> EvaluateAsync_Instrumented()
    {
        return _instrumentedLimiter.EvaluateAsync(_request, _policy);
    }

    private sealed class Config : ManualConfig
    {
        public Config()
        {
            AddJob(
                Job.Default
                    .WithToolchain(InProcessEmitToolchain.Instance)
                    .WithLaunchCount(1)
                    .WithWarmupCount(5)
                    .WithIterationCount(10));
        }
    }
}
