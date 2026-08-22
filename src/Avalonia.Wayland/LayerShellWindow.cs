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
    // Avalonia creates native top-level implementations synchronously on its
    // UI thread. AsyncLocal is the wrong scope for this hand-off: execution
    // contexts captured by unrelated UI work can retain a stale (or cleared)
    // value and turn a later shell surface into an invalid layer window.
    private static readonly ThreadLocal<LayerShellOptions?> PendingOptions = new();

    public static T Create<T>(LayerShellOptions options, Func<T> createWindow)
        where T : Window
    {
        ArgumentNullException.ThrowIfNull(createWindow);
        options.Validate();

        if (PendingOptions.Value is not null)
            throw new InvalidOperationException("Layer-shell window creation cannot be nested.");

        try
        {
            WriteDiagnostics($"offering '{options.Namespace}' to the next native window");
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
            WriteDiagnostics($"assigned '{options.Namespace}' to a layer window");
            return true;
        }

        options = default!;
        return false;
    }

    private static void WriteDiagnostics(string message)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("DAISYOS_WAYLAND_DIAGNOSTICS"), "1", StringComparison.Ordinal))
            Console.Error.WriteLine($"[daisy-layer] {message}");
    }
}
