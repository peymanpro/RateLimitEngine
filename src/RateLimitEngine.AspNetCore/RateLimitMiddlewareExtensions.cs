using Microsoft.AspNetCore.Builder;
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

    public static IApplicationBuilder UseRateLimitEngine(
        this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<RateLimitMiddleware>();
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
