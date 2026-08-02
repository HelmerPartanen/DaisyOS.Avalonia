using DaisyOS.Shell.Layout;
using Xunit;

namespace DaisyOS.Tests;

public sealed class TwoPaneLayoutPolicyTests
{
    [Theory]
    [InlineData(640, true)]
    [InlineData(899.9, true)]
    [InlineData(900, false)]
    [InlineData(1366, false)]
    public void IsCompact_UsesLogicalWidthBreakpoint(double width, bool expected)
    {
        Assert.Equal(expected, TwoPaneLayoutPolicy.IsCompact(width));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void IsCompact_IgnoresInvalidAndTransientDimensions(double width)
    {
        Assert.False(TwoPaneLayoutPolicy.IsCompact(width));
        Assert.Equal(248, TwoPaneLayoutPolicy.GetSidebarWidth(width, 248));
    }

    [Fact]
    public void GetSidebarWidth_UsesComfortableCompactTarget()
    {
        Assert.Equal(56, TwoPaneLayoutPolicy.GetSidebarWidth(700, 248));
    }
}
