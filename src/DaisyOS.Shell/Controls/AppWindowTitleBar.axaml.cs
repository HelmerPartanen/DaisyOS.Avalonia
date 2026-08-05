using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace DaisyOS.Shell.Controls;

/// <summary>Reusable client-side title bar for DottOS-owned normal windows.</summary>
public partial class AppWindowTitleBar : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<AppWindowTitleBar, string>(nameof(Title), "DottOS");

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public AppWindowTitleBar()
    {
        InitializeComponent();
    }

    private Window? HostWindow => TopLevel.GetTopLevel(this) as Window;

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            e.Source is Visual source && (source is Button || source.GetVisualAncestors().OfType<Button>().Any()))
        {
            return;
        }

        HostWindow?.BeginMoveDrag(e);
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source && (source is Button || source.GetVisualAncestors().OfType<Button>().Any()))
        {
            return;
        }

        ToggleMaximizeRestore();
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
