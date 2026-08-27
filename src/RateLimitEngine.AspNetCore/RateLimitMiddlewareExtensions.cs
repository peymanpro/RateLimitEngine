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

        Validate(options);

        services.AddSingleton(options);

        services.AddSingleton<IRateLimitKeyResolver,
            RemoteIpRateLimitKeyResolver>();

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

        var permitLimit = ParsePositiveInt(
            section["PermitLimit"],
            100,
            "PermitLimit");

        var windowSeconds = ParsePositiveDouble(
            section["WindowSeconds"],
            60,
            "WindowSeconds");

        var cost = ParsePositiveInt(
            section["Cost"],
            1,
            "Cost");

        var options = new RateLimitOptions
        {
            Algorithm = algorithm,
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            Cost = cost
        };

        Validate(options);

        services.AddSingleton(options);

        services.AddSingleton<IRateLimitKeyResolver,
            RemoteIpRateLimitKeyResolver>();

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

        if (!int.TryParse(value, out var parsed) ||
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
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed) ||
            parsed <= 0)
        {
            throw new InvalidOperationException(
                $"{name} must be a positive number.");
        }

        return parsed;
    }

    private static void Validate(RateLimitOptions options)
    {
        if (options.PermitLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.PermitLimit));
        }

        if (options.Window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.Window));
        }

        if (options.Cost <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.Cost));
        }
    }
}
