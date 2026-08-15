using DaisyOS.Shell.Services.Windows;

namespace DaisyOS.Shell.Views.Components.Taskbar;

/// <summary>Maps native window state to the compact taskbar's stable visual language.</summary>
public readonly record struct TaskbarWindowPresentation(
    bool IsRunning,
    bool IsActive,
    bool IsMinimized,
    double IndicatorWidth,
    double IndicatorOpacity,
    string? StateLabel)
{
    public static TaskbarWindowPresentation From(NativeAppWindowState state) => state switch
    {
        NativeAppWindowState.Active => new(true, true, false, 20, 1, "active"),
        NativeAppWindowState.Minimized => new(true, false, true, 4, 1, "minimized"),
        NativeAppWindowState.RunningInactive => new(true, false, false, 14, 1, "running"),
        _ => new(false, false, false, 0, 0, null)
    };
}
