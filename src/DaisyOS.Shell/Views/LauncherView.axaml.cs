using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DaisyOS.Shell.Views;

public partial class LauncherView : UserControl
{
    public LauncherView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
