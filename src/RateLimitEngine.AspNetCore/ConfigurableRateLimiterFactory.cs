using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;

namespace RateLimitEngine.AspNetCore;

internal sealed class ConfigurableRateLimiterFactory : IRateLimiterFactory
{
    private readonly IRateLimiterFactoryProvider _provider;
    private readonly RateLimitOptions _options;

    public ConfigurableRateLimiterFactory(
        IRateLimiterFactoryProvider provider,
        RateLimitOptions options)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);

        _provider = provider;
        _options = options;
    }

    public IRateLimiter Create(
        RateLimitAlgorithm algorithm)
    {
        return _provider
            .GetFactory(_options.Backend)
            .Create(algorithm);
    }
}
