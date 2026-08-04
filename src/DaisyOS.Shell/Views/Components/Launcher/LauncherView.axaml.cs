using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DaisyOS.Shell.ViewModels;

namespace DaisyOS.Shell.Views.Components.Launcher;

public partial class LauncherView : UserControl
{
    public LauncherViewModel ViewModel { get; }
    public event EventHandler? AppLaunchRequested;

    public LauncherView()
    {
        InitializeComponent();
        Focusable = true;
        AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        ViewModel = new LauncherViewModel();
        DataContext = ViewModel;
    }

    private void OnAppItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is LauncherItemViewModel item)
        {
            // Launcher activation dismisses immediately, matching established desktop launchers.
            AppLaunchRequested?.Invoke(this, EventArgs.Empty);
            ViewModel.LaunchApp(item);
        }
    }

    private void OnSetViewAlphabetical(object? sender, RoutedEventArgs e) =>
        ViewModel.SetViewAlphabetical();

    private void OnSetViewCategory(object? sender, RoutedEventArgs e) =>
        ViewModel.SetViewCategory();

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        if (e.Source is not Visual source || IsInteractive(source))
        {
            return;
        }

        // A click on launcher canvas is a deliberate exit from search editing.
        // Move focus to the launcher itself so the TextBox reliably receives LostFocus.
        Focus();
    }

    private static bool IsInteractive(Visual source) =>
        source is TextBox or Button ||
        source.GetVisualAncestors().Any(ancestor => ancestor is TextBox or Button);

    public void FocusSearch() =>
        this.FindControl<Controls.SearchBar>("LauncherSearchBar")?.FocusInput();
}
