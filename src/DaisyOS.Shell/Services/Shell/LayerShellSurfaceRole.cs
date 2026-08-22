namespace DaisyOS.Shell.Services.Shell;

/// <summary>
/// The compositor role of a DaisyOS-owned Wayland surface. These are deliberately
/// distinct from Avalonia's normal xdg_toplevel windows: assigning a layer role
/// after an xdg role has been created is a Wayland protocol error.
/// </summary>
public enum LayerShellSurfaceRole
{
    Background,
    Panel,
    Overlay,
    Toplevel
}

[Flags]
public enum LayerShellAnchor
{
    None = 0,
    Top = 1,
    Bottom = 2,
    Left = 4,
    Right = 8
}

public enum LayerShellKeyboardInteractivity
{
    None,
    OnDemand,
    Exclusive
}

/// <summary>
/// Immutable request passed to the DaisyOS-maintained Avalonia Wayland backend
/// before it creates a wl_surface. It is intentionally not an attached property
/// because changing a Wayland surface role after creation is invalid.
/// </summary>
public sealed record LayerShellSurfaceOptions(
    LayerShellSurfaceRole Role,
    string Namespace,
    LayerShellAnchor Anchor,
    int ExclusiveZone,
    LayerShellKeyboardInteractivity KeyboardInteractivity,
    string? OutputName = null)
{
    public static LayerShellSurfaceOptions Desktop(string? outputName = null) => new(
        LayerShellSurfaceRole.Background,
        "daisyos.desktop",
        LayerShellAnchor.Top | LayerShellAnchor.Bottom | LayerShellAnchor.Left | LayerShellAnchor.Right,
        0,
        LayerShellKeyboardInteractivity.None,
        outputName);

    public static LayerShellSurfaceOptions PrimaryPanel(int exclusiveZone, string? outputName = null) => new(
        LayerShellSurfaceRole.Panel,
        "daisyos.primary-panel",
        LayerShellAnchor.Bottom | LayerShellAnchor.Left | LayerShellAnchor.Right,
        exclusiveZone,
        LayerShellKeyboardInteractivity.OnDemand,
        outputName);

    public static LayerShellSurfaceOptions Overlay(string surfaceNamespace, string? outputName = null) => new(
        LayerShellSurfaceRole.Overlay,
        surfaceNamespace,
        LayerShellAnchor.Top | LayerShellAnchor.Bottom | LayerShellAnchor.Left | LayerShellAnchor.Right,
        0,
        LayerShellKeyboardInteractivity.OnDemand,
        outputName);

    public void Validate()
    {
        if (Role == LayerShellSurfaceRole.Toplevel)
        {
            throw new InvalidOperationException("Normal application windows must not request layer-shell options.");
        }

        if (string.IsNullOrWhiteSpace(Namespace))
        {
            throw new InvalidOperationException("Every DaisyOS layer surface needs a stable namespace.");
        }

        if (ExclusiveZone < 0)
        {
            throw new InvalidOperationException("A layer-shell exclusive zone cannot be negative.");
        }
    }
}
