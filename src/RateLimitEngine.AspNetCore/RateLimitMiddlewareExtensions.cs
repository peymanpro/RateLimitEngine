using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;

namespace RateLimitEngine.AspNetCore;

public static class RateLimitMiddlewareExtensions
{
    public static IServiceCollection AddRateLimitEngine(
        this IServiceCollection services,
        Action<RateLimitOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new RateLimitOptions();

        configure?.Invoke(options);

        options.Validate();

        services.AddSingleton(options);

        services.AddSingleton<IRateLimitKeyResolver,
            RemoteIpRateLimitKeyResolver>();

        services.AddSingleton<IRateLimiterFactoryProvider,
            RateLimiterFactoryProvider>();

        services.AddSingleton<IRateLimiterFactory,
            ConfigurableRateLimiterFactory>();

        return services;
    }

    public static IServiceCollection AddRateLimitEngine(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "RateLimit")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(sectionName);

        var backendName =
            section["Backend"] ?? "InMemory";

        if (!Enum.TryParse<RateLimitBackend>(
                backendName,
                ignoreCase: true,
                out var backend))
        {
            throw new InvalidOperationException(
                $"Unsupported rate limit backend '{backendName}'.");
        }

        var algorithmName =
            section["Algorithm"] ?? "FixedWindow";

        if (!Enum.TryParse<RateLimitAlgorithm>(
                algorithmName,
                ignoreCase: true,
                out var algorithm))
        {
            throw new InvalidOperationException(
                $"Unsupported rate limit algorithm '{algorithmName}'.");
        }

        var failureStrategyName =
            section["FailureStrategy"] ?? "FailOpen";

        if (!Enum.TryParse<RateLimitFailureStrategy>(
                failureStrategyName,
                ignoreCase: true,
                out var failureStrategy))
        {
            throw new InvalidOperationException(
                $"Unsupported rate limit failure strategy '{failureStrategyName}'.");
        }

        var options = new RateLimitOptions
        {
            Backend = backend,
            Algorithm = algorithm,
            FailureStrategy = failureStrategy,
            PermitLimit = ParsePositiveInt(
                section["PermitLimit"],
                100,
                "PermitLimit"),
            Window = TimeSpan.FromSeconds(
                ParsePositiveDouble(
                    section["WindowSeconds"],
                    60,
                    "WindowSeconds")),
            Cost = ParsePositiveInt(
                section["Cost"],
                1,
                "Cost")
        };

        options.Validate();

        services.AddSingleton(options);

        services.AddSingleton<IRateLimitKeyResolver,
            RemoteIpRateLimitKeyResolver>();

        services.AddSingleton<IRateLimiterFactoryProvider,
            RateLimiterFactoryProvider>();

        services.AddSingleton<IRateLimiterFactory,
            ConfigurableRateLimiterFactory>();

        return services;
    }

    public static IApplicationBuilder UseRateLimitEngine(
        this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<RateLimitMiddleware>();
    }

    private static int ParsePositiveInt(
        string? value,
        int defaultValue,
        string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed) ||
            parsed <= 0)
        {
            throw new InvalidOperationException(
                $"{name} must be a positive integer.");
        }

        return parsed;
    }

    private static double ParsePositiveDouble(
        string? value,
        double defaultValue,
        string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) ||
            parsed <= 0)
        {
            throw new InvalidOperationException(
                $"{name} must be a positive number.");
        }

        return parsed;
    }
}
