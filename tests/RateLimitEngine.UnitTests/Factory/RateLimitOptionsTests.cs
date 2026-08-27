using RateLimitEngine.AspNetCore;
using RateLimitEngine.Core.Models;

namespace RateLimitEngine.UnitTests.Factory;

public sealed class RateLimitOptionsTests
{
    [Fact]
    public void Validate_ShouldAcceptValidOptions()
    {
        var options = new RateLimitOptions
        {
            Backend = RateLimitBackend.InMemory,
            Algorithm = RateLimitAlgorithm.FixedWindow,
            PermitLimit = 10,
            Window = TimeSpan.FromSeconds(10),
            Cost = 1
        };

        options.Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ShouldRejectInvalidPermitLimit(int value)
    {
        var options = new RateLimitOptions
        {
            PermitLimit = value
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            options.Validate);
    }

    [Fact]
    public void Validate_ShouldRejectInvalidWindow()
    {
        var options = new RateLimitOptions
        {
            Window = TimeSpan.Zero
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            options.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ShouldRejectInvalidCost(int value)
    {
        var options = new RateLimitOptions
        {
            Cost = value
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            options.Validate);
    }

    [Fact]
    public void Validate_ShouldRejectUndefinedBackend()
    {
        var options = new RateLimitOptions
        {
            Backend = (RateLimitBackend)999
        };

        Assert.Throws<InvalidOperationException>(
            options.Validate);
    }

    [Fact]
    public void Validate_ShouldRejectUndefinedAlgorithm()
    {
        var options = new RateLimitOptions
        {
            Algorithm = (RateLimitAlgorithm)999
        };

        Assert.Throws<InvalidOperationException>(
            options.Validate);
    }
}
