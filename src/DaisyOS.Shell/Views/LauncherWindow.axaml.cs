using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using DaisyOS.Shell.Services.Compositor;
using DaisyOS.Shell.Views.Components.Launcher;

namespace DaisyOS.Shell.Views;

/// <summary>
/// A dedicated native surface for the launcher. Keeping it separate from the
/// fullscreen desktop window is required for KWin to blur the desktop behind it.
/// </summary>
public partial class LauncherWindow : Window
{
    private bool _hasPresentedContent;
    private bool _waitingForFirstContentFrame;
    private bool _blurEnabled;

    public LauncherWindow()
    {
        InitializeComponent();
        LauncherContent.AppLaunchRequested += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        KeyDown += OnKeyDown;
    }

    public event EventHandler? CloseRequested;

    private void EnableBlur()
    {
        if (_blurEnabled)
        {
            return;
        }

        _blurEnabled = true;
        KWinBlur.SetIsEnabled(this, true);
    }

    /// <summary>
    /// Prevents KWin from presenting the native material surface before Avalonia has
    /// produced the launcher's first content frame. Without this gate, the very first
    /// open can briefly expose the empty native window behind the control tree.
    /// </summary>
    public void PrepareForPresentation()
    {
        if (_hasPresentedContent || _waitingForFirstContentFrame)
        {
            return;
        }

        _waitingForFirstContentFrame = true;
        Opacity = 0;

        EventHandler? onLayoutUpdated = null;
        onLayoutUpdated = (_, _) =>
        {
            LauncherContent.LayoutUpdated -= onLayoutUpdated;
            // Frame one creates the native surface and control display list while it
            // remains transparent and has no KWin blur property. On frame two, first
            // publish the real content, then register its blur region for KWin.
            RequestAnimationFrame(_ => RequestAnimationFrame(_ =>
            {
                _waitingForFirstContentFrame = false;
                _hasPresentedContent = true;
                Opacity = 1;
                LauncherContent.FocusSearch();
                RequestAnimationFrame(_ => EnableBlur());
            }));
        };

        LauncherContent.LayoutUpdated += onLayoutUpdated;
    }

    public void PositionAboveBottomChrome(Window owner)
    {
        var workArea = owner.Screens.Primary?.WorkingArea;
        if (workArea is null)
        {
            return;
        }

        var scale = owner.RenderScaling;
        var width = (int)Math.Ceiling(Width * scale);
        var height = (int)Math.Ceiling(Height * scale);
        var bottomInset = (int)Math.Ceiling(64 * scale);
        Position = new PixelPoint(
            workArea.Value.X + Math.Max(0, (workArea.Value.Width - width) / 2),
            workArea.Value.Y + Math.Max(0, workArea.Value.Height - height - bottomInset));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}
