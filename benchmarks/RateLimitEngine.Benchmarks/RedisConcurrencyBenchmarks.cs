using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using RateLimitEngine.Algorithms.FixedWindow;
using RateLimitEngine.Algorithms.Gcra;
using RateLimitEngine.Algorithms.SlidingWindow;
using RateLimitEngine.Algorithms.TokenBucket;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Observability;
using RateLimitEngine.Redis.FixedWindow;
using RateLimitEngine.Redis.Gcra;
using RateLimitEngine.Redis.Infrastructure;
using RateLimitEngine.Redis.SlidingWindow;
using RateLimitEngine.Redis.TokenBucket;
using StackExchange.Redis;

namespace RateLimitEngine.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(Config))]
public class RedisConcurrencyBenchmarks
{
    [Params(
        "FixedWindow",
        "TokenBucket",
        "SlidingWindow",
        "Gcra")]
    public string Algorithm { get; set; } = "FixedWindow";

    [Params(1, 2, 4, 8)]
    public int Concurrency { get; set; }

    [Params(100)]
    public int OperationsPerWorker { get; set; }

    private ConnectionMultiplexer _connection = null!;
    private IRateLimiter _rawLimiter = null!;
    private IRateLimiter _instrumentedLimiter = null!;
    private RateLimitRequest _request = null!;
    private RateLimitPolicy _policy = null!;

    [GlobalSetup]
    public void Setup()
    {
        _connection =
            ConnectionMultiplexer.Connect(
                "localhost:6379," +
                "abortConnect=false," +
                "connectTimeout=1000," +
                "syncTimeout=1000," +
                "asyncTimeout=1000");

        var executor =
            new RedisScriptExecutor(
                _connection.GetDatabase());

        switch (Algorithm)
        {
            case "FixedWindow":
                _rawLimiter =
                    new FixedWindowRateLimiter(
                        new RedisFixedWindowStore(executor));

                _policy =
                    new RateLimitPolicy(
                        permitLimit: 1_000_000_000,
                        window: TimeSpan.FromMinutes(1));
                break;

            case "TokenBucket":
                _rawLimiter =
                    new TokenBucketRateLimiter(
                        new RedisTokenBucketStore(executor),
                        new TokenBucketOptions(1_000_000_000));

                _policy =
                    new RateLimitPolicy(
                        permitLimit: 1_000_000_000,
                        window: TimeSpan.FromMinutes(1));
                break;

            case "SlidingWindow":
                _rawLimiter =
                    new SlidingWindowRateLimiter(
                        new RedisSlidingWindowStore(executor));

                _policy =
                    new RateLimitPolicy(
                        permitLimit: 1_000_000_000,
                        window: TimeSpan.FromMinutes(1));
                break;

            case "Gcra":
                _rawLimiter =
                    new GcraRateLimiter(
                        new RedisGcraStore(executor));

                _policy =
                    new RateLimitPolicy(
                        permitLimit: 1_000_000,
                        window: TimeSpan.FromMinutes(1));
                break;

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

        _instrumentedLimiter =
            new InstrumentedRateLimiter(
                _rawLimiter,
                rateLimitAlgorithm,
                RateLimitBackend.Redis);

        _request =
            new RateLimitRequest(
                $"benchmark-redis-concurrency-{Algorithm}-{Guid.NewGuid():N}",
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

    [GlobalCleanup]
    public void Cleanup()
    {
        _connection.Dispose();
    }

    private sealed class Config : ManualConfig
    {
        public Config()
        {
            AddJob(
                Job.Default
                    .WithToolchain(InProcessEmitToolchain.Instance)
                    .WithLaunchCount(1)
                    .WithWarmupCount(3)
                    .WithIterationCount(5));
        }
    }
}