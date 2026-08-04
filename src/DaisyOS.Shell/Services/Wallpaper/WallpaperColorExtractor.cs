using Avalonia.Media;
using Avalonia.Platform;
using SkiaSharp;

namespace DaisyOS.Shell.Services.Wallpaper;

/// <summary>Extracts a representative, low-chroma Material seed from a bounded wallpaper sample.</summary>
public sealed class WallpaperColorExtractor : IWallpaperColorExtractor
{
    private const int MaxSampleEdge = 128;

    public static readonly Color FallbackSeed = Color.Parse("#6750A4");

    public async Task<Color> ExtractSeedAsync(string wallpaperUri, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(() => ExtractSeed(wallpaperUri, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return FallbackSeed;
        }
    }

    private static Color ExtractSeed(string wallpaperUri, CancellationToken cancellationToken)
    {
        using var stream = OpenWallpaper(wallpaperUri);
        using var bitmap = SKBitmap.Decode(stream);
        if (bitmap is null)
        {
            return FallbackSeed;
        }

        int longest = Math.Max(bitmap.Width, bitmap.Height);
        int step = Math.Max(1, (int)Math.Ceiling(longest / (double)MaxSampleEdge));
        long redTotal = 0;
        long greenTotal = 0;
        long blueTotal = 0;
        long alphaTotal = 0;

        for (int y = 0; y < bitmap.Height; y += step)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int x = 0; x < bitmap.Width; x += step)
            {
                var color = bitmap.GetPixel(x, y);
                if (color.Alpha < 24)
                {
                    continue;
                }

                redTotal += color.Red * color.Alpha;
                greenTotal += color.Green * color.Alpha;
                blueTotal += color.Blue * color.Alpha;
                alphaTotal += color.Alpha;
            }
        }

        if (alphaTotal == 0)
        {
            return FallbackSeed;
        }

        var average = Color.FromRgb(
            (byte)(redTotal / alphaTotal),
            (byte)(greenTotal / alphaTotal),
            (byte)(blueTotal / alphaTotal));
        return average;
    }

    private static Stream OpenWallpaper(string wallpaperUri)
    {
        if (Uri.TryCreate(wallpaperUri, UriKind.Absolute, out var uri) && uri.Scheme == "avares")
        {
            return AssetLoader.Open(uri);
        }

        return File.OpenRead(wallpaperUri);
    }
}
