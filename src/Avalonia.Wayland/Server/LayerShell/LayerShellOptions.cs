using System;

namespace Avalonia.Wayland.Server.LayerShell;

/// <summary>Creation-time role and geometry for a layer-shell surface.</summary>
public sealed record LayerShellOptions(
    ZwlrLayerShellV1.Layer Layer,
    ZwlrLayerSurfaceV1.Anchor Anchor,
    int ExclusiveZone,
    ZwlrLayerSurfaceV1.KeyboardInteractivity KeyboardInteractivity,
    string Namespace,
    string? OutputName = null,
    uint Width = 0,
    uint Height = 0)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Namespace))
            throw new ArgumentException("Layer-shell namespace is required.", nameof(Namespace));
        if (ExclusiveZone < 0)
            throw new ArgumentOutOfRangeException(nameof(ExclusiveZone));
    }
}
