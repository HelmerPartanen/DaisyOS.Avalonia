using Avalonia;
using Avalonia.Controls;
using DaisyOS.Shell.Controls;

namespace DaisyOS.Shell.Views;

/// <summary>
/// Native host for the static top-edge shell pocket.
/// </summary>
public partial class TopEdgeWindow : Window
{
    public TopEdgeWindow()
    {
        InitializeComponent();
    }

    public void PositionAtTopOf(Window owner)
    {
        var screen = owner.Screens.Primary;
        if (screen is null)
        {
            return;
        }

        var scale = owner.RenderScaling;
        Width = screen.Bounds.Width / scale;
        Position = new PixelPoint(screen.Bounds.X, screen.Bounds.Y);
    }
}
