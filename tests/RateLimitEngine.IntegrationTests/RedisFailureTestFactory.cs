using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RateLimitEngine.AspNetCore;
using RateLimitEngine.Core.Abstractions;
using StackExchange.Redis;

namespace RateLimitEngine.IntegrationTests;

public sealed class RedisFailureTestFactory
    : WebApplicationFactory<Program>
{
    private readonly string _failureStrategy;

    public RedisFailureTestFactory(
        string failureStrategy)
    {
        _failureStrategy = failureStrategy;
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(
            services =>
            {
                var failureConnection =
                    ConnectionMultiplexer.Connect(
                        "192.0.2.1:6399," +
                        "abortConnect=false," +
                        "connectTimeout=100," +
                        "syncTimeout=100," +
                        "asyncTimeout=100");

                var failureDatabase =
                    failureConnection.GetDatabase();

                services.RemoveAll<IDatabase>();
                services.RemoveAll<IConnectionMultiplexer>();
                services.RemoveAll<RateLimitOptions>();

                services.AddSingleton(
                    failureConnection);

                services.AddSingleton<IDatabase>(
                    failureDatabase);

                services.AddSingleton(
                    new RateLimitOptions
                    {
                        Backend = RateLimitEngine.Core.Models.RateLimitBackend.Redis,
                        Algorithm = RateLimitEngine.Core.Models.RateLimitAlgorithm.FixedWindow,
                        FailureStrategy = Enum.Parse<RateLimitFailureStrategy>(
                            _failureStrategy),
                        PermitLimit = 5,
                        Window = TimeSpan.FromSeconds(10),
                        Cost = 1
                    });
            });
    }
}
