using DaisyOS.Core.Services;
using DaisyOS.Shell.Services;
using Xunit;

namespace DaisyOS.Tests;

public sealed class IdleLockTrackerTests
{
    [Fact]
    public void ExpiresOnlyAfterTimeoutSinceLatestActivity()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 7, 11, 10, 0, 0, TimeSpan.Zero));
        var tracker = new IdleLockTracker(clock, TimeSpan.FromMinutes(5));

        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.False(tracker.IsExpired());

        tracker.RecordActivity();
        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.False(tracker.IsExpired());

        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(tracker.IsExpired());
    }

    [Fact]
    public void RejectsNonPositiveTimeouts()
    {
        var clock = new MutableClock(DateTimeOffset.UnixEpoch);

        Assert.Throws<ArgumentOutOfRangeException>(() => new IdleLockTracker(clock, TimeSpan.Zero));
    }

    private sealed class MutableClock(DateTimeOffset now) : IClockService
    {
        public DateTimeOffset Now { get; private set; } = now;

        public void Advance(TimeSpan duration)
        {
            Now += duration;
        }
    }
}
