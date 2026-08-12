using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace DaisyOS.Shell.Apps.Calculator;

public partial class CalculatorWindow : Window
{
    // Width / Height of the window's initial size (350x500). The window is always resized
    // back onto this ratio so the keypad grid never stretches or squashes out of shape.
    private const double AspectRatio = 350.0 / 500.0;

    private WindowEdge? _activeResizeEdge;
    private bool _isApplyingAspectRatio;

    public CalculatorWindow()
    {
        InitializeComponent();
        DataContext = new CalculatorViewModel();

        Resized += OnWindowResized;

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
        base.OnClosed(e);
        Resized -= OnWindowResized;
        (DataContext as CalculatorViewModel)?.Dispose();
    }

    /// <summary>
    /// Keeps the window locked to <see cref="AspectRatio"/> while the user drags an edge or
    /// corner. Dragging the top/bottom edges recomputes width from the new height; every other
    /// edge (left/right and the diagonal corners) recomputes height from the new width, which
    /// matches how a horizontal drag naturally feels.
    /// </summary>
    private void OnWindowResized(object? sender, WindowResizedEventArgs e)
    {
        if (_isApplyingAspectRatio ||
            e.Reason != WindowResizeReason.User ||
            WindowState == WindowState.Maximized ||
            WindowState == WindowState.FullScreen)
        {
            return;
        }

        var size = e.ClientSize;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var drivesFromHeight = _activeResizeEdge is WindowEdge.North or WindowEdge.South;

        var targetWidth = drivesFromHeight ? size.Height * AspectRatio : size.Width;
        var targetHeight = drivesFromHeight ? size.Height : size.Width / AspectRatio;

        if (Math.Abs(targetWidth - size.Width) < 0.5 && Math.Abs(targetHeight - size.Height) < 0.5)
        {
            return;
        }

        _isApplyingAspectRatio = true;
        Width = targetWidth;
        Height = targetHeight;
        _isApplyingAspectRatio = false;
    }

    private void BeginEdgeResizeDrag(WindowEdge edge, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _activeResizeEdge = edge;
        BeginResizeDrag(edge, e);
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

    private void OnResizeTopPressed(object? sender, PointerPressedEventArgs e) =>
        BeginEdgeResizeDrag(WindowEdge.North, e);

    private void OnResizeBottomPressed(object? sender, PointerPressedEventArgs e) =>
        BeginEdgeResizeDrag(WindowEdge.South, e);

    private void OnResizeLeftPressed(object? sender, PointerPressedEventArgs e) =>
        BeginEdgeResizeDrag(WindowEdge.West, e);

    private void OnResizeRightPressed(object? sender, PointerPressedEventArgs e) =>
        BeginEdgeResizeDrag(WindowEdge.East, e);

    private void OnResizeTopLeftPressed(object? sender, PointerPressedEventArgs e) =>
        BeginEdgeResizeDrag(WindowEdge.NorthWest, e);

    private void OnResizeTopRightPressed(object? sender, PointerPressedEventArgs e) =>
        BeginEdgeResizeDrag(WindowEdge.NorthEast, e);

    private void OnResizeBottomLeftPressed(object? sender, PointerPressedEventArgs e) =>
        BeginEdgeResizeDrag(WindowEdge.SouthWest, e);

    private void OnResizeBottomRightPressed(object? sender, PointerPressedEventArgs e) =>
        BeginEdgeResizeDrag(WindowEdge.SouthEast, e);
}