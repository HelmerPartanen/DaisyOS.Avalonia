using Avalonia;
using Avalonia.Controls;

namespace DaisyOS.Shell.Views.Components.Console;

public partial class ConsoleHomeView : UserControl
{
    public static readonly StyledProperty<string> ControllerNameProperty =
        AvaloniaProperty.Register<ConsoleHomeView, string>(nameof(ControllerName), "Game controller");

    public ConsoleHomeView()
    {
        InitializeComponent();
    }

    public string ControllerName
    {
        get => GetValue(ControllerNameProperty);
        set => SetValue(ControllerNameProperty, value);
    }
}
