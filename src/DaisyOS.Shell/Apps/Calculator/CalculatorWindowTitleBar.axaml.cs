using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace DaisyOS.Shell.Apps.Calculator;

public partial class CalculatorWindowTitleBar : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<CalculatorWindowTitleBar, string>(nameof(Title), "Calculator");

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public CalculatorWindowTitleBar()
    {
        InitializeComponent();
    }

    private Window? HostWindow => TopLevel.GetTopLevel(this) as Window;

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            (e.Source is Visual source && IsInteractiveControl(source)))
        {
            return;
        }

        HostWindow?.BeginMoveDrag(e);
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source && IsInteractiveControl(source))
        {
            return;
        }

        ToggleMaximizeRestore();
    }

    private static bool IsInteractiveControl(Visual source)
    {
        return source is Button || source is TextBox || source is Controls.SearchBar ||
               source.GetVisualAncestors().Any(v => v is Button || v is TextBox || v is Controls.SearchBar);
    }

    private void OnTogglePerformanceClicked(object? sender, RoutedEventArgs e)
    {
        if (HostWindow is CalculatorWindow calculatorWindow)
        {
            calculatorWindow.TogglePerformanceOverlay();
        }
    }

    private void OnMinimizeClicked(object? sender, RoutedEventArgs e)
    {
        if (HostWindow is { } window)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void OnMaximizeRestoreClicked(object? sender, RoutedEventArgs e) => ToggleMaximizeRestore();

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => HostWindow?.Close();

    private void ToggleMaximizeRestore()
    {
        if (HostWindow is not { } window)
        {
            return;
        }

        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }
}