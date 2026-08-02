// Semantic UI icon definitions using Material Symbols Rounded ligature names.
// Each value is an icon name string resolved by the Material Symbols Rounded font
// via OpenType liga features. Set FontFamily to "Material Symbols Rounded".
#nullable enable

namespace DaisyOS.Shell;

/// <summary>
/// Semantic UI icon definitions using Material Symbols Rounded ligature names.
/// </summary>
public static class UIIcons
{
    private const string FilledSuffix = "-fill";

    /// <summary>Returns the filled form of a Material Symbol icon name.</summary>
    public static string Filled(string icon) => IsFilled(icon) ? icon : $"{icon}{FilledSuffix}";

    /// <summary>Returns the regular form of a Material Symbol icon name.</summary>
    public static string Regular(string icon) => IsFilled(icon)
        ? icon[..^FilledSuffix.Length]
        : icon;

    /// <summary>Splits an icon token into its Material glyph name and fill variant.</summary>
    internal static (string Glyph, bool IsFilled) Parse(string? icon)
    {
        var token = icon ?? string.Empty;
        return IsFilled(token)
            ? (token[..^FilledSuffix.Length], true)
            : (token, false);
    }

    private static bool IsFilled(string icon) => icon.EndsWith(FilledSuffix, StringComparison.Ordinal);

    // General UI
    public const string Search = "search";
    public const string Add = "add";
    public const string Close = "close";
    public const string Check = "check";
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Error = "error";
    public const string Settings = "settings";
    public const string MoreHorizontal = "more_horiz";
    public const string MoreVertical = "more_vert";
    public const string Copy = "content_copy";
    public const string Edit = "edit";
    public const string Delete = "delete";
    public const string Share = "share";
    public const string Filter = "filter_list";

    // Navigation & Arrows
    public const string Back = "arrow_back";
    public const string Forward = "arrow_forward";
    public const string Up = "arrow_upward";
    public const string Down = "arrow_downward";
    public const string ChevronLeft = "chevron_left";
    public const string ChevronRight = "chevron_right";
    public const string ChevronUp = "expand_less";
    public const string ChevronDown = "expand_more";
    public const string Refresh = "refresh";

    // Settings Categories
    public const string Profile = "account_circle";
    public const string ProfileFill = Profile + FilledSuffix;
    public const string Appearance = "palette";
    public const string Accessibility = "accessibility";
    public const string AccessibilityFill = Accessibility + FilledSuffix;
    public const string Sound = "volume_up";
    public const string Network = "wifi";
    public const string Dock = "space_dashboard";
    public const string Notifications = "notifications";
    public const string Bell = Notifications;
    public const string BellFill = Notifications + FilledSuffix;
    public const string Privacy = "shield";
    public const string Gaming = "sports_esports";
    public const string Updates = "download";
    public const string DateTime = "schedule";
    public const string SystemInfo = "info";
    public const string DoNotDisturb = "do_not_disturb_on";

    // System Bar & Status
    public const string Wifi = "wifi";
    public const string WifiOff = "wifi_off";
    public const string Ethernet = "lan";
    public const string Bluetooth = "bluetooth";
    public const string BluetoothOff = "bluetooth_disabled";
    public const string VolumeHigh = "volume_up";
    public const string VolumeLow = "volume_down";
    public const string VolumeMute = "volume_mute";
    public const string VolumeOff = "volume_off";
    public const string BatteryFull = "battery_full";
    public const string BatteryCharging = "battery_charging_full";
    public const string BatteryAlert = "battery_alert";
    public const string Sleep = "dark_mode";
    public const string SleepFill = Sleep + FilledSuffix;
    public const string Restart = "restart_alt";
    public const string ShutDown = "power_settings_new";
    public const string User = "person";
    public const string AccountSettings = "manage_accounts";
    public const string LockScreen = "lock";
    public const string ThemeDark = "dark_mode";
    public const string ThemeLight = "light_mode";

    // File Manager & Launcher
    public const string Folder = "folder";
    public const string FolderOpen = "folder_open";
    public const string GridView = "grid_view";
    public const string ListView = "view_list";
    public const string Eye = "visibility";
    public const string EyeOff = "visibility_off";
    public const string Pin = "push_pin";
    public const string PinOff = "keep_off";
    public const string HardDrive = "storage";
    public const string TextFormat = "title";
    public const string Document = "description";
    public const string Calculator = "calculate";
    public const string Code = "code";
    public const string Globe = "language";
    public const string Music = "music_note";
    public const string Chat = "chat";
    public const string School = "school";
    public const string Bank = "account_balance";
    public const string Play = "play_arrow";
    public const string Pause = "pause";
}
