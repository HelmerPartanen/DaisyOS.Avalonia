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

        var bucketScore = new double[36];
        var bucketRed = new double[36];
        var bucketGreen = new double[36];
        var bucketBlue = new double[36];
        var bucketWeight = new double[36];

        void ProcessPixel(SKColor color)
        {
            if (color.Alpha < 24) return;
            
            color.ToHsv(out float h, out float s, out float v);
            
            double sat = s / 100.0;
            double val = v / 100.0;
            
            // Score strongly prioritizes highly saturated and bright pixels.
            double score = sat * val;
            score = Math.Pow(score, 3); // Exponentially boost the most vibrant colors
            
            // Ignore completely dull or dark pixels to avoid muddying the bucket
            if (score < 0.01) return;

            int bucket = (int)(h / 10.0);
            if (bucket >= 36) bucket = 35;
            if (bucket < 0) bucket = 0;

            bucketScore[bucket] += score;
            bucketRed[bucket] += color.Red * score;
            bucketGreen[bucket] += color.Green * score;
            bucketBlue[bucket] += color.Blue * score;
            bucketWeight[bucket] += score;
        }

        var pixels = bitmap.Pixels;
        if (pixels is not null && pixels.Length == width * height)
        {
            for (int y = 0; y < height; y += stepY)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int rowOffset = y * width;
                for (int x = 0; x < width; x += stepX)
                {
                    ProcessPixel(pixels[rowOffset + x]);
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
                    ProcessPixel(bitmap.GetPixel(x, y));
                }
            }
        }

        int bestBucket = -1;
        double maxScore = -1;
        for (int i = 0; i < 36; i++)
        {
            if (bucketScore[i] > maxScore && bucketWeight[i] > 0)
            {
                maxScore = bucketScore[i];
                bestBucket = i;
            }
        }

        if (bestBucket == -1)
        {
            return FallbackSeed;
        }

        double totalWeight = bucketWeight[bestBucket];
        return Color.FromRgb(
            (byte)Math.Clamp(bucketRed[bestBucket] / totalWeight, 0, 255),
            (byte)Math.Clamp(bucketGreen[bestBucket] / totalWeight, 0, 255),
            (byte)Math.Clamp(bucketBlue[bestBucket] / totalWeight, 0, 255));
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
