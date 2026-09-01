using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using RateLimitEngine.Algorithms.FixedWindow;
using RateLimitEngine.Algorithms.Gcra;
using RateLimitEngine.Algorithms.InMemory;
using RateLimitEngine.Algorithms.SlidingWindow;
using RateLimitEngine.Algorithms.TokenBucket;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Observability;
using RateLimitEngine.Core.Time;

namespace RateLimitEngine.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(Config))]
public class InMemoryConcurrencyBenchmarks
{
    [Params(
        "FixedWindow",
        "TokenBucket",
        "SlidingWindow",
        "Gcra")]
    public string Algorithm { get; set; } = "FixedWindow";

    [Params(1, 2, 4, 8, 16)]
    public int Concurrency { get; set; }

    [Params(1_000)]
    public int OperationsPerWorker { get; set; }

    private IRateLimiter _rawLimiter = null!;
    private IRateLimiter _instrumentedLimiter = null!;
    private RateLimitRequest _request = null!;
    private RateLimitPolicy _policy = null!;

    [GlobalSetup]
    public void Setup()
    {
        var clock = new SystemClock();

        switch (Algorithm)
        {
            case "FixedWindow":
            {
                var store = new InMemoryFixedWindowStore(clock);
                _rawLimiter = new FixedWindowRateLimiter(store);
                _policy = new RateLimitPolicy(
                    permitLimit: 1_000_000_000,
                    window: TimeSpan.FromMinutes(1));
                break;
            }

            case "TokenBucket":
            {
                var store = new InMemoryTokenBucketStore(clock);
                _rawLimiter = new TokenBucketRateLimiter(
                    store,
                    new TokenBucketOptions(1_000_000_000));

                _policy = new RateLimitPolicy(
                    permitLimit: 1_000_000_000,
                    window: TimeSpan.FromMinutes(1));
                break;
            }

            case "SlidingWindow":
            {
                var store = new InMemorySlidingWindowStore(clock);
                _rawLimiter = new SlidingWindowRateLimiter(store);

                _policy = new RateLimitPolicy(
                    permitLimit: 1_000_000_000,
                    window: TimeSpan.FromMinutes(1));
                break;
            }

            case "Gcra":
            {
                var store = new InMemoryGcraStore(clock);
                _rawLimiter = new GcraRateLimiter(store);

                _policy = new RateLimitPolicy(
                    permitLimit: 1_000_000_000,
                    window: TimeSpan.FromMinutes(1));
                break;
            }

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(Algorithm),
                    Algorithm,
                    null);
        }

        var rateLimitAlgorithm = Algorithm switch
        {
            "FixedWindow" => RateLimitAlgorithm.FixedWindow,
            "TokenBucket" => RateLimitAlgorithm.TokenBucket,
            "SlidingWindow" => RateLimitAlgorithm.SlidingWindow,
            "Gcra" => RateLimitAlgorithm.Gcra,
            _ => throw new ArgumentOutOfRangeException(
                nameof(Algorithm),
                Algorithm,
                null)
        };

        _instrumentedLimiter = new InstrumentedRateLimiter(
            _rawLimiter,
            rateLimitAlgorithm,
            RateLimitBackend.InMemory);

        _request = new RateLimitRequest(
            $"benchmark-concurrency-{Algorithm}-{Guid.NewGuid():N}",
            cost: 1);
    }

    [Benchmark(Baseline = true)]
    public Task EvaluateConcurrentAsync_Raw()
    {
        return ExecuteConcurrentAsync(_rawLimiter);
    }

    [Benchmark]
    public Task EvaluateConcurrentAsync_Instrumented()
    {
        return ExecuteConcurrentAsync(_instrumentedLimiter);
    }

    private Task ExecuteConcurrentAsync(IRateLimiter limiter)
    {
        var workers = new Task[Concurrency];

        for (var worker = 0; worker < Concurrency; worker++)
        {
            workers[worker] = Task.Run(async () =>
            {
                for (var i = 0; i < OperationsPerWorker; i++)
                {
                    await limiter.EvaluateAsync(
                        _request,
                        _policy);
                }
            });
        }

        return Task.WhenAll(workers);
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