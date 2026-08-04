using Avalonia.Media;
using Avalonia.Platform;
using MaterialColorUtilities.Quantize;
using MaterialColorUtilities.Utils;
using SkiaSharp;

namespace DaisyOS.Shell.Services.Wallpaper;

/// <summary>Extracts a scored Material seed from a bounded wallpaper sample.</summary>
public sealed class WallpaperColorExtractor : IWallpaperColorExtractor
{
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
        int step = Math.Max(1, (int)Math.Ceiling(longest / 192d));
        var pixels = new List<uint>(Math.Min(192 * 192, bitmap.Width * bitmap.Height));

        for (int y = 0; y < bitmap.Height; y += step)
        {
            for (int x = 0; x < bitmap.Width; x += step)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var color = bitmap.GetPixel(x, y);
                if (color.Alpha < 24)
                {
                    continue;
                }

                pixels.Add(((uint)color.Alpha << 24) | ((uint)color.Red << 16) | ((uint)color.Green << 8) | color.Blue);
            }
        }

        if (pixels.Count == 0)
        {
            return FallbackSeed;
        }

        var candidates = ImageUtils.ColorsFromImage(pixels.ToArray());
        if (candidates.Count == 0)
        {
            return FallbackSeed;
        }

        uint selected = candidates[0];
        return Color.FromArgb(
            (byte)(selected >> 24),
            (byte)(selected >> 16),
            (byte)(selected >> 8),
            (byte)selected);
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
