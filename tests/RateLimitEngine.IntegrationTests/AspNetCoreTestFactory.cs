using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
                services.RemoveAll<
                    RateLimitEngine.AspNetCore.IRateLimitKeyResolver>();

                services.AddSingleton<
                    RateLimitEngine.AspNetCore.IRateLimitKeyResolver>(
                    new TestRateLimitKeyResolver(_testKey));
            });
    }

    private sealed class TestRateLimitKeyResolver
        : RateLimitEngine.AspNetCore.IRateLimitKeyResolver
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
