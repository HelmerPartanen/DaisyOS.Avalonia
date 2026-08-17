using Avalonia.Media;

namespace DaisyOS.Shell.Services.Wallpaper;

/// <summary>Chooses desktop-caption contrast from the wallpaper itself, independent of shell theme.</summary>
public static class WallpaperLabelContrast
{
    public static readonly Color LightLabel = Color.Parse("#FFF5F5F5");
    public static readonly Color DarkLabel = Color.Parse("#FF1A1A1A");

    public static Color ForWallpaper(Color wallpaperAverage) =>
        RelativeLuminance(wallpaperAverage) >= 0.60 ? DarkLabel : LightLabel;

    private static double RelativeLuminance(Color color) =>
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
