using Avalonia.Media;
using DaisyOS.Shell.Services.Wallpaper;
using Xunit;

namespace DaisyOS.Tests;

public sealed class WallpaperLabelContrastTests
{
    [Fact]
    public void DarkWallpaperUsesLightDesktopCaptions() =>
        Assert.Equal(WallpaperLabelContrast.LightLabel, WallpaperLabelContrast.ForWallpaper(Color.Parse("#121D12")));

    [Fact]
    public void LightWallpaperUsesDarkDesktopCaptions() =>
        Assert.Equal(WallpaperLabelContrast.DarkLabel, WallpaperLabelContrast.ForWallpaper(Color.Parse("#F1EDE5")));
}
