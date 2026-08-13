using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Skia;
using Avalonia.Styling;
using DaisyOS.Core.Models;
using DaisyOS.Shell.Services.Wallpaper;
using DaisyOS.Shell.Services;
using DaisyOS.Shell.Services.Compositor;
using DaisyOS.Shell.Apps.System.Settings;
using DaisyOS.Shell.Views;
using System;

using DaisyOS.Shell.Apps.Notes;
using DaisyOS.Shell.Apps.Calculator;
using DaisyOS.Shell.Apps.Files;

namespace DaisyOS.Shell;

public partial class App : Application
{
    private const int DefaultKWinBlurStrength = 3;
    private IWallpaperService? _wallpaperService;
    private SettingsWindow? _settingsWindow;
    private NotesWindow? _notesWindow;
    private CalculatorWindow? _calculatorWindow;
    private FilesWindow? _filesWindow;
    private LauncherWindow? _launcherWindow;
    private BottomChromeWindow? _bottomChromeWindow;
    private QuickSettingsWindow? _quickSettingsWindow;

    public ShellSessionState SessionState { get; } = new();
    public ShellFeedbackService Feedback { get; } = new();

    public event EventHandler<string>? WallpaperChanged;
    public event EventHandler<bool>? LauncherVisibilityChanged;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.MainWindow.Opened += async (_, _) =>
            {
                ShowBottomChrome(desktop.MainWindow);

                // This is a compositor-level setting shared by all KWin blur
                // regions. Do not delay first paint while KWin updates it.
                await KWinBlur.SetStrengthAsync(DefaultKWinBlurStrength);
            };

            try
            {
                var wallpaperPath = ShellSettings.DefaultWallpaperUri;
                _wallpaperService = new WallpaperService(wallpaperPath);
                _wallpaperService.WallpaperChanged += (_, changedWallpaperUri) =>
                {
                    WallpaperChanged?.Invoke(this, changedWallpaperUri);
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize wallpaper state: {ex.Message}");
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Applies the requested appearance without deriving surface materials from the wallpaper.</summary>
    public async Task SetShellThemeAsync(ThemeVariant theme)
    {
        RequestedThemeVariant = theme;
        await Task.CompletedTask;
    }

    /// <summary>Shows or hides the launcher as its own native KWin-blurred surface.</summary>
    public void ToggleLauncher()
    {
        if (_launcherWindow is { IsVisible: true })
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
        if (_launcherWindow is not { IsVisible: true })
        {
            return;
        }

        _launcherWindow.Hide();
        LauncherVisibilityChanged?.Invoke(this, false);
    }

    private void ShowLauncher()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
        {
            return;
        }

        if (_launcherWindow is null)
        {
            _launcherWindow = new LauncherWindow();
            _launcherWindow.CloseRequested += (_, _) => HideLauncher();
            _launcherWindow.Deactivated += (_, _) => HideLauncher();
            _launcherWindow.Closed += (_, _) =>
            {
                _launcherWindow = null;
                LauncherVisibilityChanged?.Invoke(this, false);
            };
        }

        _launcherWindow.PrepareForPresentation();
        _launcherWindow.PositionAboveBottomChrome(mainWindow);
        _launcherWindow.Show(mainWindow);
        _launcherWindow.Activate();
        LauncherVisibilityChanged?.Invoke(this, true);
        if (_launcherWindow.Opacity > 0)
        {
            _launcherWindow.LauncherContent.FocusSearch();
        }
    }

    private void ShowBottomChrome(Window mainWindow)
    {
        if (_bottomChromeWindow is { IsVisible: true })
        {
            return;
        }

        if (_bottomChromeWindow is null)
        {
            _bottomChromeWindow = new BottomChromeWindow();
            _bottomChromeWindow.Taskbar.ApplyOrder(SessionState.DockOrder);
            _bottomChromeWindow.Taskbar.OrderChanged += (_, order) => SessionState.SetDockOrder(order);
            _bottomChromeWindow.Taskbar.StartButtonClicked += (_, _) => ToggleLauncher();
            _bottomChromeWindow.Taskbar.AppIconClicked += (_, appId) => LaunchTaskbarApp(appId);
            _bottomChromeWindow.SystemBar.QuickSettingsRequested += (_, _) => ToggleQuickSettings();
            LauncherVisibilityChanged += (_, isVisible) => _bottomChromeWindow?.Taskbar.SetLauncherOpen(isVisible);
        }

        _bottomChromeWindow.PositionAtBottomOf(mainWindow);
        _bottomChromeWindow.Show(mainWindow);
    }

    private void ToggleQuickSettings()
    {
        if (_quickSettingsWindow is { IsVisible: true })
        {
            HideQuickSettings();
            return;
        }

        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
        {
            return;
        }

        HideLauncher();
        if (_quickSettingsWindow is null)
        {
            _quickSettingsWindow = new QuickSettingsWindow();
            _quickSettingsWindow.QuickSettings.QuickSettingsDismissRequested += (_, _) => HideQuickSettings();
            _quickSettingsWindow.Deactivated += (_, _) => HideQuickSettings();
            _quickSettingsWindow.Closed += (_, _) => _quickSettingsWindow = null;
        }

        _quickSettingsWindow.PositionAboveSystemBar(mainWindow);
        _quickSettingsWindow.Show(mainWindow);
        _quickSettingsWindow.Activate();
    }

    private void HideQuickSettings()
    {
        if (_quickSettingsWindow is { IsVisible: true })
        {
            _quickSettingsWindow.Hide();
        }
    }

    private void LaunchTaskbarApp(string appId)
    {
        HideLauncher();
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
        HideLauncher();
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
        HideLauncher();
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

    public void ShowFiles()
    {
        HideLauncher();
        if (_filesWindow is { IsVisible: true } filesWindow)
        {
            filesWindow.WindowState = Avalonia.Controls.WindowState.Normal;
            filesWindow.Activate();
            return;
        }

        _filesWindow = new FilesWindow();
        _filesWindow.Closed += (_, _) => _filesWindow = null;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }) _filesWindow.Show(mainWindow);
        else _filesWindow.Show();
    }

    public Task SetWallpaperAsync(string wallpaperUri) =>
        _wallpaperService?.SetWallpaperAsync(wallpaperUri) ?? Task.CompletedTask;

}
