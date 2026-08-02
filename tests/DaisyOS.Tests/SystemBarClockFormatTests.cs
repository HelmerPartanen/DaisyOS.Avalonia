using System.Globalization;
using DaisyOS.Shell.ViewModels;
using Xunit;

namespace DaisyOS.Tests;

public sealed class SystemBarClockFormatTests
{
    [Fact]
    public void TimeOnlyUsesTheSelectedRegionsShortTimePattern()
    {
        var culture = CultureInfo.GetCultureInfo("fi-FI");
        var value = new DateTime(2026, 7, 24, 15, 53, 0);

        var formatted = SystemBarViewModel.FormatClockText(value, showDate: false, culture);

        Assert.Equal(value.ToString(culture.DateTimeFormat.ShortTimePattern, culture), formatted);
    }

    [Theory]
    [InlineData("fi-FI", "pe 24.7 15.53")]
    [InlineData("en-US", "Fri 7/24 3:53\u202FPM")]
    public void DateAndTimeFollowsTheSelectedRegionsOrdering(string cultureName, string expected)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var value = new DateTime(2026, 7, 24, 15, 53, 0);

        var formatted = SystemBarViewModel.FormatClockText(value, showDate: true, culture);

        Assert.Equal(expected, formatted);
    }

    [Fact]
    public void TimeZoneOptionsUseFriendlyLocationAndOffsetLabels()
    {
        var option = TimeZoneOptionViewModel.Create(
            "Europe/Helsinki",
            new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal("Europe/Helsinki", option.Id);
        Assert.Equal("(UTC+03:00) Helsinki · Europe", option.DisplayName);
        Assert.DoesNotContain('_', option.DisplayName);
    }
}
