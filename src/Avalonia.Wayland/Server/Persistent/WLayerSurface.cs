using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Avalonia.Wayland.Server.Interop;
using Avalonia.Wayland.Server.LayerShell;
using Avalonia.Wayland.Server.Transient;
using NWayland.Protocols.Wayland;

namespace Avalonia.Wayland.Server.Persistent;

/// <summary>Worker-side zwlr_layer_surface_v1 backed by Avalonia's normal render path.</summary>
internal sealed class WLayerSurface : WSurface, ILayerSurface
{
    private readonly LayerShellOptions _options;
    private readonly WLayerSurfaceEventSinkProxy _eventSink;
    private ZwlrLayerSurfaceV1? _layerSurface;
    private uint? _pendingAckSerial;
    private bool _configured;
    private readonly TaskCompletionSource<XdgConfigureBatch> _initialConfigure = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public WLayerSurface(WaylandWorker worker, LayerShellOptions options, WLayerSurfaceEventSinkProxy eventSink)
        : base(worker, registerImmediately: false)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _eventSink = eventSink;
        RegisterWithWorker();
    }

    public Task<XdgConfigureBatch> BasicInitCompleted => _initialConfigure.Task;

    internal override WSurfaceEventSinkProxy EventSink => _eventSink;

    public override void OnConnected(WaylandConnection connection, WaylandGlobals globals)
    {
        var options = _options ?? throw new InvalidOperationException(
            "A layer surface reached the Wayland worker before its options were initialized.");
        LayerShellDiagnostics.Write($"connecting '{options.Namespace}'");
        base.OnConnected(connection, globals);
        LayerShellDiagnostics.Write($"'{options.Namespace}' created wl_surface");
        if (globals.LayerShell is null)
            throw new AvaloniaWaylandException("zwlr_layer_shell_v1 is required for a DaisyOS shell surface.");

        LayerShellDiagnostics.Write($"creating '{options.Namespace}' as {options.Layer}");

        // The protocol allows a null output to let the compositor choose, but
        // the current NWayland descriptor exposes this argument as non-null.
        // Select a concrete output instead. DaisyOS supplies an explicit name
        // when output routing is available; the first announced output is the
        // compositor's primary output for the single-output prototype.
        var output = string.IsNullOrWhiteSpace(options.OutputName)
            ? globals.Outputs.Outputs.FirstOrDefault()?.WlOutput
            : globals.Outputs.Outputs.FirstOrDefault(candidate =>
                string.Equals(candidate.XdgName ?? candidate.OutputName, options.OutputName, StringComparison.Ordinal))?.WlOutput;

        if (output is null)
            throw new AvaloniaWaylandException("The compositor did not announce an output for a DaisyOS shell surface.");

        _layerSurface = globals.LayerShell.GetLayerSurface(
            WlSurface!, output, options.Layer, options.Namespace,
            new LayerListener(this), connection.Queue);
        _layerSurface.SetSize(options.Width, options.Height);
        _layerSurface.SetAnchor(options.Anchor);
        _layerSurface.SetExclusiveZone(options.ExclusiveZone);
        _layerSurface.SetKeyboardInteractivity(options.KeyboardInteractivity);
        // A passive overlay (for example notification toasts) must never
        // become an invisible full-screen click target. A null input region
        // makes Wayland continue hit-testing the surface below it.
        if (options.InputPassthrough)
            WlSurface!.SetInputRegion(null!);
        WlSurface!.Commit();
    }

    public void SetPendingAckSerial(uint serial)
    {
        LayerShellDiagnostics.Write($"'{_options.Namespace}' acknowledged configure {serial}");
        _configured = true;
        _pendingAckSerial = serial;

        // Configure is delivered worker → UI → worker. The first wake occurs
        // while this surface is still NotReady; wake again after the UI has
        // accepted the serial so the initial layer buffer is actually drawn.
        Worker.WakeupRenderLoop();
    }

    public override PlatformRenderTargetState State =>
        _configured ? base.State : PlatformRenderTargetState.NotReadyWillWakeupRenderLoop;

    public override void OnBeforeNewBufferAttached(IRenderTarget.RenderTargetSceneInfo sceneInfo)
    {
        LayerShellDiagnostics.Write($"'{_options.Namespace}' attaching {sceneInfo.Size.Width}x{sceneInfo.Size.Height} buffer");
        if (_pendingAckSerial is { } serial)
        {
            _layerSurface!.AckConfigure(serial);
            _pendingAckSerial = null;
        }
        base.OnBeforeNewBufferAttached(sceneInfo);
    }

    public override void OnDisconnected()
    {
        _layerSurface?.Destroy();
        _layerSurface = null;
        _pendingAckSerial = null;
        _configured = false;
        base.OnDisconnected();
    }

    private sealed class LayerListener(WLayerSurface parent) : ZwlrLayerSurfaceV1.Listener
    {
        protected override void Configure(ZwlrLayerSurfaceV1 sender, uint serial, uint width, uint height)
        {
            LayerShellDiagnostics.Write($"'{parent._options.Namespace}' configured {width}x{height} (serial {serial})");
            var batch = new XdgConfigureBatch
            {
                Serial = serial,
                Size = new PixelSize((int)width, (int)height)
            };
            parent._eventSink.OnConfigure(batch);
            parent._initialConfigure.TrySetResult(batch);
            parent.Worker.WakeupRenderLoop();
        }

        protected override void Closed(ZwlrLayerSurfaceV1 sender) => parent._eventSink.OnClose();
    }
}

/// <summary>
/// Tiny opt-in startup trace for diagnosing compositor/session integration.
/// It is intentionally disabled outside the development prototype so normal
/// applications do not receive shell-specific stderr noise.
/// </summary>
internal static class LayerShellDiagnostics
{
    private static readonly bool Enabled = string.Equals(
        Environment.GetEnvironmentVariable("DAISYOS_WAYLAND_DIAGNOSTICS"), "1", StringComparison.Ordinal);

    public static void Write(string message)
    {
        if (Enabled)
            Console.Error.WriteLine($"[daisy-layer] {message}");
    }
}
