using System;

namespace DaisyOS.Shell.Services.Wallpaper.VideoWallpaper;

/// <summary>
/// Managed wrapper around libdaisy_live_wallpaper.so handle.
/// Provides high-performance 4K live video wallpaper decoding and DMA-BUF frame acquisition.
/// </summary>
public sealed class VideoWallpaperService : IDisposable
{
    private nint _handle;
    private bool _disposed;

    public bool IsLoaded => _handle != nint.Zero;

    public VideoWallpaperService()
    {
        _handle = DaisyNativeWallpaper.Create();
    }

    public bool Load(string videoPath)
    {
        if (_handle == nint.Zero || string.IsNullOrWhiteSpace(videoPath))
            return false;

        int res = DaisyNativeWallpaper.Load(_handle, videoPath);
        return res == 0;
    }

    public void Play()
    {
        if (_handle != nint.Zero)
            DaisyNativeWallpaper.Play(_handle);
    }

    public void Pause()
    {
        if (_handle != nint.Zero)
            DaisyNativeWallpaper.Pause(_handle);
    }

    public void Stop()
    {
        if (_handle != nint.Zero)
            DaisyNativeWallpaper.Stop(_handle);
    }

    public void SetFpsCap(int fps)
    {
        if (_handle != nint.Zero)
            DaisyNativeWallpaper.SetFpsCap(_handle, fps);
    }

    public bool TryGetFrame(out DaisyNativeWallpaper.NativeVideoFrame frame)
    {
        frame = default;
        if (_handle == nint.Zero)
            return false;

        return DaisyNativeWallpaper.TryGetFrame(_handle, ref frame) != 0;
    }

    public bool GetStats(out DaisyNativeWallpaper.NativeWallpaperStats stats)
    {
        stats = default;
        if (_handle == nint.Zero)
            return false;

        DaisyNativeWallpaper.GetStats(_handle, ref stats);
        return true;
    }

    public void PrintStats()
    {
        if (_handle != nint.Zero)
            DaisyNativeWallpaper.PrintStats(_handle);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_handle != nint.Zero)
        {
            DaisyNativeWallpaper.Stop(_handle);
            DaisyNativeWallpaper.Destroy(_handle);
            _handle = nint.Zero;
        }
    }
}
