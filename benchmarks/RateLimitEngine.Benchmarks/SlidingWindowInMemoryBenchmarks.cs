using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using RateLimitEngine.Algorithms.InMemory;
using RateLimitEngine.Algorithms.SlidingWindow;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Observability;
using RateLimitEngine.Core.Time;

namespace RateLimitEngine.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(Config))]
public class SlidingWindowInMemoryBenchmarks
{
    private BenchmarkClock _clock = null!;
    private SlidingWindowRateLimiter _rawLimiter = null!;
    private IRateLimiter _instrumentedLimiter = null!;
    private RateLimitRequest _request = null!;
    private RateLimitPolicy _policy = null!;

    [GlobalSetup]
    public void Setup()
    {
        _clock = new BenchmarkClock(DateTimeOffset.UtcNow);

        _rawLimiter =
            new SlidingWindowRateLimiter(
                new InMemorySlidingWindowStore(_clock));

        _instrumentedLimiter =
            new InstrumentedRateLimiter(
                _rawLimiter,
                RateLimitAlgorithm.SlidingWindow,
                RateLimitBackend.InMemory);

        _request =
            new RateLimitRequest(
                "benchmark-client",
                cost: 1);

        _policy =
            new RateLimitPolicy(
                permitLimit: 1,
                window: TimeSpan.FromMilliseconds(1));
    }

    [Benchmark(Baseline = true)]
    public ValueTask<RateLimitDecision> EvaluateAsync_Raw()
    {
        _clock.Advance(TimeSpan.FromMilliseconds(2));

        return _rawLimiter.EvaluateAsync(_request, _policy);
    }

    [Benchmark]
    public ValueTask<RateLimitDecision> EvaluateAsync_Instrumented()
    {
        _clock.Advance(TimeSpan.FromMilliseconds(2));

        return _instrumentedLimiter.EvaluateAsync(_request, _policy);
    }

    private sealed class BenchmarkClock : IClock
    {
        private DateTimeOffset _utcNow;

        public BenchmarkClock(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public DateTimeOffset UtcNow => _utcNow;

        public long GetTimestamp() => _utcNow.Ticks;

        public TimeSpan GetElapsedTime(long startingTimestamp) =>
            TimeSpan.FromTicks(_utcNow.Ticks - startingTimestamp);

        public void Advance(TimeSpan duration) =>
            _utcNow = _utcNow.Add(duration);
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
