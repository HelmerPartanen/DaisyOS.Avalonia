using System;
using DaisyOS.Shell.Services.Wallpaper.VideoWallpaper;
using Xunit;

namespace DaisyOS.Shell.Tests;

public class VideoWallpaperServiceTests
{
    [Fact]
    public void VideoWallpaperService_CanInstantiateAndDispose()
    {
        using var service = new VideoWallpaperService();
        Assert.True(service.IsLoaded);
    }

    [Fact]
    public void VideoWallpaperService_LoadNonExistentFile_ReturnsFalse()
    {
        using var service = new VideoWallpaperService();
        bool result = service.Load("/nonexistent/video.mp4");
        Assert.False(result);
    }

    [Fact]
    public void VideoWallpaperService_LoadGreenField_Succeeds()
    {
        const string videoPath = "src/DaisyOS.Shell/Assets/Wallpapers/Cat.mp4";
        if (!global::System.IO.File.Exists(videoPath))
            return;

        using var service = new VideoWallpaperService();
        bool loaded = service.Load(videoPath);
        Assert.True(loaded);

        service.Play();
        bool gotStats = service.GetStats(out var stats);
        Assert.True(gotStats);
        Assert.Equal(1920, stats.SrcWidth);
        Assert.Equal(1080, stats.SrcHeight);
        Assert.False(string.IsNullOrEmpty(stats.CodecString));

        service.Pause();
        Assert.True(service.IsPaused);

        service.Play();
        Assert.False(service.IsPaused);

        service.Stop();
    }

    [Fact]
    public void VideoWallpaperService_Pause_SetsIsPausedTrue()
    {
        using var service = new VideoWallpaperService();
        Assert.False(service.IsPaused);
        service.Pause();
        Assert.True(service.IsPaused);
        service.Play();
        Assert.False(service.IsPaused);
    }
}
