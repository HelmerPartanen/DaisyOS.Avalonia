using DaisyOS.Core.Models;

namespace DaisyOS.Shell.Services.Appearance;

/// <summary>Persisted preferences that affect the rendered shell appearance.</summary>
public sealed class AppearanceSettings
{
    public ThemeMode ThemeMode { get; set; } = ThemeMode.Dark;

    public string WallpaperUri { get; set; } = ShellSettings.DefaultWallpaperUri;

    public AccentColor AccentColor { get; set; } = AccentColor.Default;

    public bool HighContrast { get; set; }

    public bool ReduceMotion { get; set; }
}
