using System;
using Avalonia;
using Avalonia.X11;

namespace DaisyOS.Shell;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            // Configure Skia GPU resource cache for wallpaper performance
            // 256 MB allows multiple 4K wallpapers to remain in GPU memory
            // without excessive eviction and re-upload
            .With(new SkiaOptions
            {
                MaxGpuResourceSizeBytes = 256L * 1024 * 1024
            });

        var x11Options = new X11PlatformOptions
        {
            OverlayPopups = true
        };

        if (string.Equals(Environment.GetEnvironmentVariable("DAISYOS_SOFTWARE_RENDERING"), "1",
                StringComparison.Ordinal))
        {
            x11Options.RenderingMode = [X11RenderingMode.Software];
        }

        return builder.With(x11Options);
    }
}
