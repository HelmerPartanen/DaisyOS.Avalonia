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
    // 400 / 560 = 5:7, giving the window a comfortably portrait aspect ratio.
    private const double DefaultWidth = 400.0;
    private const double DefaultHeight = 560.0;
    private const double AspectRatio = DefaultWidth / DefaultHeight;

    private WindowEdge? _activeResizeEdge;
    private bool _isApplyingAspectRatio;

    public CalculatorWindow()
    {
        InitializeComponent();

        DataContext = new CalculatorViewModel();
        Activated += (_, _) => AppFrame.Classes.Set("WindowFocused", true);
        Deactivated += (_, _) => AppFrame.Classes.Set("WindowFocused", false);

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

        var targetSize = GetAspectConstrainedSize(size, drivesFromHeight);

        // Avoid tiny corrections / resize loops caused by
        // floating-point rounding.
        if (Math.Abs(targetSize.Width - size.Width) < 0.5 &&
            Math.Abs(targetSize.Height - size.Height) < 0.5)
        {
            return;
        }

        _isApplyingAspectRatio = true;

        try
        {
            ClientSize = targetSize;
        }
        finally
        {
            _isApplyingAspectRatio = false;
        }
    }

    /// <summary>
    /// Projects a proposed client size onto the calculator's aspect ratio while
    /// respecting the window's minimum and maximum dimensions as one pair.
    /// This avoids the platform clamping one dimension independently and
    /// leaving a stretched window behind.
    /// </summary>
    private Size GetAspectConstrainedSize(Size proposedSize, bool drivesFromHeight)
    {
        var minimumWidth = Math.Max(MinWidth, MinHeight * AspectRatio);
        var minimumHeight = minimumWidth / AspectRatio;

        var maximumWidth = MaxWidth;
        if (!double.IsInfinity(MaxHeight))
        {
            maximumWidth = Math.Min(maximumWidth, MaxHeight * AspectRatio);
        }

        if (drivesFromHeight)
        {
            var height = Math.Clamp(
                proposedSize.Height,
                minimumHeight,
                maximumWidth / AspectRatio);
            return new Size(height * AspectRatio, height);
        }

        var width = Math.Clamp(proposedSize.Width, minimumWidth, maximumWidth);
        return new Size(width, width / AspectRatio);
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
