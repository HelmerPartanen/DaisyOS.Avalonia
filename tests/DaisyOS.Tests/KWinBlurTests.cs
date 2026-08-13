using Avalonia;
using DaisyOS.Shell.Services.Compositor;
using Xunit;

namespace DaisyOS.Tests;

public sealed class KWinBlurTests
{
    [Fact]
    public void RegionBoundsAreMappedToPhysicalSurfaceCoordinatesWithoutClippingEdges()
    {
        var rectangles = KWinBlurRectangle.FromLogicalBounds(
            new Point(10.25, 20.5),
            new Size(100.25, 40.1),
            scale: 1.5);

        var rectangle = Assert.Single(rectangles);
        Assert.Equal(15, rectangle.X);
        Assert.Equal(30, rectangle.Y);
        Assert.Equal(151, rectangle.Width);
        Assert.Equal(61, rectangle.Height);
    }

    [Fact]
    public void RoundedRegionOmitsTransparentCornerPixels()
    {
        var rectangles = KWinBlurRectangle.FromLogicalBounds(default, new Size(100, 52), 1, cornerRadius: 12);

        Assert.Contains(rectangles, rectangle => rectangle.Y == 0 && rectangle.X > 0 && rectangle.Width < 100);
        Assert.Contains(rectangles, rectangle => rectangle.Y == 12 && rectangle.X == 0 && rectangle.Width == 100);
        Assert.DoesNotContain(rectangles, rectangle => rectangle.X == 0 && rectangle.Y == 0);
    }
}
