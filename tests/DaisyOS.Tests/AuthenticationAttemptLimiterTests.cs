using DaisyOS.Core.Services;
using DaisyOS.Shell.Services;
using Xunit;

namespace DaisyOS.Tests;

public sealed class AuthenticationAttemptLimiterTests
{
    [Fact]
    public void LocksAfterMaximumFailuresAndRecoversAfterCooldown()
    {
        var clock = new MutableClock(DateTimeOffset.Parse("2026-07-12T12:00:00Z"));
        var limiter = new AuthenticationAttemptLimiter(clock, 3, TimeSpan.FromSeconds(30));

        limiter.RegisterFailure();
        limiter.RegisterFailure();
        Assert.False(limiter.IsLockedOut);
        limiter.RegisterFailure();
        Assert.True(limiter.IsLockedOut);
        Assert.Equal(30, limiter.RemainingSeconds);

        clock.Now += TimeSpan.FromSeconds(29);
        limiter.Refresh();
        Assert.True(limiter.IsLockedOut);
        Assert.Equal(1, limiter.RemainingSeconds);

        clock.Now += TimeSpan.FromSeconds(1);
        limiter.Refresh();
        Assert.False(limiter.IsLockedOut);
        Assert.Equal(0, limiter.FailedAttempts);
    }

    private sealed class MutableClock(DateTimeOffset now) : IClockService
    {
        public DateTimeOffset Now { get; set; } = now;
    }
}
