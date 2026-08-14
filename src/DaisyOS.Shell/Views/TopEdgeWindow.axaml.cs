using Avalonia;
using Avalonia.Controls;
using DaisyOS.Shell.Controls;

namespace DaisyOS.Shell.Views;

/// <summary>
/// Transparent native strip for the top-edge material. Keeping it outside the
/// fullscreen shell window gives KWin a real transparent surface to blur.
/// </summary>
public partial class TopEdgeWindow : Window
{
    public TopEdgeWindow()
    {
        InitializeComponent();
    }

    public TopEdgeMetaball TopEdge => TopEdgeContent;

    public Task PlayVolumePanelAsync() => TopEdge.PlayKeyboardRevealAsync();

    public void PositionAtTopOf(Window owner)
    {
        var workArea = owner.Screens.Primary?.WorkingArea;
        if (workArea is null)
        {
            return;
        }

        var scale = owner.RenderScaling;
        Width = workArea.Value.Width / scale;
        Position = new PixelPoint(workArea.Value.X, workArea.Value.Y);
    }
}
