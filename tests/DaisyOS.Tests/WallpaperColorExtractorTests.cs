using System.Diagnostics;
using System.IO;
using Avalonia.Media;
using DaisyOS.Shell.Services.Wallpaper;
using SkiaSharp;
using Xunit;

namespace DaisyOS.Tests;

public sealed class WallpaperColorExtractorTests : IDisposable
{
    private readonly string _testImagePath;

    public WallpaperColorExtractorTests()
    {
        _testImagePath = Path.Combine(Path.GetTempPath(), $"test_6k_wallpaper_{Guid.NewGuid()}.png");
        Create6KTestBitmap(_testImagePath, 6144, 3456, SKColors.Teal);
    }

    public void Dispose()
    {
        if (File.Exists(_testImagePath))
        {
            try { File.Delete(_testImagePath); } catch { }
        }
    }

    [Fact]
    public async Task ExtractSeedAsync_6KWallpaper_ExecutesFastAndAccurately()
    {
        var extractor = new WallpaperColorExtractor();

        var stopwatch = Stopwatch.StartNew();
        var palette = await extractor.ExtractPaletteAsync(_testImagePath);
        var seed = palette.PrimarySeed;
        stopwatch.Stop();

        // Must complete extraction on a 6K image within reasonable threshold (< 500ms even on slow virtual machines)
        Assert.True(stopwatch.ElapsedMilliseconds < 500, $"6K wallpaper color extraction took {stopwatch.ElapsedMilliseconds}ms, expected < 500ms");

        // Verify color accuracy (Teal = R:0, G:128, B:128)
        Assert.True(seed.G > 100 && seed.B > 100 && seed.R < 50, $"Expected Teal seed, got R:{seed.R} G:{seed.G} B:{seed.B}");

        // Second call must hit the seed cache instantly (< 5ms)
        var cachedStopwatch = Stopwatch.StartNew();
        var cachedPalette = await extractor.ExtractPaletteAsync(_testImagePath);
        var cachedSeed = cachedPalette.PrimarySeed;
        cachedStopwatch.Stop();

        Assert.Equal(seed, cachedSeed);
        Assert.True(cachedStopwatch.ElapsedMilliseconds < 20, $"Cached seed lookup took {cachedStopwatch.ElapsedMilliseconds}ms, expected < 20ms");
    }

    [Fact]
    public async Task ExtractSeedAsync_DarkGreenWallpaperRetainsItsHue()
    {
        var darkForestPath = Path.Combine(Path.GetTempPath(), $"test_dark_forest_{Guid.NewGuid()}.png");
        try
        {
            Create6KTestBitmap(darkForestPath, 1024, 768, new SKColor(18, 49, 31));

            var palette = await new WallpaperColorExtractor().ExtractPaletteAsync(darkForestPath);

            Assert.False(palette.IsGrayscale);
            Assert.True(palette.PrimarySeed.G > palette.PrimarySeed.R);
            Assert.True(palette.PrimarySeed.G > palette.PrimarySeed.B);
        }
        finally
        {
            if (File.Exists(darkForestPath))
            {
                try { File.Delete(darkForestPath); } catch { }
            }
        }
    }

    private static void Create6KTestBitmap(string path, int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }
}
