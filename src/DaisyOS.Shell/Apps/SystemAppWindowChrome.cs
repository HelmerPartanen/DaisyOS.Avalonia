using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DaisyOS.Shell.Apps;

/// <summary>
/// Keeps DaisyOS-owned application windows visually honest as their native state changes.
/// Maximized windows keep desktop-style controls; explicit full screen removes all chrome.
/// </summary>
internal sealed class SystemAppWindowChrome : IDisposable
{
    private readonly Window _window;
    private readonly Control _appFrame;
    private readonly Control _frameHighlight;
    private readonly Control _contentFrame;
    private readonly Control _titleBar;
    private readonly Grid _contentLayout;
    private readonly IReadOnlyList<Control> _resizeHandles;
    private readonly string _normalRowDefinitions;
    private readonly string _fullscreenRowDefinitions;
    private WindowState _stateBeforeFullscreen = WindowState.Normal;

    public SystemAppWindowChrome(
        Window window,
        Control appFrame,
        Control frameHighlight,
        Control contentFrame,
        Control titleBar,
        Grid contentLayout,
        IReadOnlyList<Control>? resizeHandles = null,
        string normalRowDefinitions = "44,*",
        string fullscreenRowDefinitions = "0,*")
    {
        _window = window;
        _appFrame = appFrame;
        _frameHighlight = frameHighlight;
        _contentFrame = contentFrame;
        _titleBar = titleBar;
        _contentLayout = contentLayout;
        _resizeHandles = resizeHandles ?? Array.Empty<Control>();
        _normalRowDefinitions = normalRowDefinitions;
        _fullscreenRowDefinitions = fullscreenRowDefinitions;

        _window.PropertyChanged += OnWindowPropertyChanged;
        _window.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        ApplyWindowState();
    }

    public void Dispose()
    {
        _window.PropertyChanged -= OnWindowPropertyChanged;
        _window.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty)
        {
            ApplyWindowState();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _window.WindowState == WindowState.FullScreen)
        {
            ExitFullscreen();
            e.Handled = true;
        }
    }

    private void ToggleFullscreen()
    {
        if (_window.WindowState == WindowState.FullScreen)
        {
            ExitFullscreen();
            return;
        }

        _stateBeforeFullscreen = _window.WindowState == WindowState.Minimized
            ? WindowState.Normal
            : _window.WindowState;
        _window.WindowState = WindowState.FullScreen;
    }

    private void ExitFullscreen() => _window.WindowState = _stateBeforeFullscreen;

    private void ApplyWindowState()
    {
        var isFullscreen = _window.WindowState == WindowState.FullScreen;
        var fillsDisplay = isFullscreen || _window.WindowState == WindowState.Maximized;
        _appFrame.Classes.Set("FilledWindow", fillsDisplay);
        _frameHighlight.Classes.Set("FilledWindow", fillsDisplay);
        _contentFrame.Classes.Set("FilledWindow", fillsDisplay);
        _titleBar.IsVisible = !isFullscreen;
        _contentLayout.RowDefinitions = new RowDefinitions(isFullscreen ? _fullscreenRowDefinitions : _normalRowDefinitions);

        foreach (var resizeHandle in _resizeHandles)
        {
            resizeHandle.IsHitTestVisible = !fillsDisplay;
        }
    }
}
