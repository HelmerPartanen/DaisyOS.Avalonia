using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DaisyOS.Shell.Views;

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
            try
            {
                var wallpaperPath = "avares://DaisyOS.Shell/Assets/Wallpapers/Windows.jpg";
                this.Resources["MicaDarkBrush"] = DaisyOS.Shell.Rendering.MicaMaterialGenerator.GenerateMicaBrush(wallpaperPath, DaisyOS.Shell.Rendering.MicaTheme.DarkBase);
                this.Resources["MicaLightBrush"] = DaisyOS.Shell.Rendering.MicaMaterialGenerator.GenerateMicaBrush(wallpaperPath, DaisyOS.Shell.Rendering.MicaTheme.LightBase);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to generate Mica brush: {ex.Message}");
            }

            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
