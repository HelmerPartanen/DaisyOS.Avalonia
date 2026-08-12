using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Skia;
using Avalonia.Styling;
using DaisyOS.Core.Models;
using DaisyOS.Shell.Services.Theming;
using DaisyOS.Shell.Services.Wallpaper;
using DaisyOS.Shell.Apps.System.Settings;
using DaisyOS.Shell.Views;
using System;

using DaisyOS.Shell.Apps.Notes;
using DaisyOS.Shell.Apps.Calculator;

namespace DaisyOS.Shell;

public partial class App : Application
{
    private DynamicThemeService? _dynamicThemeService;
    private IWallpaperService? _wallpaperService;
    private SettingsWindow? _settingsWindow;
    private NotesWindow? _notesWindow;
    private CalculatorWindow? _calculatorWindow;

    public event EventHandler<string>? WallpaperChanged;

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
                var wallpaperPath = ShellSettings.DefaultWallpaperUri;
                _dynamicThemeService = new DynamicThemeService(
                    new WallpaperColorExtractor(),
                    new MaterialDynamicSchemeGenerator());
                _wallpaperService = new WallpaperService(wallpaperPath);
                _wallpaperService.WallpaperChanged += (_, changedWallpaperUri) =>
                {
                    WallpaperChanged?.Invoke(this, changedWallpaperUri);
                    _ = _dynamicThemeService.RefreshFromWallpaperAsync(changedWallpaperUri, ActualThemeVariant);
                };
                _ = _dynamicThemeService.RefreshFromWallpaperAsync(_wallpaperService.CurrentWallpaperUri, ActualThemeVariant);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize dynamic shell colors: {ex.Message}");
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Applies the requested appearance to the complete shell and refreshes its dynamic palette.</summary>
    public async Task SetShellThemeAsync(ThemeVariant theme)
    {
        RequestedThemeVariant = theme;

        if (_dynamicThemeService is not null)
        {
            var wallpaperUri = _wallpaperService?.CurrentWallpaperUri ?? ShellSettings.DefaultWallpaperUri;
            await _dynamicThemeService.RefreshFromWallpaperAsync(wallpaperUri, theme);
        }
    }

    /// <summary>Opens one normal, compositor-managed instance of the DottOS Settings app.</summary>
    public void ShowSettings()
    {
        if (_settingsWindow is { IsVisible: true } settingsWindow)
        {
            settingsWindow.WindowState = Avalonia.Controls.WindowState.Normal;
            settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow();
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;

        // The shell root is fullscreen. Make Settings an owned normal window so KWin/X11
        // presents it above the shell instead of allowing the fullscreen root to obscure it.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
        {
            _settingsWindow.Show(mainWindow);
        }
        else
        {
            _settingsWindow.Show();
        }
    }

    /// <summary>Opens one instance of the native DaisyOS Notes app.</summary>
    public void ShowNotes()
    {
        if (_notesWindow is { IsVisible: true } notesWindow)
        {
            notesWindow.WindowState = Avalonia.Controls.WindowState.Normal;
            notesWindow.Activate();
            return;
        }

        _notesWindow = new NotesWindow();
        _notesWindow.Closed += (_, _) => _notesWindow = null;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
        {
            _notesWindow.Show(mainWindow);
        }
        else
        {
            _notesWindow.Show();
        }
    }

    /// <summary>Opens one instance of the native DaisyOS Calculator app.</summary>
    public void ShowCalculator()
    {
        if (_calculatorWindow is { IsVisible: true } calculatorWindow)
        {
            calculatorWindow.WindowState = Avalonia.Controls.WindowState.Normal;
            calculatorWindow.Activate();
            return;
        }

        _calculatorWindow = new CalculatorWindow();
        _calculatorWindow.Closed += (_, _) => _calculatorWindow = null;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
        {
            _calculatorWindow.Show(mainWindow);
        }
        else
        {
            _calculatorWindow.Show();
        }
    }

    public Task SetWallpaperAsync(string wallpaperUri) =>
        _wallpaperService?.SetWallpaperAsync(wallpaperUri) ?? Task.CompletedTask;

}
