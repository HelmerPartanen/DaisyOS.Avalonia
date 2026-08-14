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

    public static readonly WallpaperPalette FallbackPalette = new(Color.Parse("#6750A4"), Color.Parse("#6750A4"), false);

    private static readonly ConcurrentDictionary<string, WallpaperPalette> s_assetSeedCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, (DateTime lastWrite, long fileSize, WallpaperPalette palette)> s_fileSeedCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<WallpaperPalette> ExtractPaletteAsync(string wallpaperUri, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(wallpaperUri))
        {
            return FallbackPalette;
        }

        if (TryGetCachedPalette(wallpaperUri, out var cachedPalette))
        {
            return cachedPalette;
        }

        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var stream = OpenWallpaper(wallpaperUri);
                var palette = ExtractPalette(stream, cancellationToken);
                CachePalette(wallpaperUri, palette);
                return palette;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return FallbackPalette;
        }
    }

    /// <summary>Applies the wallpaper sampler to arbitrary image data, including album artwork.</summary>
    public async Task<WallpaperPalette> ExtractPaletteAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(() => ExtractPalette(imageStream, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return FallbackPalette;
        }
    }

    private static bool TryGetCachedPalette(string wallpaperUri, out WallpaperPalette palette)
    {
        if (wallpaperUri.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
        {
            return s_assetSeedCache.TryGetValue(wallpaperUri, out palette);
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
                        palette = cached.palette;
                        return true;
                    }
                }
                catch
                {
                    // Fall back to re-extracting if file stats cannot be retrieved
                }
            }
        }

        palette = default;
        return false;
    }

    private static void CachePalette(string wallpaperUri, WallpaperPalette palette)
    {
        if (wallpaperUri.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
        {
            s_assetSeedCache[wallpaperUri] = palette;
            return;
        }

        try
        {
            if (File.Exists(wallpaperUri))
            {
                var fileInfo = new FileInfo(wallpaperUri);
                s_fileSeedCache[wallpaperUri] = (fileInfo.LastWriteTimeUtc, fileInfo.Length, palette);
            }
        }
        catch
        {
            // Ignore cache storage errors for non-accessible files
        }
    }

    private static WallpaperPalette ExtractPalette(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var bitmap = SKBitmap.Decode(stream);
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            return FallbackPalette;
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
        
        var dominantCounts = new int[32768];
        var dominantR = new long[32768];
        var dominantG = new long[32768];
        var dominantB = new long[32768];
        int maxDominantCount = -1;
        int bestDominantIndex = -1;

        int maxLuma = -1;
        byte brightestR = 0;
        byte brightestG = 0;
        byte brightestB = 0;

        void ProcessPixel(SKColor color)
        {
            if (color.Alpha < 24) return;
            
            // --- Dominant color logic ---
            int r5 = color.Red >> 3;
            int g5 = color.Green >> 3;
            int b5 = color.Blue >> 3;
            int index = (r5 << 10) | (g5 << 5) | b5;
            
            dominantCounts[index]++;
            dominantR[index] += color.Red;
            dominantG[index] += color.Green;
            dominantB[index] += color.Blue;
            
            if (dominantCounts[index] > maxDominantCount)
            {
                maxDominantCount = dominantCounts[index];
                bestDominantIndex = index;
            }
            // ----------------------------

            int luma = (color.Red * 299 + color.Green * 587 + color.Blue * 114) / 1000;
            if (luma > maxLuma)
            {
                maxLuma = luma;
                brightestR = color.Red;
                brightestG = color.Green;
                brightestB = color.Blue;
            }
            
            color.ToHsv(out float h, out float s, out float v);
            
            double sat = s / 100.0;
            double val = v / 100.0;
            
            // Preserve a wallpaper's hue even when it is intentionally dark. A forest at dusk
            // can have rich green chroma with a low value; brightness is raised only after its
            // hue wins, rather than discarding it and incorrectly treating the image as grayscale.
            double score = sat * (0.25 + (0.75 * val));
            score *= score;

            // Ignore genuinely neutral pixels, while retaining low-light coloured pixels.
            if (score < 0.0025) return;

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

        Color primarySeed = FallbackPalette.PrimarySeed;
        bool useGrayscaleFallback = true;

        if (bestBucket != -1)
        {
            double totalWeight = bucketWeight[bestBucket];
            byte r = (byte)Math.Clamp(bucketRed[bestBucket] / totalWeight, 0, 255);
            byte g = (byte)Math.Clamp(bucketGreen[bestBucket] / totalWeight, 0, 255);
            byte b = (byte)Math.Clamp(bucketBlue[bestBucket] / totalWeight, 0, 255);
            
            var skColor = new SKColor(r, g, b);
            skColor.ToHsv(out float h, out float s, out float v);
            
            // A hue that is present across the sampled image should remain an accent even when
            // the wallpaper is dark. True grayscale imagery stays below this chroma threshold.
            if (s >= 12f && maxScore >= 0.5)
            {
                useGrayscaleFallback = false;
                
                // Enforce a minimum brightness so active buttons don't become too dark
                if (v < 50f)
                {
                    v = 50f;
                    skColor = SKColor.FromHsv(h, s, v);
                    r = skColor.Red;
                    g = skColor.Green;
                    b = skColor.Blue;
                }
                
                primarySeed = Color.FromRgb(r, g, b);
            }
        }
        
        if (useGrayscaleFallback)
        {
            if (maxLuma != -1)
            {
                var skColor = new SKColor(brightestR, brightestG, brightestB);
                skColor.ToHsv(out float h, out float s, out float v);
                
                if (v < 50f)
                {
                    v = 50f;
                    skColor = SKColor.FromHsv(h, s, v);
                }
                
                primarySeed = Color.FromRgb(skColor.Red, skColor.Green, skColor.Blue);
            }
            else
            {
                primarySeed = FallbackPalette.PrimarySeed;
            }
        }
        
        Color surfaceTint = FallbackPalette.SurfaceTint;
        if (bestDominantIndex != -1 && maxDominantCount > 0)
        {
            byte r = (byte)(dominantR[bestDominantIndex] / maxDominantCount);
            byte g = (byte)(dominantG[bestDominantIndex] / maxDominantCount);
            byte b = (byte)(dominantB[bestDominantIndex] / maxDominantCount);
            surfaceTint = Color.FromRgb(r, g, b);
        }

        return new WallpaperPalette(primarySeed, surfaceTint, useGrayscaleFallback);
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
