using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DaisyOS.Shell.Apps;

namespace DaisyOS.Shell.Apps.Calculator;

public partial class CalculatorWindow : Window
{
    private readonly SystemAppWindowChrome _windowChrome;

    public CalculatorWindow()
    {
        InitializeComponent();

        DataContext = new CalculatorViewModel();
        _windowChrome = new SystemAppWindowChrome(this, AppFrame, FrameHighlight, ContentFrame, TitleBar, ContentLayout);
        Activated += (_, _) => AppFrame.Classes.Set("WindowFocused", true);
        Deactivated += (_, _) => AppFrame.Classes.Set("WindowFocused", false);

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
        _windowChrome.Dispose();
        (DataContext as CalculatorViewModel)?.Dispose();

        base.OnClosed(e);
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

}
