using RateLimitEngine.Algorithms;
using RateLimitEngine.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRateLimitEngineInMemory();

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

    var forecast = Enumerable.Range(1, 5)
        .Select(index =>
            new WeatherForecast(
                DateOnly.FromDateTime(
                    DateTime.Now.AddDays(index)),
                Random.Shared.Next(-20, 55),
                summaries[
                    Random.Shared.Next(
                        summaries.Length)]))
        .ToArray();

    return forecast;
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
