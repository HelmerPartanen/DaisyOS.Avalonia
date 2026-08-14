using Avalonia.Controls;
using Avalonia.Media;
using DaisyOS.Shell.Services.Theming;
using Xunit;

namespace DaisyOS.Tests;

public sealed class DynamicThemeServiceTests
{
    [Fact]
    public void ApplyDoesNotModifyDividerBrush()
    {
        var resources = new ResourceDictionary();
        var dividerBrush = new SolidColorBrush(Color.Parse("#26000000"));
        resources["DividerBrush"] = dividerBrush;

        var generator = new MaterialDynamicSchemeGenerator();
        var scheme = generator.Generate(Color.Parse("#6750A4"), isDark: false);

        DynamicThemeService.Apply(resources, scheme, Colors.Black, new DaisyOS.Shell.Services.Wallpaper.WallpaperPalette(Color.Parse("#6750A4"), Color.Parse("#6750A4")));

        Assert.True(resources.ContainsKey("DividerBrush"));
        Assert.Same(dividerBrush, resources["DividerBrush"]);
    }

    [Fact]
    public void ApplyDoesNotReplaceDarkSurfaceBrushes()
    {
        var resources = new ResourceDictionary();
        var primarySurface = new SolidColorBrush(Color.Parse("#FF000000"));
        var secondarySurface = new SolidColorBrush(Color.Parse("#D91C1C1C"));
        var elevatedSurface = new SolidColorBrush(Color.Parse("#F2262628"));
        resources["PrimarySurfaceBrush"] = primarySurface;
        resources["SecondarySurfaceBrush"] = secondarySurface;
        resources["ElevatedSurfaceBrush"] = elevatedSurface;
        var generator = new MaterialDynamicSchemeGenerator();
        var seedColor = Color.FromRgb(255, 0, 0); // Pure Red seed
        var scheme = generator.Generate(seedColor, isDark: true);

        DynamicThemeService.Apply(resources, scheme, Colors.White, new DaisyOS.Shell.Services.Wallpaper.WallpaperPalette(seedColor, seedColor));

        Assert.Same(primarySurface, resources["PrimarySurfaceBrush"]);
        Assert.Same(secondarySurface, resources["SecondarySurfaceBrush"]);
        Assert.Same(elevatedSurface, resources["ElevatedSurfaceBrush"]);
    }

    [Fact]
    public void ApplyUpdatesTheOsThemeAccentWithoutReplacingSurfaceMaterial()
    {
        var resources = new ResourceDictionary();
        var secondarySurface = new SolidColorBrush(Color.Parse("#D9FFFFFF"));
        var primaryAccent = new SolidColorBrush(Color.Parse("#FFD0BCFF"));
        resources["SecondarySurfaceBrush"] = secondarySurface;
        resources["AccentBrush"] = primaryAccent;
        var generator = new MaterialDynamicSchemeGenerator();
        var seedColor = Color.FromRgb(0, 0, 255); // Pure Blue seed
        var scheme = generator.Generate(seedColor, isDark: false);

        DynamicThemeService.Apply(resources, scheme, Colors.Black, new DaisyOS.Shell.Services.Wallpaper.WallpaperPalette(seedColor, seedColor));

        Assert.Same(secondarySurface, resources["SecondarySurfaceBrush"]);
        Assert.NotSame(primaryAccent, resources["AccentBrush"]);
        Assert.Equal(scheme.Primary, Assert.IsType<SolidColorBrush>(resources["AccentBrush"]).Color);
        Assert.Equal(Blend(scheme.Primary, scheme.OnPrimary, 0.08), Assert.IsType<SolidColorBrush>(resources["AccentHoverBrush"]).Color);
        Assert.Equal(Blend(scheme.Primary, scheme.OnPrimary, 0.16), Assert.IsType<SolidColorBrush>(resources["AccentPressedBrush"]).Color);
        Assert.Equal(scheme.OnPrimary, Assert.IsType<SolidColorBrush>(resources["OnAccentBrush"]).Color);
    }

    [Fact]
    public void ApplyCreatesAccentRolesButNotSurfaceMaterialResources()
    {
        var resources = new ResourceDictionary();
        var scheme = new MaterialDynamicSchemeGenerator().Generate(Color.Parse("#6750A4"), isDark: true);

        DynamicThemeService.Apply(resources, scheme, Colors.White, new DaisyOS.Shell.Services.Wallpaper.WallpaperPalette(Color.Parse("#6750A4"), Color.Parse("#6750A4")));

        Assert.False(resources.ContainsKey("PrimarySurfaceBrush"));
        Assert.False(resources.ContainsKey("SecondarySurfaceBrush"));
        Assert.False(resources.ContainsKey("ElevatedSurfaceBrush"));
        Assert.True(resources.ContainsKey("AccentBrush"));
        Assert.True(resources.ContainsKey("AccentHoverBrush"));
        Assert.True(resources.ContainsKey("AccentPressedBrush"));
        Assert.True(resources.ContainsKey("AccentSurfaceBrush"));
    }

    private static Color Blend(Color from, Color to, double amount) =>
        Color.FromArgb(
            (byte)Math.Round(from.A + ((to.A - from.A) * amount)),
            (byte)Math.Round(from.R + ((to.R - from.R) * amount)),
            (byte)Math.Round(from.G + ((to.G - from.G) * amount)),
            (byte)Math.Round(from.B + ((to.B - from.B) * amount)));
}
