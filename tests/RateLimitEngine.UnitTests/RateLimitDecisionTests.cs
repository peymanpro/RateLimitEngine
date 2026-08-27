using RateLimitEngine.Core.Models;

namespace RateLimitEngine.UnitTests;

public sealed class RateLimitDecisionTests
{
    [Fact]
    public void Constructor_ShouldRejectNonPositiveLimit()
    {
        var action = () =>
            new RateLimitDecision(
                allowed: true,
                limit: 0,
                remaining: 0);

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void Constructor_ShouldRejectNegativeRemaining()
    {
        var action = () =>
            new RateLimitDecision(
                allowed: true,
                limit: 10,
                remaining: -1);

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void Constructor_ShouldAllowRemainingGreaterThanLimit()
    {
        var decision =
            new RateLimitDecision(
                allowed: true,
                limit: 10,
                remaining: 20);

        Assert.Equal(20, decision.Remaining);
    }

    [Fact]
    public void Constructor_ShouldRejectNegativeResetAfter()
    {
        var action = () =>
            new RateLimitDecision(
                allowed: true,
                limit: 10,
                remaining: 5,
                resetAfter: TimeSpan.FromSeconds(-1));

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void Constructor_ShouldRejectNegativeRetryAfter()
    {
        var action = () =>
            new RateLimitDecision(
                allowed: false,
                limit: 10,
                remaining: 0,
                retryAfter: TimeSpan.FromSeconds(-1));

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void Constructor_ShouldRejectRetryAfterForAllowedDecision()
    {
        var action = () =>
            new RateLimitDecision(
                allowed: true,
                limit: 10,
                remaining: 5,
                retryAfter: TimeSpan.FromSeconds(1));

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Constructor_ShouldRejectZeroRetryAfterForRejectedDecision()
    {
        var action = () =>
            new RateLimitDecision(
                allowed: false,
                limit: 10,
                remaining: 0,
                retryAfter: TimeSpan.Zero);

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Constructor_ShouldAllowRejectedDecisionWithoutRetryAfter()
    {
        var decision =
            new RateLimitDecision(
                allowed: false,
                limit: 10,
                remaining: 0);

        Assert.False(decision.Allowed);
        Assert.Null(decision.RetryAfter);
    }
}
