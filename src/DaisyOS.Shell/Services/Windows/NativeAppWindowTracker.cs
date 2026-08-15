using Avalonia;
using Avalonia.Controls;

namespace DaisyOS.Shell.Services.Windows;

/// <summary>
/// Owns state subscriptions for DaisyOS windows and provides taskbar semantics
/// without depending on compositor- or process-level window discovery.
/// </summary>
public sealed class NativeAppWindowTracker
{
    private sealed class TrackedWindow(Window window)
    {
        public Window Window { get; } = window;
        public WindowState RestoreState { get; set; } = WindowState.Normal;
    }

    private readonly NativeAppWindowStateStore _states = new();
    private readonly Dictionary<string, TrackedWindow> _windows = new(StringComparer.Ordinal);

    public NativeAppWindowTracker() => _states.StateChanged += (_, args) => StateChanged?.Invoke(this, args);

    public event EventHandler<NativeAppWindowStateChangedEventArgs>? StateChanged;

    public NativeAppWindowState GetState(string appId) => _states.GetState(appId);

    public void Register(string appId, Window window)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentNullException.ThrowIfNull(window);

        Unregister(appId);
        _windows.Add(appId, new TrackedWindow(window));
        window.Activated += OnWindowActivated;
        window.Deactivated += OnWindowDeactivated;
        window.Closed += OnWindowClosed;
        window.PropertyChanged += OnWindowPropertyChanged;
        _states.Register(appId);
    }

    /// <summary>Runs the standard taskbar action. Returns false if the app has no window.</summary>
    public bool ToggleFromTaskbar(string appId)
    {
        if (!_windows.TryGetValue(appId, out var tracked))
        {
            return false;
        }

        if (GetState(appId) == NativeAppWindowState.Active)
        {
            Minimize(appId, tracked);
            return true;
        }

        RestoreAndActivate(appId, tracked);
        return true;
    }

    /// <summary>Brings an existing owned window forward without applying the active-click minimize action.</summary>
    public bool RestoreAndActivate(string appId)
    {
        if (!_windows.TryGetValue(appId, out var tracked))
        {
            return false;
        }

        RestoreAndActivate(appId, tracked);
        return true;
    }

    public void Unregister(string appId)
    {
        if (!_windows.Remove(appId, out var tracked))
        {
            return;
        }

        tracked.Window.Activated -= OnWindowActivated;
        tracked.Window.Deactivated -= OnWindowDeactivated;
        tracked.Window.Closed -= OnWindowClosed;
        tracked.Window.PropertyChanged -= OnWindowPropertyChanged;
        _states.Unregister(appId);
    }

    private void Minimize(string appId, TrackedWindow tracked)
    {
        if (tracked.Window.WindowState is WindowState.Normal or WindowState.Maximized)
        {
            tracked.RestoreState = tracked.Window.WindowState;
        }

        tracked.Window.WindowState = WindowState.Minimized;
        _states.Minimize(appId);
    }

    private void RestoreAndActivate(string appId, TrackedWindow tracked)
    {
        if (tracked.Window.WindowState == WindowState.Minimized)
        {
            tracked.Window.WindowState = tracked.RestoreState;
            _states.Restore(appId);
        }

        tracked.Window.Activate();
        _states.Activate(appId);
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        if (FindAppId(sender as Window) is { } appId)
        {
            _states.Activate(appId);
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (FindAppId(sender as Window) is { } appId && GetState(appId) != NativeAppWindowState.Minimized)
        {
            _states.Deactivate(appId);
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (FindAppId(sender as Window) is { } appId)
        {
            Unregister(appId);
        }
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Window.WindowStateProperty || FindAppId(sender as Window) is not { } appId || !_windows.TryGetValue(appId, out var tracked))
        {
            return;
        }

        var state = e.GetNewValue<WindowState>();
        if (state == WindowState.Minimized)
        {
            _states.Minimize(appId);
            return;
        }

        if (state is WindowState.Normal or WindowState.Maximized)
        {
            tracked.RestoreState = state;
            _states.Restore(appId);
        }
    }

    private string? FindAppId(Window? window) => window is null
        ? null
        : _windows.FirstOrDefault(pair => ReferenceEquals(pair.Value.Window, window)).Key;
}
