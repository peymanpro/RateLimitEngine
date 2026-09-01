using System.Diagnostics.Metrics;
using RateLimitEngine.Core.Observability;
using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.IntegrationTests.Redis;

public sealed class RedisScriptRetryTests
{
    [Fact]
    public async Task ShouldNotRetryByDefault()
    {
        var exception =
            new RedisConnectionException(
                ConnectionFailureType.UnableToConnect,
                CommandFlags.None,
                "connection failure");

        var inner =
            new SequenceExecutor(exception);

        var executor =
            new RetryingRedisScriptExecutor(
                inner,
                new RedisRetryOptions());

        var resultException =
            await Assert.ThrowsAsync<RedisConnectionException>(
                async () =>
                    await executor.ExecuteAsync(
                        "return 1",
                        Array.Empty<RedisKey>(),
                        Array.Empty<RedisValue>()));

        Assert.Same(exception, resultException);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task ShouldRetryConfiguredConnectionFailuresUntilSuccess()
    {
        var inner =
            new SequenceExecutor(
                new RedisConnectionException(
                    ConnectionFailureType.UnableToConnect,
                    CommandFlags.None,
                    "transient-1"),
                new RedisConnectionException(
                    ConnectionFailureType.UnableToConnect,
                    CommandFlags.None,
                    "transient-2"),
                RedisResult.Create((RedisValue)"ok"));

        var executor =
            new RetryingRedisScriptExecutor(
                inner,
                new RedisRetryOptions
                {
                    MaxRetryAttempts = 2
                });

        var result =
            await executor.ExecuteAsync(
                "return 1",
                Array.Empty<RedisKey>(),
                Array.Empty<RedisValue>());

        Assert.Equal(
            "ok",
            (string)result!);

        Assert.Equal(3, inner.CallCount);
    }

    [Fact]
    public async Task ShouldRecordRetryMetric()
    {
        using var listener = new MeterListener();

        long retryAttempts = 0;

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
                    "ratelimit.redis.retry.attempts")
                {
                    retryAttempts += measurement;
                }
            });

        listener.Start();

        var inner =
            new SequenceExecutor(
                new RedisConnectionException(
                    ConnectionFailureType.UnableToConnect,
                    CommandFlags.None,
                    "transient-1"),
                new RedisConnectionException(
                    ConnectionFailureType.UnableToConnect,
                    CommandFlags.None,
                    "transient-2"),
                RedisResult.Create((RedisValue)"ok"));

        var executor =
            new RetryingRedisScriptExecutor(
                inner,
                new RedisRetryOptions
                {
                    MaxRetryAttempts = 2
                });

        var result =
            await executor.ExecuteAsync(
                "return 1",
                Array.Empty<RedisKey>(),
                Array.Empty<RedisValue>());

        Assert.Equal(
            "ok",
            (string)result!);

        Assert.Equal(2, retryAttempts);
        Assert.Equal(3, inner.CallCount);
    }
    [Fact]
    public async Task ShouldStopAtConfiguredRetryLimit()
    {
        var first =
            new RedisConnectionException(
                ConnectionFailureType.UnableToConnect,
                CommandFlags.None,
                "transient-1");

        var second =
            new RedisConnectionException(
                ConnectionFailureType.UnableToConnect,
                CommandFlags.None,
                "transient-2");

        var inner =
            new SequenceExecutor(
                first,
                second);

        var executor =
            new RetryingRedisScriptExecutor(
                inner,
                new RedisRetryOptions
                {
                    MaxRetryAttempts = 1
                });

        var resultException =
            await Assert.ThrowsAsync<RedisConnectionException>(
                async () =>
                    await executor.ExecuteAsync(
                        "return 1",
                        Array.Empty<RedisKey>(),
                        Array.Empty<RedisValue>()));

        Assert.Same(second, resultException);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task ShouldNotRetryNonConnectionException()
    {
        var exception =
            new InvalidOperationException(
                "non-retryable failure");

        var inner =
            new SequenceExecutor(exception);

        var executor =
            new RetryingRedisScriptExecutor(
                inner,
                new RedisRetryOptions
                {
                    MaxRetryAttempts = 3
                });

        var resultException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await executor.ExecuteAsync(
                        "return 1",
                        Array.Empty<RedisKey>(),
                        Array.Empty<RedisValue>()));

        Assert.Same(exception, resultException);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task ShouldPropagateCancellationDuringRetryDelay()
    {
        var inner =
            new SequenceExecutor(
                new RedisConnectionException(
                    ConnectionFailureType.UnableToConnect,
                    CommandFlags.None,
                    "transient"));

        var executor =
            new RetryingRedisScriptExecutor(
                inner,
                new RedisRetryOptions
                {
                    MaxRetryAttempts = 3,
                    RetryDelay = TimeSpan.FromSeconds(10)
                });

        using var cancellationTokenSource =
            new CancellationTokenSource();

        var task =
            executor.ExecuteAsync(
                "return 1",
                Array.Empty<RedisKey>(),
                Array.Empty<RedisValue>(),
                cancellationTokenSource.Token);

        await Task.Delay(50);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await task);

        Assert.Equal(1, inner.CallCount);
    }

    private sealed class SequenceExecutor :
        IRedisScriptExecutor
    {
        private readonly Queue<object> _results;

        public int CallCount { get; private set; }

        public SequenceExecutor(params object[] results)
        {
            _results = new Queue<object>(results);
        }

        public Task<RedisResult> ExecuteAsync(
            string script,
            RedisKey[] keys,
            RedisValue[] values,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            var result = _results.Dequeue();

            return result switch
            {
                Exception exception =>
                    Task.FromException<RedisResult>(exception),

                RedisResult redisResult =>
                    Task.FromResult(redisResult),

                _ =>
                    Task.FromException<RedisResult>(
                        new InvalidOperationException())
            };
        }
    }
}
