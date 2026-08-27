using RateLimitEngine.Algorithms.TokenBucket;

namespace RateLimitEngine.UnitTests;

public sealed class TokenBucketOptionsTests
{
    [Fact]
    public void Constructor_ShouldRejectNonPositiveCapacity()
    {
        var action = () => new TokenBucketOptions(0);

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void Constructor_ShouldCreateValidOptions()
    {
        var options = new TokenBucketOptions(100);

        Assert.Equal(100, options.Capacity);
    }
}
