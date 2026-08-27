using RateLimitEngine.Algorithms;
using RateLimitEngine.AspNetCore;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Redis;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var rateLimitSection =
    builder.Configuration.GetSection("RateLimit");

var backendName =
    rateLimitSection["Backend"] ?? "InMemory";

if (!Enum.TryParse<RateLimitBackend>(
        backendName,
        ignoreCase: true,
        out var backend))
{
    throw new InvalidOperationException(
        $"Unsupported rate limit backend '{backendName}'.");
}

if (backend == RateLimitBackend.Redis)
{
    var connectionString =
        builder.Configuration.GetConnectionString("Redis")
        ?? "localhost:6379";

    var connection =
        await ConnectionMultiplexer.ConnectAsync(connectionString);

    builder.Services.AddSingleton<IConnectionMultiplexer>(
        connection);

    builder.Services.AddRateLimitEngineRedis(
        connection.GetDatabase());
}
else
{
    builder.Services.AddRateLimitEngineInMemory();
}

builder.Services.AddRateLimitEngine(
    builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimitEngine();

app.MapGet("/weatherforecast", () =>
{
    var summaries = new[]
    {
        "Freezing",
        "Bracing",
        "Chilly",
        "Cool",
        "Mild",
        "Warm",
        "Balmy",
        "Hot",
        "Sweltering",
        "Scorching"
    };

    return Enumerable.Range(1, 5)
        .Select(index =>
            new WeatherForecast(
                DateOnly.FromDateTime(
                    DateTime.Now.AddDays(index)),
                Random.Shared.Next(-20, 55),
                summaries[
                    Random.Shared.Next(
                        summaries.Length)]))
        .ToArray();
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

record WeatherForecast(
    DateOnly Date,
    int TemperatureC,
    string? Summary)
{
    public int TemperatureF =>
        32 + (int)(TemperatureC / 0.5556);
}

public partial class Program;
