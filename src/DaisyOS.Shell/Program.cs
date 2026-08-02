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
            .LogToTrace();

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
