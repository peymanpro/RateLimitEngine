using System.Diagnostics;
using Xunit;

namespace RateLimitEngine.DistributedTests.Redis;

public sealed class RedisDockerFixture : IAsyncLifetime
{
    public const string ContainerName =
        "ratelimitengine-test-redis";

    public const int HostPort = 6381;

    public string ConnectionString =>
        $"localhost:{HostPort}," +
        "abortConnect=false," +
        "connectTimeout=1000," +
        "asyncTimeout=1000," +
        "syncTimeout=1000";

    public async Task InitializeAsync()
    {
        await RunDockerIgnoringFailureAsync(
            "rm",
            "-f",
            ContainerName);

        await RunDockerAsync(
            "run",
            "-d",
            "--name",
            ContainerName,
            "-p",
            $"{HostPort}:6379",
            "redis:7-alpine",
            "redis-server",
            "--appendonly",
            "yes",
            "--appendfsync",
            "always");

        await WaitForRedisAsync();
    }

    public async Task DisposeAsync()
    {
        await RunDockerIgnoringFailureAsync(
            "rm",
            "-f",
            ContainerName);
    }

    public async Task StopAsync()
    {
        await RunDockerAsync(
            "stop",
            ContainerName);
    }

    public async Task StartAsync()
    {
        await RunDockerAsync(
            "start",
            ContainerName);

        await WaitForRedisAsync();
    }

    private async Task WaitForRedisAsync()
    {
        var deadline =
            DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var connection =
                    await StackExchange.Redis.ConnectionMultiplexer
                        .ConnectAsync(ConnectionString);

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

        throw new TimeoutException(
            $"Redis did not become ready on port {HostPort}.");
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
                $"Docker command failed with exit code {result.ExitCode}."
                + Environment.NewLine
                + result.StdErr);
        }
    }

    private static async Task RunDockerIgnoringFailureAsync(
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
