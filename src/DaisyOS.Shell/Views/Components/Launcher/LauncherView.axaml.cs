using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DaisyOS.Shell.ViewModels;
using DaisyOS.System.Power;
using DaisyOS.System.Processes;

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

    private async void OnAppItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is LauncherItemViewModel item)
        {
            // Launcher activation dismisses immediately, matching established desktop launchers.
            AppLaunchRequested?.Invoke(this, EventArgs.Empty);
            var result = await ViewModel.LaunchAppAsync(item);
            if (result is { Succeeded: false }) (Application.Current as App)?.Feedback.Show(result.Message);
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

    private async void OnSuspendClicked(object? sender, RoutedEventArgs e) =>
        await RunPowerActionAsync(service => service.SuspendAsync());

    private async void OnRestartClicked(object? sender, RoutedEventArgs e) =>
        await RunPowerActionAsync(service => service.RebootAsync());

    private async void OnShutdownClicked(object? sender, RoutedEventArgs e) =>
        await RunPowerActionAsync(service => service.ShutdownAsync());

    private static async Task RunPowerActionAsync(Func<LinuxPowerService, Task<DaisyOS.Core.Models.PowerActionResult>> action)
    {
        // Each menu selection is deliberate; systemd/logind remains the policy authority and
        // returns a clean failure when the session is not allowed to perform the action.
        var result = await action(new LinuxPowerService(new SafeCommandRunner()));
        if (!result.Succeeded) (Application.Current as App)?.Feedback.Show("DaisyOS could not complete that power action.");
    }
}
