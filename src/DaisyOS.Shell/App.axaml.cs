using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using DaisyOS.Shell.Views;
using System;

namespace DaisyOS.Shell;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();

            try
            {
                var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow);
                double scaling = topLevel?.RenderScaling ?? 1.0;
                
                var screen = desktop.MainWindow.Screens.Primary;
                double logicalWidth = screen != null ? screen.WorkingArea.Width / scaling : 1920;
                double logicalHeight = screen != null ? screen.WorkingArea.Height / scaling : 1080;

                var wallpaperPath = "avares://DaisyOS.Shell/Assets/Wallpapers/Wallpaper.jpg";
                
                if (this.Resources.MergedDictionaries.Count > 0 && this.Resources.MergedDictionaries[0] is ResourceDictionary osTheme)
                {
                    ResourceDictionary? darkDict = null;
                    ResourceDictionary? lightDict = null;

                    if (osTheme.ThemeDictionaries.TryGetValue(Avalonia.Styling.ThemeVariant.Dark, out var darkTheme) && darkTheme is ResourceDictionary darkThemeDictionary)
                    {
                        darkDict = darkThemeDictionary;
                    }
                    if (osTheme.ThemeDictionaries.TryGetValue(Avalonia.Styling.ThemeVariant.Light, out var lightTheme) && lightTheme is ResourceDictionary lightThemeDictionary)
                    {
                        lightDict = lightThemeDictionary;
                    }

                    var darkBrush = DaisyOS.Shell.Rendering.MicaMaterialGenerator.GenerateMicaBrush(
                        wallpaperPath,
                        ReadMicaTheme(darkDict, DaisyOS.Shell.Rendering.MicaTheme.DarkBase),
                        logicalWidth: logicalWidth,
                        logicalHeight: logicalHeight,
                        renderScaling: scaling);

                    var lightBrush = DaisyOS.Shell.Rendering.MicaMaterialGenerator.GenerateMicaBrush(
                        wallpaperPath,
                        ReadMicaTheme(lightDict, DaisyOS.Shell.Rendering.MicaTheme.LightBase),
                        logicalWidth: logicalWidth,
                        logicalHeight: logicalHeight,
                        renderScaling: scaling);

                    if (darkDict is not null)
                    {
                        darkDict["TaskbarBackgroundBrush"] = darkBrush;
                    }

                    if (lightDict is not null)
                    {
                        lightDict["TaskbarBackgroundBrush"] = lightBrush;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to generate Mica brush: {ex.Message}");
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static DaisyOS.Shell.Rendering.MicaTheme ReadMicaTheme(
        ResourceDictionary? themeResources,
        DaisyOS.Shell.Rendering.MicaTheme fallback)
    {
        if (themeResources?.TryGetValue("MicaTintBrush", out var resource) == true &&
            resource is SolidColorBrush tintBrush)
        {
            var color = tintBrush.Color;
            fallback.TintColor = new SkiaSharp.SKColor(color.R, color.G, color.B, color.A);
        }

        return fallback;
    }
}
