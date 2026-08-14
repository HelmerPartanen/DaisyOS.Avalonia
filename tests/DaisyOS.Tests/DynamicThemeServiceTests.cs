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

        DynamicThemeService.Apply(resources, scheme, Colors.Black, new DaisyOS.Shell.Services.Wallpaper.WallpaperPalette(Color.Parse("#6750A4"), Color.Parse("#6750A4")));

        Assert.True(resources.ContainsKey("TaskbarBorderBrush"));
        Assert.Same(staticTaskbarBorderBrush, resources["TaskbarBorderBrush"]);
    }

    [Fact]
    public void ApplyDoesNotReplaceDarkShellMaterialBrushes()
    {
        var resources = new ResourceDictionary();
        var shellSurface = new SolidColorBrush(Color.Parse("#D91E1E1E"));
        var taskbarMaterial = new SolidColorBrush(Color.Parse("#D91E1E1E"));
        var launcherMaterial = new SolidColorBrush(Color.Parse("#D91E1E1E"));
        resources["ShellSurfaceBrush"] = shellSurface;
        resources["TaskbarMaterialBrush"] = taskbarMaterial;
        resources["LauncherMaterialBrush"] = launcherMaterial;
        var generator = new MaterialDynamicSchemeGenerator();
        var seedColor = Color.FromRgb(255, 0, 0); // Pure Red seed
        var scheme = generator.Generate(seedColor, isDark: true);

        DynamicThemeService.Apply(resources, scheme, Colors.White, new DaisyOS.Shell.Services.Wallpaper.WallpaperPalette(seedColor, seedColor));

        Assert.Same(shellSurface, resources["ShellSurfaceBrush"]);
        Assert.Same(taskbarMaterial, resources["TaskbarMaterialBrush"]);
        Assert.Same(launcherMaterial, resources["LauncherMaterialBrush"]);
    }

    [Fact]
    public void ApplyUpdatesTheOsThemeAccentWithoutReplacingSurfaceMaterial()
    {
        var resources = new ResourceDictionary();
        var systemBarMaterial = new SolidColorBrush(Color.Parse("#D9F4F5F7"));
        var primaryAccent = new SolidColorBrush(Color.Parse("#FFD0BCFF"));
        resources["SystemBarMaterialBrush"] = systemBarMaterial;
        resources["AppPrimaryBrush"] = primaryAccent;
        var generator = new MaterialDynamicSchemeGenerator();
        var seedColor = Color.FromRgb(0, 0, 255); // Pure Blue seed
        var scheme = generator.Generate(seedColor, isDark: false);

        DynamicThemeService.Apply(resources, scheme, Colors.Black, new DaisyOS.Shell.Services.Wallpaper.WallpaperPalette(seedColor, seedColor));

        Assert.Same(systemBarMaterial, resources["SystemBarMaterialBrush"]);
        Assert.NotSame(primaryAccent, resources["AppPrimaryBrush"]);
        Assert.Equal(scheme.Primary, Assert.IsType<SolidColorBrush>(resources["AppPrimaryBrush"]).Color);
        Assert.Equal(scheme.OnPrimary, Assert.IsType<SolidColorBrush>(resources["AppOnPrimaryBrush"]).Color);
    }

    [Fact]
    public void ApplyCreatesAccentRolesButNotSurfaceMaterialResources()
    {
        var resources = new ResourceDictionary();
        var scheme = new MaterialDynamicSchemeGenerator().Generate(Color.Parse("#6750A4"), isDark: true);

        DynamicThemeService.Apply(resources, scheme, Colors.White, new DaisyOS.Shell.Services.Wallpaper.WallpaperPalette(Color.Parse("#6750A4"), Color.Parse("#6750A4")));

        Assert.False(resources.ContainsKey("ShellSurfaceBrush"));
        Assert.False(resources.ContainsKey("AppSurfaceBrush"));
        Assert.False(resources.ContainsKey("LauncherMaterialBrush"));
        Assert.False(resources.ContainsKey("SystemBarMaterialBrush"));
        Assert.True(resources.ContainsKey("AppPrimaryBrush"));
        Assert.True(resources.ContainsKey("AppPrimaryContainerBrush"));
    }
}
