using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;

namespace UIKit;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void ThemeButton_OnClick(object? sender, RoutedEventArgs e) =>
        Application.Current!.RequestedThemeVariant = Application.Current.ActualThemeVariant == ThemeVariant.Dark
            ? ThemeVariant.Light
            : ThemeVariant.Dark;

}
