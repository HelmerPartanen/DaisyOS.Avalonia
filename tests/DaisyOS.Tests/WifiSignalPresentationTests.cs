using DaisyOS.Shell.Helpers;
using Xunit;

namespace DaisyOS.Tests;

public sealed class WifiSignalPresentationTests
{
    [Theory]
    [InlineData(0, "signal_wifi_0_bar", "No Wi-Fi signal")]
    [InlineData(1, "network_wifi_1_bar", "Weak Wi-Fi signal")]
    [InlineData(25, "network_wifi_1_bar", "Weak Wi-Fi signal")]
    [InlineData(26, "network_wifi_2_bar", "Fair Wi-Fi signal")]
    [InlineData(50, "network_wifi_2_bar", "Fair Wi-Fi signal")]
    [InlineData(51, "network_wifi_3_bar", "Good Wi-Fi signal")]
    [InlineData(75, "network_wifi_3_bar", "Good Wi-Fi signal")]
    [InlineData(76, "signal_wifi_4_bar", "Strong Wi-Fi signal")]
    [InlineData(100, "signal_wifi_4_bar", "Strong Wi-Fi signal")]
    public void SignalStrengthUsesFamiliarFourLevelWifiBars(
        int signalPercent,
        string expectedGlyph,
        string expectedLabel)
    {
        Assert.Equal(expectedGlyph, WifiSignalPresentation.Glyph(signalPercent));
        Assert.Equal(expectedLabel, WifiSignalPresentation.Label(signalPercent));
    }

    [Theory]
    [InlineData(-20, "signal_wifi_0_bar")]
    [InlineData(140, "signal_wifi_4_bar")]
    public void SignalStrengthClampsUnexpectedValues(int signalPercent, string expectedGlyph)
    {
        Assert.Equal(expectedGlyph, WifiSignalPresentation.Glyph(signalPercent));
    }
}
