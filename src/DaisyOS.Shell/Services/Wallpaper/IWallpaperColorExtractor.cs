using Avalonia.Media;

namespace DaisyOS.Shell.Services.Wallpaper;

public interface IWallpaperColorExtractor
{
    Task<Color> ExtractSeedAsync(string wallpaperUri, CancellationToken cancellationToken = default);

    Task<Color> ExtractSeedAsync(Stream imageStream, CancellationToken cancellationToken = default);
}
