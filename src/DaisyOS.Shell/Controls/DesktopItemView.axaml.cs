using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DaisyOS.Shell.Controls;

public partial class DesktopItemView : UserControl
{
    public DesktopItemView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
