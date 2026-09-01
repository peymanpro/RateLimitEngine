using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using RateLimitEngine.Algorithms.InMemory;
using RateLimitEngine.Algorithms.TokenBucket;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Observability;
using RateLimitEngine.Redis.Infrastructure;
using RateLimitEngine.Redis.TokenBucket;
using StackExchange.Redis;

namespace RateLimitEngine.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(Config))]
public class TokenBucketRedisBenchmarks
{
    private ConnectionMultiplexer _connection = null!;
    private IRateLimiter _rawLimiter = null!;
    private IRateLimiter _instrumentedLimiter = null!;
    private RateLimitRequest _rawRequest = null!;
    private RateLimitRequest _instrumentedRequest = null!;
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

        var store =
            new RedisTokenBucketStore(executor);

        var limiter =
            new TokenBucketRateLimiter(
                store,
                new TokenBucketOptions(1_000_000_000));

        _rawLimiter = limiter;

        _instrumentedLimiter =
            new InstrumentedRateLimiter(
                limiter,
                RateLimitAlgorithm.TokenBucket,
                RateLimitBackend.Redis);

        _policy =
            new RateLimitPolicy(
                permitLimit: 1_000_000_000,
                window: TimeSpan.FromMinutes(1));

        _rawRequest =
            new RateLimitRequest(
                $"benchmark-redis-token-raw-{Guid.NewGuid():N}",
                cost: 1);

        _instrumentedRequest =
            new RateLimitRequest(
                $"benchmark-redis-token-instrumented-{Guid.NewGuid():N}",
                cost: 1);
    }

    [Benchmark(Baseline = true)]
    public ValueTask<RateLimitDecision> EvaluateAsync_Raw()
    {
        return _rawLimiter.EvaluateAsync(
            _rawRequest,
            _policy);
    }

    [Benchmark]
    public ValueTask<RateLimitDecision> EvaluateAsync_Instrumented()
    {
        return _instrumentedLimiter.EvaluateAsync(
            _instrumentedRequest,
            _policy);
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
                    .WithWarmupCount(10)
                    .WithIterationCount(20));
        }
    }
}
