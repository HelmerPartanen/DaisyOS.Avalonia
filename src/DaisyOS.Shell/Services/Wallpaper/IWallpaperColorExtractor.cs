using Avalonia.Media;

namespace DaisyOS.Shell.Services.Wallpaper;

public record struct WallpaperPalette(Color PrimarySeed, Color SurfaceTint, bool IsGrayscale = false);

public interface IWallpaperColorExtractor
{
    Task<WallpaperPalette> ExtractPaletteAsync(string wallpaperUri, CancellationToken cancellationToken = default);

    Task<WallpaperPalette> ExtractPaletteAsync(Stream imageStream, CancellationToken cancellationToken = default);
}
