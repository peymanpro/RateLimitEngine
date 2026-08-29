using System.Diagnostics;
using RateLimitEngine.Redis.FixedWindow;
using RateLimitEngine.Redis.Infrastructure;
using StackExchange.Redis;

namespace RateLimitEngine.DistributedTests.Redis;

public sealed class RedisRecoveryTests
{
    private const string ContainerName =
        "ratelimitengine-recovery-test";

    private const int HostPort = 6381;

    [Fact]
    public async Task SameRedisConnection_ShouldRecoverAfterRedisRestart()
    {
        await StopAndRemoveContainerAsync();

        try
        {
            await RunDockerAsync(
                "run",
                "-d",
                "--name",
                ContainerName,
                "-p",
                $"{HostPort}:6379",
                "redis:7-alpine");

            await WaitForRedisAsync();

            await using var connection =
                await ConnectionMultiplexer.ConnectAsync(
                    $"localhost:{HostPort}," +
                    "abortConnect=false," +
                    "connectTimeout=500," +
                    "asyncTimeout=500," +
                    "syncTimeout=500");

            var store = new RedisFixedWindowStore(
                new RedisScriptExecutor(
                    connection.GetDatabase()));

            var key =
                $"redis-recovery-{Guid.NewGuid():N}";

            const int limit = 2;
            var window = TimeSpan.FromSeconds(30);

            var first = await store.IncrementAsync(
                key,
                window,
                limit,
                cost: 1);

            Assert.True(first.Accepted);
            Assert.Equal(1, first.Consumed);

            await RunDockerAsync(
                "stop",
                ContainerName);

            await AssertEventuallyRedisOperationFailsAsync(
                connection);

            await RunDockerAsync(
                "start",
                ContainerName);

            await WaitForRedisAsync();

            await AssertEventuallyRedisOperationSucceedsAsync(
                connection);

            var second = await store.IncrementAsync(
                key,
                window,
                limit,
                cost: 1);

            Assert.True(second.Accepted);
            Assert.Equal(2, second.Consumed);
            Assert.Equal(0, second.Remaining);
        }
        finally
        {
            await StopAndRemoveContainerAsync();
        }
    }

    private static async Task WaitForRedisAsync()
    {
        var deadline =
            DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var connection =
                    await ConnectionMultiplexer.ConnectAsync(
                        $"localhost:{HostPort}," +
                        "abortConnect=false," +
                        "connectTimeout=250," +
                        "asyncTimeout=250," +
                        "syncTimeout=250");

                await connection.GetDatabase().PingAsync();
                return;
            }
            catch
            {
                await Task.Delay(100);
            }
        }

        throw new TimeoutException(
            $"Redis did not become ready on port {HostPort}.");
    }

    private static async Task
        AssertEventuallyRedisOperationFailsAsync(
            IConnectionMultiplexer connection)
    {
        var deadline =
            DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await connection
                    .GetDatabase()
                    .PingAsync();

                await Task.Delay(100);
            }
            catch (RedisException)
            {
                return;
            }
        }

        throw new Xunit.Sdk.XunitException(
            "Redis operation did not fail after the container was stopped.");
    }

    private static async Task
        AssertEventuallyRedisOperationSucceedsAsync(
            IConnectionMultiplexer connection)
    {
        var deadline =
            DateTime.UtcNow.AddSeconds(15);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await connection
                    .GetDatabase()
                    .PingAsync();

                return;
            }
            catch
            {
                await Task.Delay(100);
            }
        }

        throw new Xunit.Sdk.XunitException(
            "The same Redis connection did not recover after restart.");
    }

    private static async Task StopAndRemoveContainerAsync()
    {
        await RunDockerIgnoringFailureAsync(
            "rm",
            "-f",
            ContainerName);
    }

    private static async Task RunDockerAsync(
        params string[] arguments)
    {
        var result =
            await RunProcessAsync(
                "docker",
                arguments);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Docker command failed with exit code {result.ExitCode}." +
                Environment.NewLine +
                result.StdErr);
        }
    }

    private static async Task
        RunDockerIgnoringFailureAsync(
            params string[] arguments)
    {
        await RunProcessAsync(
            "docker",
            arguments);
    }

    private static async Task<(int ExitCode, string StdErr)>
        RunProcessAsync(
            string fileName,
            IReadOnlyList<string> arguments)
    {
        var startInfo =
            new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process =
            new Process
            {
                StartInfo = startInfo
            };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                $"Failed to start process '{fileName}'.");
        }

        var stderr =
            await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return (
            process.ExitCode,
            stderr);
    }
}
