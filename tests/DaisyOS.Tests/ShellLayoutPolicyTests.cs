using DaisyOS.Shell.Layout;
using Xunit;

namespace DaisyOS.Tests;

public sealed class ShellLayoutPolicyTests
{
    [Theory]
    [InlineData(1024, 768, ShellLayoutProfile.Compact)]
    [InlineData(1366, 768, ShellLayoutProfile.Standard)]
    [InlineData(1920, 1080, ShellLayoutProfile.Standard)]
    [InlineData(2560, 1440, ShellLayoutProfile.Spacious)]
    [InlineData(3840, 2160, ShellLayoutProfile.Spacious)]
    public void SelectProfileUsesLogicalViewport(double width, double height, ShellLayoutProfile expected) =>
        Assert.Equal(expected, ShellLayoutPolicy.SelectProfile(width, height));

    [Theory]
    [InlineData(680, 1366, 12, 680)]
    [InlineData(680, 640, 12, 616)]
    [InlineData(440, 420, 16, 388)]
    public void ClampOverlayWidthKeepsSurfacesInsideViewport(double requested, double viewport, double inset, double expected) =>
        Assert.Equal(expected, ShellLayoutPolicy.ClampOverlayWidth(requested, viewport, inset));
}
