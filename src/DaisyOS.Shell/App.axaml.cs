using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Skia;
using Avalonia.Media;
using Avalonia.Styling;
using DaisyOS.Core.Models;
using DaisyOS.Shell.Services.Appearance;
using DaisyOS.Shell.Services.Wallpaper;
using DaisyOS.Shell.Services;
using DaisyOS.Shell.Services.Theming;
using DaisyOS.Shell.Apps.System.Settings;
using DaisyOS.Shell.Views;
using System;

using DaisyOS.Shell.Apps.Notes;
using DaisyOS.Shell.Apps.Calculator;
using DaisyOS.Shell.Apps.Files;
using DaisyOS.Shell.Apps.Calendar;
using DaisyOS.Shell.Services.Windows;
using DaisyOS.Shell.Services.Notifications;

namespace DaisyOS.Shell;

public partial class App : Application
{
    private IWallpaperService? _wallpaperService;
    private readonly AppearanceSettingsStore _appearanceSettingsStore = new();
    private readonly AppearanceSettings _appearanceSettings;
    private readonly AutomaticThemeScheduler _automaticThemeScheduler;
    private readonly DynamicThemeService _dynamicThemeService = new(
        new WallpaperColorExtractor(),
        new MaterialDynamicSchemeGenerator());
    private SettingsWindow? _settingsWindow;
    private NotesWindow? _notesWindow;
    private CalendarWindow? _calendarWindow;
    private CalculatorWindow? _calculatorWindow;
    private FilesWindow? _filesWindow;
    private ShellView? _shellView;
    private readonly NativeAppWindowTracker _nativeWindowTracker = new();

    public ShellSessionState SessionState { get; } = new();
    public ShellFeedbackService Feedback { get; } = new();
    public NotificationService Notifications { get; } = new();
    public NativeAppWindowTracker WindowTracker => _nativeWindowTracker;
    public string CurrentWallpaperUri => _wallpaperService?.CurrentWallpaperUri ?? ShellSettings.DefaultWallpaperUri;
    public ThemeMode CurrentThemeMode => _appearanceSettings.ThemeMode;
    public AccentColor CurrentAccentColor => _appearanceSettings.AccentColor;
    public bool HighContrastEnabled => _appearanceSettings.HighContrast;
    public bool ReduceMotionEnabled => _appearanceSettings.ReduceMotion;

    public event EventHandler<string>? WallpaperChanged;
    public event EventHandler<bool>? LauncherVisibilityChanged;
    public event EventHandler? AppearanceChanged;

    public App()
    {
        _appearanceSettings = _appearanceSettingsStore.Load();
        _automaticThemeScheduler = new AutomaticThemeScheduler(theme => _ = ApplyThemeVariantAsync(theme));
        _nativeWindowTracker.StateChanged += (_, args) =>
            _shellView?.Taskbar.SetAppWindowState(args.AppId, args.State);
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (_appearanceSettings.ThemeMode == ThemeMode.Automatic)
        {
            _automaticThemeScheduler.Start();
        }
        else
        {
            RequestedThemeVariant = ToThemeVariant(_appearanceSettings.ThemeMode);
        }
        ApplyAccessibilityPreferences();
        ApplyAccentPreference();

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
                var wallpaperPath = GetSafeWallpaperUri(_appearanceSettings.WallpaperUri);
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
    public Task SetShellThemeAsync(ThemeVariant theme) =>
        SetThemeModeAsync(theme == ThemeVariant.Light ? ThemeMode.Light : ThemeMode.Dark);

    public async Task SetThemeModeAsync(ThemeMode mode)
    {
        _appearanceSettings.ThemeMode = mode;
        if (mode == ThemeMode.Automatic)
        {
            _automaticThemeScheduler.Start();
        }
        else
        {
            _automaticThemeScheduler.Stop();
            await ApplyThemeVariantAsync(ToThemeVariant(mode));
        }

        await PersistAppearanceAsync();
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetAutomaticThemeScheduleDescription() =>
        _automaticThemeScheduler.GetScheduleDescription(DateTimeOffset.Now);

    public async Task SetAccentColorAsync(AccentColor accentColor)
    {
        _appearanceSettings.AccentColor = accentColor;
        ApplyAccentPreference();
        await PersistAppearanceAsync();
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetHighContrastAsync(bool enabled)
    {
        _appearanceSettings.HighContrast = enabled;
        ApplyAccessibilityPreferences();
        await PersistAppearanceAsync();
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetReduceMotionAsync(bool enabled)
    {
        _appearanceSettings.ReduceMotion = enabled;
        await PersistAppearanceAsync();
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task ApplyThemeVariantAsync(ThemeVariant theme)
    {
        RequestedThemeVariant = theme;
        ApplyAccessibilityPreferences();
        ApplyAccentPreference();
        if (_wallpaperService is not null)
        {
            await RefreshWallpaperAccentAsync(_wallpaperService.CurrentWallpaperUri, theme);
        }

        AppearanceChanged?.Invoke(this, EventArgs.Empty);
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

        if (_shellView?.SystemBar.IsCalendarPanelVisible == true)
        {
            _shellView.SystemBar.HideCalendarPanel();
            return true;
        }

        if (_shellView?.SystemBar.IsNotificationPanelVisible == true)
        {
            _shellView.SystemBar.HideNotificationPanel();
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
            case "calendar":
                ShowCalendar();
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

    /// <summary>Opens one instance of the native DaisyOS Calendar app.</summary>
    public void ShowCalendar()
    {
        HideLauncher();
        if (_nativeWindowTracker.RestoreAndActivate("calendar"))
        {
            return;
        }

        _calendarWindow = new CalendarWindow();
        _nativeWindowTracker.Register("calendar", _calendarWindow);
        _calendarWindow.Closed += (_, _) => _calendarWindow = null;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
        {
            _calendarWindow.Show(mainWindow);
        }
        else
        {
            _calendarWindow.Show();
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

    public async Task SetWallpaperAsync(string wallpaperUri)
    {
        var safeWallpaperUri = GetSafeWallpaperUri(wallpaperUri);
        if (!string.Equals(safeWallpaperUri, wallpaperUri, StringComparison.Ordinal))
        {
            throw new FileNotFoundException("The selected wallpaper could not be found.", wallpaperUri);
        }

        if (_wallpaperService is not null)
        {
            await _wallpaperService.SetWallpaperAsync(safeWallpaperUri);
        }

        _appearanceSettings.WallpaperUri = safeWallpaperUri;
        await PersistAppearanceAsync();
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task ResetWallpaperAsync() => SetWallpaperAsync(ShellSettings.DefaultWallpaperUri);

    private static ThemeVariant ToThemeVariant(ThemeMode mode) =>
        mode == ThemeMode.Light ? ThemeVariant.Light : ThemeVariant.Dark;

    private static string GetSafeWallpaperUri(string? wallpaperUri)
    {
        if (string.IsNullOrWhiteSpace(wallpaperUri)) return ShellSettings.DefaultWallpaperUri;
        if (wallpaperUri.StartsWith("avares://", StringComparison.OrdinalIgnoreCase)) return wallpaperUri;
        if (Path.IsPathRooted(wallpaperUri) && !File.Exists(wallpaperUri)) return ShellSettings.DefaultWallpaperUri;
        return wallpaperUri;
    }

    private async Task PersistAppearanceAsync()
    {
        try
        {
            await _appearanceSettingsStore.SaveAsync(_appearanceSettings);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save appearance preferences: {ex.Message}");
        }
    }

    private void ApplyAccentPreference()
    {
        var accent = _appearanceSettings.AccentColor switch
        {
            AccentColor.Blue => Color.Parse("#75A7FF"),
            AccentColor.Purple => Color.Parse("#BB9CFF"),
            AccentColor.Pink => Color.Parse("#FF9AC6"),
            AccentColor.Red => Color.Parse("#FF9B91"),
            AccentColor.Orange => Color.Parse("#FFB87B"),
            AccentColor.Green => Color.Parse("#7DD9A0"),
            AccentColor.Gray => Color.Parse("#C4C7CD"),
            _ => ActualThemeVariant == ThemeVariant.Light ? Color.Parse("#56565B") : Colors.White
        };

        if (Resources is null) return;
        Resources["AccentColor"] = accent;
        Resources["AccentBrush"] = new SolidColorBrush(accent);
        Resources["AccentHoverBrush"] = new SolidColorBrush(Blend(accent, ActualThemeVariant == ThemeVariant.Light ? Colors.White : Colors.Black, 0.12));
        Resources["AccentPressedBrush"] = new SolidColorBrush(Blend(accent, ActualThemeVariant == ThemeVariant.Light ? Colors.White : Colors.Black, 0.22));
        Resources["OnAccentBrush"] = new SolidColorBrush(IsLight(accent) ? Colors.Black : Colors.White);
    }

    private void ApplyAccessibilityPreferences()
    {
        if (Resources is null) return;

        var keys = new[] { "PrimarySurfaceBrush", "SecondarySurfaceBrush", "TertiarySurfaceBrush", "DividerBrush", "TextPrimaryBrush", "TextSecondaryBrush", "TextTertiaryBrush", "TextMutedBrush" };
        foreach (var key in keys) Resources.Remove(key);
        if (!_appearanceSettings.HighContrast) return;

        var light = ActualThemeVariant == ThemeVariant.Light;
        var foreground = light ? Colors.Black : Colors.White;
        var background = light ? Colors.White : Colors.Black;
        Resources["PrimarySurfaceBrush"] = new SolidColorBrush(background);
        Resources["SecondarySurfaceBrush"] = new SolidColorBrush(background);
        Resources["TertiarySurfaceBrush"] = new SolidColorBrush(light ? Color.Parse("#16000000") : Color.Parse("#1AFFFFFF"));
        Resources["DividerBrush"] = new SolidColorBrush(foreground);
        Resources["TextPrimaryBrush"] = new SolidColorBrush(foreground);
        Resources["TextSecondaryBrush"] = new SolidColorBrush(foreground);
        Resources["TextTertiaryBrush"] = new SolidColorBrush(foreground);
        Resources["TextMutedBrush"] = new SolidColorBrush(foreground);
    }

    private static Color Blend(Color from, Color to, double amount) => Color.FromArgb(
        (byte)Math.Round(from.A + ((to.A - from.A) * amount)),
        (byte)Math.Round(from.R + ((to.R - from.R) * amount)),
        (byte)Math.Round(from.G + ((to.G - from.G) * amount)),
        (byte)Math.Round(from.B + ((to.B - from.B) * amount)));

    private static bool IsLight(Color color) => ((color.R * 299) + (color.G * 587) + (color.B * 114)) / 1000 > 150;

    private static readonly string[] NativeAppIds = ["notes", "calculator", "settings", "files"];

}
