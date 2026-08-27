namespace RateLimitEngine.Core.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }

    long GetTimestamp();

    TimeSpan GetElapsedTime(long startingTimestamp);
}
