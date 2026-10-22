using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RateLimitEngine.Algorithms;
using RateLimitEngine.AspNetCore;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Time;

namespace RateLimitEngine.IntegrationTests;

public sealed class AspNetCoreTestFactory
    : WebApplicationFactory<Program>
{
    private readonly string _testKey =
        $"integration-test-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(
            services =>
            {
                services.RemoveAll<IClock>();
                services.RemoveAll<IRateLimitKeyResolver>();
                services.RemoveAll<RateLimitOptions>();

                // Use a fixed instant so this test cannot cross a wall-clock window boundary.
                services.AddSingleton<IClock, FixedTestClock>();

                services.AddRateLimitEngineInMemory();

                services.AddSingleton(
                    new RateLimitOptions
                    {
                        Backend =
                            RateLimitBackend.InMemory,

                        Algorithm =
                            RateLimitAlgorithm.FixedWindow,

                        FailureStrategy =
                            RateLimitFailureStrategy.FailOpen,

                        PermitLimit = 5,
                        Window = TimeSpan.FromSeconds(10),
                        Cost = 1
                    });

                services.AddSingleton<IRateLimitKeyResolver>(
                    new TestRateLimitKeyResolver(_testKey));
            });
    }

    private sealed class FixedTestClock : IClock
    {
        private static readonly DateTimeOffset Instant =
            new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public DateTimeOffset UtcNow => Instant;

        public long GetTimestamp() => Instant.Ticks;

        public TimeSpan GetElapsedTime(long startingTimestamp) =>
            TimeSpan.FromTicks(Instant.Ticks - startingTimestamp);
    }

    private sealed class TestRateLimitKeyResolver
        : IRateLimitKeyResolver
    {
        private readonly string _key;

        public TestRateLimitKeyResolver(string key)
        {
            _key = key;
        }

        public string Resolve(
            Microsoft.AspNetCore.Http.HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return _key;
        }
    }
}
