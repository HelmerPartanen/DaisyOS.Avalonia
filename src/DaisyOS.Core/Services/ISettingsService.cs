using DaisyOS.Core.Models;

namespace DaisyOS.Core.Services;

public interface ISettingsService
{
    event EventHandler? SettingsChanged;

    ThemeMode ThemeMode { get; set; }

    AccentColor AccentColor { get; set; }

    bool HighContrast { get; set; }

    bool ReduceMotion { get; set; }

    double Volume { get; set; }

    DockPosition DockPosition { get; set; }

    IReadOnlyList<SystemBarItem> VisibleSystemBarItems { get; set; }

    MediaWidgetSize MediaWidgetSize { get; set; }

    bool UseAdaptiveMediaTint { get; set; }

    bool ShowDateInSystemBar { get; set; }

    bool NotificationToastsEnabled { get; set; }
    bool NotificationSoundsEnabled { get; set; }
    bool SilenceNotificationsWhileGaming { get; set; }

    bool DoNotDisturb { get; set; }

    bool GamingMode { get; set; }

    bool SearchFileContents { get; set; }

    bool AutomaticUpdateChecks { get; set; }

    IReadOnlyList<UpdateCheckRecord> UpdateCheckHistory { get; set; }

    OnboardingPreferences OnboardingPreferences { get; set; }

    IReadOnlyList<AppEntry> PinnedDockApps { get; set; }

    IReadOnlyList<string> DockItemOrder { get; set; }

    IReadOnlyList<AppEntry> RecentApps { get; set; }
    IReadOnlyList<string> FavoriteAppIds { get; set; }

    IReadOnlyList<FileManagerLocation>? PinnedFileManagerLocations { get; set; }

    string WallpaperUri { get; set; }

    string ProfilePictureUri { get; set; }
}
