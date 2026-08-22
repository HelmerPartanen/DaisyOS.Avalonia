using System;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Wayland.Server.LayerShell;

namespace Avalonia.Wayland;

/// <summary>
/// Creates an Avalonia <see cref="Window"/> with a zwlr_layer_shell_v1 role.
///
/// The callback is deliberately the only creation path: the Wayland backend
/// must choose the surface role while Avalonia constructs the window's native
/// implementation.  Normal windows created outside this scope remain ordinary
/// xdg_toplevels.
/// </summary>
public static class LayerShellWindow
{
    private static readonly AsyncLocal<LayerShellOptions?> PendingOptions = new();

    public static T Create<T>(LayerShellOptions options, Func<T> createWindow)
        where T : Window
    {
        ArgumentNullException.ThrowIfNull(createWindow);
        options.Validate();

        if (PendingOptions.Value is not null)
            throw new InvalidOperationException("Layer-shell window creation cannot be nested.");

        try
        {
            PendingOptions.Value = options;
            return createWindow();
        }
        finally
        {
            PendingOptions.Value = null;
        }
    }

    internal static bool TryTakePendingOptions(out LayerShellOptions options)
    {
        if (PendingOptions.Value is { } pending)
        {
            PendingOptions.Value = null;
            options = pending;
            return true;
        }

        options = default!;
        return false;
    }
}
