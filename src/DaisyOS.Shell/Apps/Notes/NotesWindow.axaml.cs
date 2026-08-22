using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Avalonia.Threading;
using DaisyOS.Shell.Apps;

namespace DaisyOS.Shell.Apps.Notes;

public partial class NotesWindow : Window
{
    private readonly SystemAppWindowChrome _windowChrome;

    public NotesWindow()
    {
        InitializeComponent();
        DataContext = new NotesViewModel();
        _windowChrome = new SystemAppWindowChrome(this, AppFrame, FrameHighlight, ContentFrame, TitleBar, ContentLayout,
            [ResizeTop, ResizeBottom, ResizeLeft, ResizeRight, ResizeTopLeft, ResizeTopRight, ResizeBottomLeft, ResizeBottomRight]);
        Activated += (_, _) => AppFrame.Classes.Set("WindowFocused", true);
        Deactivated += (_, _) => AppFrame.Classes.Set("WindowFocused", false);

        Loaded += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                GC.Collect(2, GCCollectionMode.Optimized, false);
            }, DispatcherPriority.Background);
        };
    }

    public void TogglePerformanceOverlay()
    {
        PerfOverlay.ToggleOverlayVisibility();
    }

    protected override void OnClosed(EventArgs e)
    {
        _windowChrome.Dispose();
        base.OnClosed(e);
        (DataContext as NotesViewModel)?.Dispose();
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
