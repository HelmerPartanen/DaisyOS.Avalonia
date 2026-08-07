using System.Collections.Concurrent;
using Avalonia.Media;
using Avalonia.Platform;
using SkiaSharp;

namespace DaisyOS.Shell.Services.Wallpaper;

/// <summary>
/// Extracts a representative, low-chroma Material seed from a wallpaper using fast strided sampling and seed caching.
/// </summary>
public sealed class WallpaperColorExtractor : IWallpaperColorExtractor
{
    private const int MaxSampleEdge = 64;

    public static readonly Color FallbackSeed = Color.Parse("#6750A4");

    private static readonly ConcurrentDictionary<string, Color> s_assetSeedCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, (DateTime lastWrite, long fileSize, Color seed)> s_fileSeedCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<Color> ExtractSeedAsync(string wallpaperUri, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(wallpaperUri))
        {
            return FallbackSeed;
        }

        if (TryGetCachedSeed(wallpaperUri, out var cachedSeed))
        {
            return cachedSeed;
        }

        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var stream = OpenWallpaper(wallpaperUri);
                var seed = ExtractSeed(stream, cancellationToken);
                CacheSeed(wallpaperUri, seed);
                return seed;
            }, cancellationToken).ConfigureAwait(false);
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

    /// <summary>Applies the wallpaper sampler to arbitrary image data, including album artwork.</summary>
    public async Task<Color> ExtractSeedAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(() => ExtractSeed(imageStream, cancellationToken), cancellationToken)
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

    private static bool TryGetCachedSeed(string wallpaperUri, out Color seed)
    {
        if (wallpaperUri.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
        {
            return s_assetSeedCache.TryGetValue(wallpaperUri, out seed);
        }

        if (File.Exists(wallpaperUri))
        {
            if (s_fileSeedCache.TryGetValue(wallpaperUri, out var cached))
            {
                try
                {
                    var fileInfo = new FileInfo(wallpaperUri);
                    if (fileInfo.Exists && fileInfo.LastWriteTimeUtc == cached.lastWrite && fileInfo.Length == cached.fileSize)
                    {
                        seed = cached.seed;
                        return true;
                    }
                }
                catch
                {
                    // Fall back to re-extracting if file stats cannot be retrieved
                }
            }
        }

        seed = default;
        return false;
    }

    private static void CacheSeed(string wallpaperUri, Color seed)
    {
        if (wallpaperUri.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
        {
            s_assetSeedCache[wallpaperUri] = seed;
            return;
        }

        try
        {
            if (File.Exists(wallpaperUri))
            {
                var fileInfo = new FileInfo(wallpaperUri);
                s_fileSeedCache[wallpaperUri] = (fileInfo.LastWriteTimeUtc, fileInfo.Length, seed);
            }
        }
        catch
        {
            // Ignore cache storage errors for non-accessible files
        }
    }

    private static Color ExtractSeed(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var bitmap = SKBitmap.Decode(stream);
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            return FallbackSeed;
        }

        int width = bitmap.Width;
        int height = bitmap.Height;

        int stepX = Math.Max(1, width / MaxSampleEdge);
        int stepY = Math.Max(1, height / MaxSampleEdge);

        long redTotal = 0;
        long greenTotal = 0;
        long blueTotal = 0;
        long alphaTotal = 0;

        var pixels = bitmap.Pixels;
        if (pixels is not null && pixels.Length == width * height)
        {
            for (int y = 0; y < height; y += stepY)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int rowOffset = y * width;
                for (int x = 0; x < width; x += stepX)
                {
                    var color = pixels[rowOffset + x];
                    if (color.Alpha < 24)
                    {
                        continue;
                    }

                    redTotal += (long)color.Red * color.Alpha;
                    greenTotal += (long)color.Green * color.Alpha;
                    blueTotal += (long)color.Blue * color.Alpha;
                    alphaTotal += color.Alpha;
                }
            }
        }
        else
        {
            for (int y = 0; y < height; y += stepY)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x < width; x += stepX)
                {
                    var color = bitmap.GetPixel(x, y);
                    if (color.Alpha < 24)
                    {
                        continue;
                    }

                    redTotal += (long)color.Red * color.Alpha;
                    greenTotal += (long)color.Green * color.Alpha;
                    blueTotal += (long)color.Blue * color.Alpha;
                    alphaTotal += color.Alpha;
                }
            }
        }

        if (alphaTotal == 0)
        {
            return FallbackSeed;
        }

        return Color.FromRgb(
            (byte)(redTotal / alphaTotal),
            (byte)(greenTotal / alphaTotal),
            (byte)(blueTotal / alphaTotal));
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
