using DaisyOS.Shell.Rendering;
using Xunit;

namespace DaisyOS.ShellDemo.Tests;

public class ShellDemoContractTests
{
    [Fact]
    public void MicaTheme_DarkBaseDefaultsAreValid()
    {
        var theme = MicaTheme.DarkBase;
        Assert.True(theme.TintOpacity > 0f && theme.TintOpacity <= 1f);
        Assert.True(theme.LuminosityOpacity > 0f && theme.LuminosityOpacity <= 1f);
    }

    [Fact]
    public void MicaTheme_LightBaseDefaultsAreValid()
    {
        var theme = MicaTheme.LightBase;
        Assert.True(theme.TintOpacity > 0f && theme.TintOpacity <= 1f);
        Assert.True(theme.LuminosityOpacity > 0f && theme.LuminosityOpacity <= 1f);
    }
}
