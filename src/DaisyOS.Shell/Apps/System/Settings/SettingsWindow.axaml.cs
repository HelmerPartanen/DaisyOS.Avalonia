using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace DaisyOS.Shell.Apps.System.Settings;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source &&
            source is not TextBox &&
            source is not Controls.SearchBar &&
            !source.GetVisualAncestors().Any(v => v is Controls.SearchBar || v is TextBox))
        {
            FocusManager?.Focus(null);
        }
    }

    private void OnResizeTopPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.North, e);
    }

    private void OnResizeBottomPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.South, e);
    }

    private void OnResizeLeftPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.West, e);
    }

    private void OnResizeRightPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.East, e);
    }

    private void OnResizeTopLeftPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.NorthWest, e);
    }

    private void OnResizeTopRightPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.NorthEast, e);
    }

    private void OnResizeBottomLeftPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.SouthWest, e);
    }

    private void OnResizeBottomRightPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.SouthEast, e);
    }
}
