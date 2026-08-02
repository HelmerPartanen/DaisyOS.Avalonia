using DaisyOS.Shell.Layout;
using Xunit;

namespace DaisyOS.Tests;

public sealed class ShellDensityPolicyTests
{
    [Theory]
    [InlineData(1199.99, ShellDensityPolicy.NormalScale)]
    [InlineData(1200, ShellDensityPolicy.NormalScale)]
    [InlineData(1799.99, ShellDensityPolicy.NormalScale)]
    [InlineData(1800, ShellDensityPolicy.NormalScale)]
    [InlineData(2160, ShellDensityPolicy.NormalScale)]
    public void SelectScale_UsesLogicalHeightBands(double height, double expected)
    {
        Assert.Equal(expected, ShellDensityPolicy.SelectScale(height));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void SelectScale_InvalidOrTransientHeight_UsesNormalDensity(double height)
    {
        Assert.Equal(ShellDensityPolicy.NormalScale, ShellDensityPolicy.SelectScale(height));
    }
}
