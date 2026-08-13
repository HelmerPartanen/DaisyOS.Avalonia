using Avalonia;
using Avalonia.Controls;
using DaisyOS.Shell.Views.Components.SystemBar;

namespace DaisyOS.Shell.Views;

/// <summary>A native, unclipped compositor surface for Quick Settings.</summary>
public partial class QuickSettingsWindow : Window
{
    public QuickSettingsWindow()
    {
        InitializeComponent();
        QuickSettingsContent.ShowQuickSettingsPanelOnly();
    }

    public SystemBarView QuickSettings => QuickSettingsContent;

    public void PositionAboveSystemBar(Window owner)
    {
        var workArea = owner.Screens.Primary?.WorkingArea;
        if (workArea is null)
        {
            return;
        }

        var scale = owner.RenderScaling;
        var width = (int)Math.Ceiling(Width * scale);
        var height = (int)Math.Ceiling(Height * scale);
        var rightInset = (int)Math.Ceiling(4 * scale);
        var bottomChromeHeight = (int)Math.Ceiling(56 * scale);
        var gap = (int)Math.Ceiling(12 * scale);
        Position = new PixelPoint(
            workArea.Value.X + Math.Max(0, workArea.Value.Width - width - rightInset),
            workArea.Value.Y + Math.Max(0, workArea.Value.Height - bottomChromeHeight - gap - height));
    }
}
