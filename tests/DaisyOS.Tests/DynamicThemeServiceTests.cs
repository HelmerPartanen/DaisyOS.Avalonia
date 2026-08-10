using Avalonia.Controls;
using Avalonia.Media;
using DaisyOS.Shell.Services.Theming;
using Xunit;

namespace DaisyOS.Tests;

public sealed class DynamicThemeServiceTests
{
    [Fact]
    public void ApplyDoesNotModifyTaskbarBorderBrush()
    {
        var resources = new ResourceDictionary();
        var staticTaskbarBorderBrush = new SolidColorBrush(Color.Parse("#1F000000"));
        resources["TaskbarBorderBrush"] = staticTaskbarBorderBrush;

        var generator = new MaterialDynamicSchemeGenerator();
        var scheme = generator.Generate(Color.Parse("#6750A4"), isDark: false);

        DynamicThemeService.Apply(resources, scheme, Colors.Black, Color.Parse("#6750A4"));

        Assert.True(resources.ContainsKey("TaskbarBorderBrush"));
        Assert.Same(staticTaskbarBorderBrush, resources["TaskbarBorderBrush"]);
    }

    [Fact]
    public void ApplySetsStrengthenedWallpaperTintBrushesInDarkTheme()
    {
        var resources = new ResourceDictionary();
        var generator = new MaterialDynamicSchemeGenerator();
        var seedColor = Color.FromRgb(255, 0, 0); // Pure Red seed
        var scheme = generator.Generate(seedColor, isDark: true);

        DynamicThemeService.Apply(resources, scheme, Colors.White, seedColor);

        var systemBarBrush = Assert.IsType<SolidColorBrush>(resources["SystemBarMaterialBrush"]);
        var taskbarMaterialBrush = Assert.IsType<SolidColorBrush>(resources["TaskbarMaterialBrush"]);
        var taskbarBgBrush = Assert.IsType<SolidColorBrush>(resources["TaskbarBackgroundBrush"]);

        // Base Dark #121212 (R:18, G:18, B:18) with 30% Red seed (R:255, G:0, B:0)
        // Expected R = 18 + (255-18)*0.30 = 18 + 71.1 = 89
        // Expected G = 18 + (0-18)*0.30 = 18 - 5.4 = 13
        // Expected B = 18 + (0-18)*0.30 = 18 - 5.4 = 13
        Assert.Equal(89, systemBarBrush.Color.R);
        Assert.Equal(13, systemBarBrush.Color.G);
        Assert.Equal(13, systemBarBrush.Color.B);
        Assert.Equal(systemBarBrush.Color, taskbarMaterialBrush.Color);
        Assert.Equal(systemBarBrush.Color, taskbarBgBrush.Color);
    }

    [Fact]
    public void ApplySetsStrengthenedWallpaperTintBrushesInLightTheme()
    {
        var resources = new ResourceDictionary();
        var generator = new MaterialDynamicSchemeGenerator();
        var seedColor = Color.FromRgb(0, 0, 255); // Pure Blue seed
        var scheme = generator.Generate(seedColor, isDark: false);

        DynamicThemeService.Apply(resources, scheme, Colors.Black, seedColor);

        var systemBarBrush = Assert.IsType<SolidColorBrush>(resources["SystemBarMaterialBrush"]);
        var taskbarMaterialBrush = Assert.IsType<SolidColorBrush>(resources["TaskbarMaterialBrush"]);
        var taskbarBgBrush = Assert.IsType<SolidColorBrush>(resources["TaskbarBackgroundBrush"]);

        // Base Light #F4F5F7 (R:244, G:245, B:247) with 14% Blue seed (R:0, G:0, B:255)
        // Expected R = 244 + (0-244)*0.14 = 244 - 34.16 = 210
        // Expected G = 245 + (0-245)*0.14 = 245 - 34.3 = 211
        // Expected B = 247 + (255-247)*0.14 = 247 + 1.12 = 248
        Assert.Equal(210, systemBarBrush.Color.R);
        Assert.Equal(211, systemBarBrush.Color.G);
        Assert.Equal(248, systemBarBrush.Color.B);
        Assert.Equal(systemBarBrush.Color, taskbarMaterialBrush.Color);
        Assert.Equal(systemBarBrush.Color, taskbarBgBrush.Color);
    }
}
