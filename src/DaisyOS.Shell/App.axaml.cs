using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using DaisyOS.Core.Models;
using DaisyOS.Shell.Services.Theming;
using DaisyOS.Shell.Services.Wallpaper;
using DaisyOS.Shell.Views;
using System;
using System.Collections.Generic;

namespace DaisyOS.Shell;

public partial class App : Application
{
    private DynamicThemeService? _dynamicThemeService;
    private IWallpaperService? _wallpaperService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();

            try
            {
                var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow);
                double scaling = topLevel?.RenderScaling ?? 1.0;
                
                var screen = desktop.MainWindow.Screens.Primary;
                double logicalWidth = screen != null ? screen.WorkingArea.Width / scaling : 1920;
                double logicalHeight = screen != null ? screen.WorkingArea.Height / scaling : 1080;

                var wallpaperPath = ShellSettings.DefaultWallpaperUri;
                int physicalWidth = Math.Max(1, checked((int)Math.Ceiling(logicalWidth * scaling)));
                int physicalHeight = Math.Max(1, checked((int)Math.Ceiling(logicalHeight * scaling)));
                var materialRegions = CreateMaterialRegions(physicalWidth, physicalHeight, scaling);
                
                if (this.Resources.MergedDictionaries.Count > 0 && this.Resources.MergedDictionaries[0] is ResourceDictionary osTheme)
                {
                    ResourceDictionary? darkDict = null;
                    ResourceDictionary? lightDict = null;

                    if (osTheme.ThemeDictionaries.TryGetValue(Avalonia.Styling.ThemeVariant.Dark, out var darkTheme) && darkTheme is ResourceDictionary darkThemeDictionary)
                    {
                        darkDict = darkThemeDictionary;
                    }
                    if (osTheme.ThemeDictionaries.TryGetValue(Avalonia.Styling.ThemeVariant.Light, out var lightTheme) && lightTheme is ResourceDictionary lightThemeDictionary)
                    {
                        lightDict = lightThemeDictionary;
                    }

                    var darkBrushes = DaisyOS.Shell.Rendering.MicaMaterialGenerator.GenerateMicaBrushes(
                        wallpaperPath,
                        ReadMicaTheme(darkDict, DaisyOS.Shell.Rendering.MicaTheme.DarkBase),
                        physicalWidth,
                        physicalHeight,
                        materialRegions);

                    var lightBrushes = DaisyOS.Shell.Rendering.MicaMaterialGenerator.GenerateMicaBrushes(
                        wallpaperPath,
                        ReadMicaTheme(lightDict, DaisyOS.Shell.Rendering.MicaTheme.LightBase),
                        physicalWidth,
                        physicalHeight,
                        materialRegions);

                    if (darkDict is not null)
                    {
                        darkDict["TaskbarMaterialBrush"] = darkBrushes["Taskbar"];
                        darkDict["SystemBarMaterialBrush"] = darkBrushes["SystemBar"];
                        darkDict["LauncherMaterialBrush"] = darkBrushes["Launcher"];
                    }

                    if (lightDict is not null)
                    {
                        lightDict["TaskbarMaterialBrush"] = lightBrushes["Taskbar"];
                        lightDict["SystemBarMaterialBrush"] = lightBrushes["SystemBar"];
                        lightDict["LauncherMaterialBrush"] = lightBrushes["Launcher"];
                    }
                }

                _dynamicThemeService = new DynamicThemeService(
                    new WallpaperColorExtractor(),
                    new MaterialDynamicSchemeGenerator());
                _wallpaperService = new WallpaperService(wallpaperPath);
                _wallpaperService.WallpaperChanged += (_, changedWallpaperUri) =>
                    _ = _dynamicThemeService.RefreshFromWallpaperAsync(changedWallpaperUri, ActualThemeVariant);
                _ = _dynamicThemeService.RefreshFromWallpaperAsync(_wallpaperService.CurrentWallpaperUri, ActualThemeVariant);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to generate Mica brush: {ex.Message}");
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IReadOnlyDictionary<string, PixelRect> CreateMaterialRegions(
        int screenWidth,
        int screenHeight,
        double renderScaling)
    {
        int Scale(double logicalPixels) => Math.Max(1, checked((int)Math.Ceiling(logicalPixels * renderScaling)));

        int bottomSurfaceHeight = Scale(52);
        int bottomInset = Scale(4);
        int taskbarWidth = Scale(160);
        int systemBarWidth = Scale(216);
        int launcherWidth = Scale(680);
        int launcherHeight = Scale(640);
        int launcherBottomInset = Scale(64);

        return new Dictionary<string, PixelRect>(StringComparer.Ordinal)
        {
            ["Taskbar"] = new PixelRect(
                (screenWidth - taskbarWidth) / 2,
                screenHeight - bottomInset - bottomSurfaceHeight,
                taskbarWidth,
                bottomSurfaceHeight),
            ["SystemBar"] = new PixelRect(
                screenWidth - Scale(12) - systemBarWidth,
                screenHeight - bottomInset - bottomSurfaceHeight,
                systemBarWidth,
                bottomSurfaceHeight),
            ["Launcher"] = new PixelRect(
                (screenWidth - launcherWidth) / 2,
                screenHeight - launcherBottomInset - launcherHeight,
                launcherWidth,
                launcherHeight)
        };
    }

    private static DaisyOS.Shell.Rendering.MicaTheme ReadMicaTheme(
        ResourceDictionary? themeResources,
        DaisyOS.Shell.Rendering.MicaTheme fallback)
    {
        fallback.TintOpacity = ReadThemeOpacity(themeResources, "MicaTintOpacity", fallback.TintOpacity);
        fallback.LuminosityOpacity = ReadThemeOpacity(themeResources, "MicaLuminosityOpacity", fallback.LuminosityOpacity);

        if (themeResources?.TryGetValue("MicaTintBrush", out var resource) == true &&
            resource is SolidColorBrush tintBrush)
        {
            var color = tintBrush.Color;
            fallback.TintColor = new SkiaSharp.SKColor(color.R, color.G, color.B, color.A);
            var tintAlpha = color.A / 255f;
            fallback.TintOpacity *= tintAlpha;
            fallback.LuminosityOpacity *= tintAlpha;
        }

        return fallback;
    }

    private static float ReadThemeOpacity(ResourceDictionary? themeResources, string key, float fallback)
    {
        if (themeResources?.TryGetValue(key, out var value) == true && value is double opacity)
        {
            return Math.Clamp((float)opacity, 0f, 1f);
        }

        return fallback;
    }

}
