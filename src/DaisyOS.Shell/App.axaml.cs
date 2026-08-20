using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Skia;
using Avalonia.Styling;
using DaisyOS.Core.Models;
using DaisyOS.Shell.Services.Wallpaper;
using DaisyOS.Shell.Services;
using DaisyOS.Shell.Services.Theming;
using DaisyOS.Shell.Apps.System.Settings;
using DaisyOS.Shell.Views;
using System;

using DaisyOS.Shell.Apps.Notes;
using DaisyOS.Shell.Apps.Calculator;
using DaisyOS.Shell.Apps.Files;
using DaisyOS.Shell.Services.Windows;
using DaisyOS.Shell.Services.Notifications;

namespace DaisyOS.Shell;

public partial class App : Application
{
    private IWallpaperService? _wallpaperService;
    private readonly DynamicThemeService _dynamicThemeService = new(
        new WallpaperColorExtractor(),
        new MaterialDynamicSchemeGenerator());
    private SettingsWindow? _settingsWindow;
    private NotesWindow? _notesWindow;
    private CalculatorWindow? _calculatorWindow;
    private FilesWindow? _filesWindow;
    private ShellView? _shellView;
    private readonly NativeAppWindowTracker _nativeWindowTracker = new();

    public ShellSessionState SessionState { get; } = new();
    public ShellFeedbackService Feedback { get; } = new();
    public NotificationService Notifications { get; } = new();
    public NativeAppWindowTracker WindowTracker => _nativeWindowTracker;
    public string CurrentWallpaperUri => _wallpaperService?.CurrentWallpaperUri ?? ShellSettings.DefaultWallpaperUri;

    public event EventHandler<string>? WallpaperChanged;
    public event EventHandler<bool>? LauncherVisibilityChanged;

    public App()
    {
        _nativeWindowTracker.StateChanged += (_, args) =>
            _shellView?.Taskbar.SetAppWindowState(args.AppId, args.State);
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
            mainWindow.Opened += (_, _) =>
            {
                ConfigureShellOverlays(mainWindow.ShellContent);
            };

            try
            {
                var wallpaperPath = ShellSettings.DefaultWallpaperUri;
                _wallpaperService = new WallpaperService(wallpaperPath);
                _wallpaperService.WallpaperChanged += async (_, changedWallpaperUri) =>
                {
                    WallpaperChanged?.Invoke(this, changedWallpaperUri);
                    await RefreshWallpaperAccentAsync(changedWallpaperUri, ActualThemeVariant);
                };
                _ = RefreshWallpaperAccentAsync(wallpaperPath, ActualThemeVariant);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize wallpaper state: {ex.Message}");
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Applies the requested appearance and regenerates its wallpaper-derived accent roles.</summary>
    public async Task SetShellThemeAsync(ThemeVariant theme)
    {
        RequestedThemeVariant = theme;
        if (_wallpaperService is not null)
        {
            await RefreshWallpaperAccentAsync(_wallpaperService.CurrentWallpaperUri, theme);
        }
    }

    private async Task RefreshWallpaperAccentAsync(string wallpaperUri, ThemeVariant theme)
    {
        try
        {
            await _dynamicThemeService.RefreshFromWallpaperAsync(wallpaperUri, theme);
        }
        catch (OperationCanceledException)
        {
            // A newer wallpaper or theme refresh superseded this one.
        }
        catch (Exception ex)
        {
            // Accent extraction is cosmetic; retain the last known safe palette on failure.
            Console.WriteLine($"Failed to refresh wallpaper accent: {ex.Message}");
        }
    }

    /// <summary>Shows or hides the launcher within the shell window.</summary>
    public void ToggleLauncher()
    {
        if (_shellView?.IsLauncherVisible == true)
        {
            HideLauncher();
        }
        else
        {
            ShowLauncher();
        }
    }

    public void HideLauncher()
    {
        if (_shellView?.IsLauncherVisible != true)
        {
            return;
        }

        _shellView.HideLauncher();
        LauncherVisibilityChanged?.Invoke(this, false);
    }

    private void ShowLauncher()
    {
        if (_shellView is null)
        {
            return;
        }

        HideQuickSettings();
        _shellView.ShowLauncher();
        LauncherVisibilityChanged?.Invoke(this, true);
    }

    private void ConfigureShellOverlays(ShellView shell)
    {
        _shellView = shell;
        shell.Taskbar.ApplyOrder(SessionState.DockOrder);
        shell.Taskbar.OrderChanged += (_, order) => SessionState.SetDockOrder(order);
        shell.Taskbar.StartButtonClicked += (_, _) => ToggleLauncher();
        shell.Taskbar.AppIconClicked += (_, appId) => LaunchTaskbarApp(appId);
        foreach (var appId in NativeAppIds)
        {
            shell.Taskbar.SetAppWindowState(appId, _nativeWindowTracker.GetState(appId));
        }
        shell.SystemBar.QuickSettingsRequested += (_, _) => ToggleQuickSettings();
        shell.SystemBar.QuickSettingsDismissRequested += (_, _) => HideQuickSettings();
        shell.SystemBar.ConsoleModeRequested += (_, _) => shell.RequestConsoleMode();
        shell.Launcher.AppLaunchRequested += (_, _) => HideLauncher();
        LauncherVisibilityChanged += (_, isVisible) => shell.Taskbar.SetLauncherOpen(isVisible);
    }

    private void ToggleQuickSettings()
    {
        if (_shellView?.SystemBar.IsQuickSettingsVisible == true)
        {
            HideQuickSettings();
            return;
        }

        if (_shellView is null)
        {
            return;
        }

        HideLauncher();
        _shellView.SystemBar.ShowQuickSettingsPanel();
    }

    private void HideQuickSettings()
    {
        _shellView?.SystemBar.HideQuickSettingsPanel();
    }

    public bool DismissTransientShellSurfaces()
    {
        if (_shellView?.IsMetricsPanelVisible == true)
        {
            _shellView.HideMetricsPanel();
            return true;
        }

        if (_shellView?.SystemBar.IsQuickSettingsVisible == true)
        {
            HideQuickSettings();
            return true;
        }

        if (_shellView?.IsLauncherVisible == true)
        {
            HideLauncher();
            return true;
        }

        return false;
    }

    private void LaunchTaskbarApp(string appId)
    {
        HideLauncher();
        if (_nativeWindowTracker.ToggleFromTaskbar(appId))
        {
            return;
        }

        switch (appId)
        {
            case "settings":
                ShowSettings();
                break;
            case "notes":
                ShowNotes();
                break;
            case "calculator":
                ShowCalculator();
                break;
            case "files":
                ShowFiles();
                break;
        }
    }

    /// <summary>Opens one normal, compositor-managed instance of the DottOS Settings app.</summary>
    public void ShowSettings()
    {
        HideLauncher();
        if (_nativeWindowTracker.RestoreAndActivate("settings"))
        {
            return;
        }

        _settingsWindow = new SettingsWindow();
        _nativeWindowTracker.Register("settings", _settingsWindow);
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
        HideLauncher();
        if (_nativeWindowTracker.RestoreAndActivate("notes"))
        {
            return;
        }

        _notesWindow = new NotesWindow();
        _nativeWindowTracker.Register("notes", _notesWindow);
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
        HideLauncher();
        if (_nativeWindowTracker.RestoreAndActivate("calculator"))
        {
            return;
        }

        _calculatorWindow = new CalculatorWindow();
        _nativeWindowTracker.Register("calculator", _calculatorWindow);
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

    public void ShowFiles()
    {
        HideLauncher();
        if (_nativeWindowTracker.RestoreAndActivate("files"))
        {
            return;
        }

        _filesWindow = new FilesWindow();
        _nativeWindowTracker.Register("files", _filesWindow);
        _filesWindow.Closed += (_, _) => _filesWindow = null;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }) _filesWindow.Show(mainWindow);
        else _filesWindow.Show();
    }

    public Task SetWallpaperAsync(string wallpaperUri) =>
        _wallpaperService?.SetWallpaperAsync(wallpaperUri) ?? Task.CompletedTask;

    private static readonly string[] NativeAppIds = ["notes", "calculator", "settings", "files"];

}
