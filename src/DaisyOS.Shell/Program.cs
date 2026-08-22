using System;
using Avalonia;
using Avalonia.Wayland;
using Avalonia.X11;

namespace DaisyOS.Shell;

internal static class Program
{
    private static readonly bool IsShellSession =
        string.Equals(Environment.GetEnvironmentVariable("DAISYOS_SHELL_SESSION"), "1", StringComparison.Ordinal)
        || Environment.GetCommandLineArgs().Contains("--shell-session", StringComparer.Ordinal);

    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args.Where(arg => arg != "--shell-session").ToArray());
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .LogToTrace()
            // Configure Skia GPU resource cache for wallpaper performance
            // 256 MB allows multiple 4K wallpapers to remain in GPU memory
            // without excessive eviction and re-upload
            .With(new SkiaOptions
            {
                MaxGpuResourceSizeBytes = 256L * 1024 * 1024
            });

        if (IsShellSession)
        {
            // The display-manager session must be a native Wayland client. Do
            // not let platform auto-detection silently put the shell back on
            // XWayland when a Wayland-session prerequisite is missing.
            // KWin's dmabuf import path is not yet stable enough for the shell
            // on every GPU. The WSI/EGL path is slightly less aggressive but
            // keeps the session alive instead of aborting during first render.
            return builder
                .UseSkia()
                .UseHarfBuzz()
                .With(new WaylandPlatformOptions { UseDmabufSwapchain = false })
                .UseWayland();
        }

        builder.UsePlatformDetect();
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
