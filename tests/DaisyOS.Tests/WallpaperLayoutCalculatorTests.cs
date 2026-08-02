using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DaisyOS.Shell.Rendering;
using Xunit;

namespace DaisyOS.Tests;

public sealed class WallpaperLayoutCalculatorTests
{
    [Theory]
    [InlineData(1920, 1080, 1920, 1080, 1, 1, 0, 0)]
    [InlineData(3840, 2160, 1920, 1080, .5, .5, 0, 0)]
    [InlineData(1920, 1080, 2560, 1440, 1.3333333333, 1.3333333333, 0, 0)]
    [InlineData(1080, 1920, 1920, 1080, 1.7777777778, 1.7777777778, 0, -1166.6666667)]
    public void UniformToFill_is_exact(int sw, int sh, int tw, int th, double sx, double sy, double ox, double oy)
    {
        var actual = WallpaperLayoutCalculator.Calculate(new PixelSize(sw, sh), new Size(tw, th), Stretch.UniformToFill);
        Close(sx, actual.ScaleX); Close(sy, actual.ScaleY); Close(ox, actual.OffsetX); Close(oy, actual.OffsetY);
    }

    [Fact]
    public void Fill_has_independent_scales()
    {
        var r = WallpaperLayoutCalculator.Calculate(new PixelSize(100, 200), new Size(300, 100), Stretch.Fill);
        Close(3, r.ScaleX); Close(.5, r.ScaleY);
    }
    [Fact]
    public void None_obeys_right_bottom_alignment()
    {
        var r = WallpaperLayoutCalculator.Calculate(new PixelSize(100, 50), new Size(300, 200), Stretch.None,
            HorizontalAlignment.Right, VerticalAlignment.Bottom);
        Close(200, r.OffsetX); Close(150, r.OffsetY);
    }
    [Fact]
    public void Uniform_letterboxes_and_centers()
    {
        var r = WallpaperLayoutCalculator.Calculate(new PixelSize(1920, 1080), new Size(1080, 1920), Stretch.Uniform);
        Close(.5625, r.ScaleX); Close(656.25, r.OffsetY);
    }
    [Fact]
    public void Mapping_inverts_layout()
    {
        var l = WallpaperLayoutCalculator.Calculate(new PixelSize(4000, 2000), new Size(1000, 1000), Stretch.UniformToFill);
        var r = WallpaperLayoutCalculator.MapDestinationToSource(new Rect(100, 200, 300, 400), l);
        Close(1200, r.X); Close(400, r.Y); Close(600, r.Width); Close(800, r.Height);
    }
    [Fact]
    public void Padded_crop_clamps_and_reports_loss()
    {
        var l = WallpaperLayoutCalculator.Calculate(new PixelSize(100, 100), new Size(100, 100), Stretch.Fill);
        var r = WallpaperLayoutCalculator.CalculatePaddedCrop(new Rect(0, 0, 20, 20), l, new PixelSize(100, 100), 10);
        Assert.Equal(new Rect(0, 0, 30, 30), r.ClampedSourceRect); Close(10, r.LostLeft); Close(10, r.LostTop);
    }
    [Theory]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void Physical_scaling_preserves_mapping(double scaling)
    {
        var size = new Size(1920 * scaling, 1080 * scaling);
        var l = WallpaperLayoutCalculator.Calculate(new PixelSize(1920, 1080), size, Stretch.UniformToFill);
        var r = WallpaperLayoutCalculator.MapDestinationToSource(new Rect(100 * scaling, 50 * scaling, 200 * scaling, 100 * scaling), l);
        Close(100, r.X); Close(50, r.Y); Close(200, r.Width); Close(100, r.Height);
    }
    private static void Close(double expected, double actual) => Assert.InRange(actual, expected - 1e-5, expected + 1e-5);
}
