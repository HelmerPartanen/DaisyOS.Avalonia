using Avalonia.Controls;
using Avalonia.Interactivity;
using DaisyOS.Shell.ViewModels;

namespace DaisyOS.Shell.Views.Components.Launcher;

public partial class LauncherView : UserControl
{
    public LauncherViewModel ViewModel { get; }

    public LauncherView()
    {
        InitializeComponent();
        ViewModel = new LauncherViewModel();
        DataContext = ViewModel;
    }

    private void OnAppItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is LauncherItemViewModel item)
            ViewModel.LaunchApp(item);
    }

    private void OnSetViewAlphabetical(object? sender, RoutedEventArgs e) =>
        ViewModel.SetViewAlphabetical();

    private void OnSetViewCategory(object? sender, RoutedEventArgs e) =>
        ViewModel.SetViewCategory();

    public void FocusSearch() =>
        this.FindControl<Controls.SearchBar>("LauncherSearchBar")?.FocusInput();
}
