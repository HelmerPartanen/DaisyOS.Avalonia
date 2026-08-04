using Avalonia.Media;
using DaisyOS.Shell.Services.Theming;
using Xunit;

namespace DaisyOS.Tests;

public sealed class MaterialDynamicSchemeGeneratorTests
{
    private readonly MaterialDynamicSchemeGenerator _generator = new();

    [Fact]
    public void LightAndDarkSchemesDifferForTheSameSeed()
    {
        var seed = Color.Parse("#6750A4");

        var light = _generator.Generate(seed, isDark: false);
        var dark = _generator.Generate(seed, isDark: true);

        Assert.NotEqual(light.Surface, dark.Surface);
        Assert.NotEqual(light.PrimaryContainer, dark.PrimaryContainer);
    }

    [Fact]
    public void DifferentSeedsProduceDifferentPrimaryContainers()
    {
        var purple = _generator.Generate(Color.Parse("#6750A4"), isDark: true);
        var green = _generator.Generate(Color.Parse("#146C2E"), isDark: true);

        Assert.NotEqual(purple.PrimaryContainer, green.PrimaryContainer);
    }

    [Fact]
    public void DarkSchemeAccentsAreMutedWithoutLosingContrast()
    {
        var scheme = _generator.Generate(Color.Parse("#123012"), isDark: true);

        Assert.InRange(ChannelSpread(scheme.PrimaryContainer), 0, 32);
        Assert.InRange(ChannelSpread(scheme.OnPrimaryContainer), 0, 32);
        Assert.True(Contrast(scheme.PrimaryContainer, scheme.OnPrimaryContainer) >= 4.5);
    }

    [Fact]
    public void LightSchemePrimaryContainerIsTemperedWithoutLosingContrast()
    {
        var scheme = _generator.Generate(Color.Parse("#121D12"), isDark: false);

        Assert.True(Luminance(scheme.PrimaryContainer) < 0.65);
        Assert.True(Contrast(scheme.PrimaryContainer, scheme.OnPrimaryContainer) >= 4.5);
    }

    [Theory]
    [InlineData("#6750A4", false)]
    [InlineData("#6750A4", true)]
    [InlineData("#7B1FA2", true)]
    public void SemanticForegroundsMeetWcagContrastTargets(string hex, bool isDark)
    {
        var scheme = _generator.Generate(Color.Parse(hex), isDark);

        Assert.True(Contrast(scheme.PrimaryContainer, scheme.OnPrimaryContainer) >= 4.5);
        Assert.True(Contrast(scheme.Primary, scheme.OnPrimary) >= 4.5);
        Assert.True(Contrast(scheme.Surface, scheme.OnSurface) >= 4.5);
        Assert.True(Contrast(scheme.SurfaceContainer, scheme.OnSurface) >= 4.5);
        Assert.True(Contrast(scheme.ErrorContainer, scheme.OnErrorContainer) >= 4.5);
    }

    private static double Contrast(Color first, Color second)
    {
        var firstLuminance = Luminance(first);
        var secondLuminance = Luminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05) /
               (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    private static int ChannelSpread(Color color) =>
        Math.Max(color.R, Math.Max(color.G, color.B)) - Math.Min(color.R, Math.Min(color.G, color.B));

    private static double Luminance(Color color) =>
        0.2126 * Linear(color.R) +
        0.7152 * Linear(color.G) +
        0.0722 * Linear(color.B);

    private static double Linear(byte component)
    {
        var value = component / 255d;
        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
