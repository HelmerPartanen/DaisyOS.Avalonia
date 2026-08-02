using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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

                var wallpaperPath = "avares://DaisyOS.Shell/Assets/Wallpapers/Dark.jpg";
                
                var darkBrush = DaisyOS.Shell.Rendering.MicaMaterialGenerator.GenerateMicaBrush(
                    wallpaperPath, 
                    DaisyOS.Shell.Rendering.MicaTheme.DarkBase,
                    logicalWidth: logicalWidth,
                    logicalHeight: logicalHeight,
                    renderScaling: scaling);

                var lightBrush = DaisyOS.Shell.Rendering.MicaMaterialGenerator.GenerateMicaBrush(
                    wallpaperPath, 
                    DaisyOS.Shell.Rendering.MicaTheme.LightBase,
                    logicalWidth: logicalWidth,
                    logicalHeight: logicalHeight,
                    renderScaling: scaling);

                if (this.Resources.MergedDictionaries.Count > 0 && this.Resources.MergedDictionaries[0] is ResourceDictionary osTheme)
                {
                    if (osTheme.ThemeDictionaries.TryGetValue(Avalonia.Styling.ThemeVariant.Dark, out var darkTheme) && darkTheme is ResourceDictionary darkDict)
                    {
                        darkDict["TaskbarBackgroundBrush"] = darkBrush;
                    }
                    if (osTheme.ThemeDictionaries.TryGetValue(Avalonia.Styling.ThemeVariant.Light, out var lightTheme) && lightTheme is ResourceDictionary lightDict)
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
}
