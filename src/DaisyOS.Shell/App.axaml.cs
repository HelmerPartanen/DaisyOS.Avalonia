using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using DaisyOS.Core.Models;
using DaisyOS.Shell.Services.Theming;
using DaisyOS.Shell.Services.Wallpaper;
using DaisyOS.Shell.Views;
using System;

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
                var wallpaperPath = ShellSettings.DefaultWallpaperUri;
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
                Console.WriteLine($"Failed to initialize dynamic shell colors: {ex.Message}");
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Applies the requested appearance to the complete shell and refreshes its dynamic palette.</summary>
    public async Task SetShellThemeAsync(ThemeVariant theme)
    {
        RequestedThemeVariant = theme;

        if (_dynamicThemeService is not null)
        {
            var wallpaperUri = _wallpaperService?.CurrentWallpaperUri ?? ShellSettings.DefaultWallpaperUri;
            await _dynamicThemeService.RefreshFromWallpaperAsync(wallpaperUri, theme);
        }
    }

}
