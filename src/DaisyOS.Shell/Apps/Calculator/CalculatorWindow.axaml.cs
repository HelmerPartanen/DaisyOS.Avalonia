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
    // Default calculator window size.
    // 420 / 560 = 0.75, giving the window a 3:4 aspect ratio.
    private const double DefaultWidth = 400.0;
    private const double DefaultHeight = 560.0;
    private const double AspectRatio = DefaultWidth / DefaultHeight;

    private WindowEdge? _activeResizeEdge;
    private bool _isApplyingAspectRatio;

    public CalculatorWindow()
    {
        InitializeComponent();

        DataContext = new CalculatorViewModel();

        // Initial window size.
        Width = DefaultWidth;
        Height = DefaultHeight;

        // Keep the window locked to the desired aspect ratio.
        Resized += OnWindowResized;

        Loaded += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                GC.Collect(
                    2,
                    GCCollectionMode.Optimized,
                    false
                );
            }, DispatcherPriority.Background);
        };
    }

    public void TogglePerformanceOverlay()
    {
        PerfOverlay.ToggleOverlayVisibility();
    }

    protected override void OnClosed(EventArgs e)
    {
        Resized -= OnWindowResized;

        (DataContext as CalculatorViewModel)?.Dispose();

        base.OnClosed(e);
    }

    /// <summary>
    /// Keeps the window locked to the calculator's aspect ratio
    /// while the user manually resizes it.
    ///
    /// Dragging the top/bottom edges uses height as the controlling
    /// dimension.
    ///
    /// Dragging the left/right edges or corners uses width as the
    /// controlling dimension.
    /// </summary>
    private void OnWindowResized(
        object? sender,
        WindowResizedEventArgs e)
    {
        // Prevent recursion when we resize the window ourselves.
        if (_isApplyingAspectRatio)
        {
            return;
        }

        // Only correct manual user resizing.
        if (e.Reason != WindowResizeReason.User)
        {
            return;
        }

        // Do not interfere with maximized/fullscreen windows.
        if (WindowState == WindowState.Maximized ||
            WindowState == WindowState.FullScreen)
        {
            return;
        }

        var size = e.ClientSize;

        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        // If the user is dragging the top or bottom edge,
        // height determines the width.
        //
        // Otherwise width determines the height.
        bool drivesFromHeight =
            _activeResizeEdge is
                WindowEdge.North or
                WindowEdge.South;

        double targetWidth;
        double targetHeight;

        if (drivesFromHeight)
        {
            targetHeight = size.Height;
            targetWidth = targetHeight * AspectRatio;
        }
        else
        {
            targetWidth = size.Width;
            targetHeight = targetWidth / AspectRatio;
        }

        // Avoid tiny corrections / resize loops caused by
        // floating-point rounding.
        if (Math.Abs(targetWidth - size.Width) < 0.5 &&
            Math.Abs(targetHeight - size.Height) < 0.5)
        {
            return;
        }

        _isApplyingAspectRatio = true;

        try
        {
            ClientSize = new Size(
                targetWidth,
                targetHeight
            );
        }
        finally
        {
            _isApplyingAspectRatio = false;
        }
    }

    /// <summary>
    /// Starts a native resize operation from one of the custom
    /// resize handles.
    /// </summary>
    private void BeginEdgeResizeDrag(
        WindowEdge edge,
        PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this)
              .Properties
              .IsLeftButtonPressed)
        {
            return;
        }

        _activeResizeEdge = edge;

        BeginResizeDrag(edge, e);
    }

    /// <summary>
    /// Removes keyboard focus from text/search controls when
    /// clicking elsewhere in the calculator window.
    /// </summary>
    private void OnWindowPointerPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        if (e.Source is Visual source &&
            source is not TextBox &&
            source is not Controls.SearchBar &&
            !source.GetVisualAncestors().Any(
                v => v is Controls.SearchBar ||
                     v is TextBox))
        {
            FocusManager?.Focus(null);
        }
    }

    private void OnResizeTopPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        BeginEdgeResizeDrag(
            WindowEdge.North,
            e
        );
    }

    private void OnResizeBottomPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        BeginEdgeResizeDrag(
            WindowEdge.South,
            e
        );
    }

    private void OnResizeLeftPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        BeginEdgeResizeDrag(
            WindowEdge.West,
            e
        );
    }

    private void OnResizeRightPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        BeginEdgeResizeDrag(
            WindowEdge.East,
            e
        );
    }

    private void OnResizeTopLeftPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        BeginEdgeResizeDrag(
            WindowEdge.NorthWest,
            e
        );
    }

    private void OnResizeTopRightPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        BeginEdgeResizeDrag(
            WindowEdge.NorthEast,
            e
        );
    }

    private void OnResizeBottomLeftPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        BeginEdgeResizeDrag(
            WindowEdge.SouthWest,
            e
        );
    }

    private void OnResizeBottomRightPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        BeginEdgeResizeDrag(
            WindowEdge.SouthEast,
            e
        );
    }
}