using RateLimitEngine.Core.Time;

namespace RateLimitEngine.Testing;

public sealed class FakeClock : IClock
{
    private DateTimeOffset _utcNow;

    public FakeClock(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public DateTimeOffset UtcNow => _utcNow;

    public long GetTimestamp() => _utcNow.Ticks;

    public TimeSpan GetElapsedTime(long startingTimestamp) =>
        TimeSpan.FromTicks(_utcNow.Ticks - startingTimestamp);

    public void Advance(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        _utcNow = _utcNow.Add(duration);
    }
}
