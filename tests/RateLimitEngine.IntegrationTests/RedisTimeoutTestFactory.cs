using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RateLimitEngine.AspNetCore;
using StackExchange.Redis;

namespace RateLimitEngine.IntegrationTests;

public sealed class RedisTimeoutTestFactory
    : WebApplicationFactory<Program>
{
    private readonly string _failureStrategy;

    public RedisTimeoutTestFactory(
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
                var configuration =
                    ConfigurationOptions.Parse(
                        "localhost:6382");

                configuration.AbortOnConnectFail = false;
                configuration.ConnectTimeout = 1000;
                configuration.SyncTimeout = 100;
                configuration.AsyncTimeout = 100;

                var connection =
                    ConnectionMultiplexer.Connect(
                        configuration);

                var database =
                    connection.GetDatabase();

                services.RemoveAll<IConnectionMultiplexer>();
                services.RemoveAll<IDatabase>();
                services.RemoveAll<RateLimitOptions>();

                services.AddSingleton<IConnectionMultiplexer>(
                    connection);

                services.AddSingleton<IDatabase>(
                    database);

                services.AddSingleton(
                    new RateLimitOptions
                    {
                        Backend =
                            RateLimitEngine.Core.Models
                                .RateLimitBackend.Redis,

                        Algorithm =
                            RateLimitEngine.Core.Models
                                .RateLimitAlgorithm.FixedWindow,

                        FailureStrategy =
                            Enum.Parse<RateLimitFailureStrategy>(
                                _failureStrategy),

                        PermitLimit = 5,
                        Window = TimeSpan.FromSeconds(10),
                        Cost = 1
                    });
            });
    }
}
