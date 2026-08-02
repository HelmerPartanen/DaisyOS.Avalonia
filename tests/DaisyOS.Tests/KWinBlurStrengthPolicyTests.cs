using DaisyOS.Shell.Services;
using Xunit;

namespace DaisyOS.Tests;

public sealed class KWinBlurStrengthPolicyTests
{
    [Theory]
    [InlineData(-10, 0, "0%")]
    [InlineData(0, 0, "0%")]
    [InlineData(33.4, 33.4, "33%")]
    [InlineData(50, 50, "50%")]
    [InlineData(66.6, 66.6, "67%")]
    [InlineData(100, 100, "100%")]
    [InlineData(110, 100, "100%")]
    public void StrengthRemainsContinuousWithinItsSupportedRange(
        double input, double expectedStrength, string expectedLabel)
    {
        Assert.Equal(expectedStrength, KWinBlurStrengthPolicy.Normalize(input));
        Assert.Equal(expectedLabel, KWinBlurStrengthPolicy.Label(input));
    }
}
