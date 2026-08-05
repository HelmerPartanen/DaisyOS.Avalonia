using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;

namespace DaisyOS.Shell.Apps.System.Settings
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private async void OnLightThemeClicked(object? sender, RoutedEventArgs e)
        {
            if (Application.Current is App app)
            {
                await app.SetShellThemeAsync(ThemeVariant.Light);
            }
        }

        private async void OnDarkThemeClicked(object? sender, RoutedEventArgs e)
        {
            if (Application.Current is App app)
            {
                await app.SetShellThemeAsync(ThemeVariant.Dark);
            }
        }

        private async void OnWallpaperClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string wallpaperUri } && Application.Current is App app)
            {
                await app.SetWallpaperAsync(wallpaperUri);
            }
        }
    }
}
