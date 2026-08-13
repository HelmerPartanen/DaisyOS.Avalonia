using Avalonia;
using Avalonia.Controls;
using DaisyOS.Shell.Views.Components.Taskbar;
using DaisyOS.Shell.Views.Components.SystemBar;

namespace DaisyOS.Shell.Views;

/// <summary>
/// Transparent native strip containing the bottom shell chrome. Its material pixels
/// live above the desktop, allowing KWin to blur only those material regions.
/// </summary>
public partial class BottomChromeWindow : Window
{
    public BottomChromeWindow()
    {
        InitializeComponent();
    }

    public TaskbarView Taskbar => TaskbarContent;
    public SystemBarView SystemBar => SystemBarContent;

    public void PositionAtBottomOf(Window owner)
    {
        var workArea = owner.Screens.Primary?.WorkingArea;
        if (workArea is null)
        {
            return;
        }

        var scale = owner.RenderScaling;
        Width = workArea.Value.Width / scale;
        var height = (int)Math.Ceiling(Height * scale);
        Position = new PixelPoint(
            workArea.Value.X,
            workArea.Value.Y + Math.Max(0, workArea.Value.Height - height));
    }
}
