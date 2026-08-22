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
        : base(worker)
    {
        _options = options;
        _eventSink = eventSink;
    }

    public Task<XdgConfigureBatch> BasicInitCompleted => _initialConfigure.Task;

    public override void OnConnected(WaylandConnection connection, WaylandGlobals globals)
    {
        base.OnConnected(connection, globals);
        if (globals.LayerShell is null)
            throw new AvaloniaWaylandException("zwlr_layer_shell_v1 is required for a DaisyOS shell surface.");

        var output = string.IsNullOrWhiteSpace(_options.OutputName)
            ? null
            : globals.Outputs.Outputs.FirstOrDefault(candidate =>
                string.Equals(candidate.XdgName ?? candidate.OutputName, _options.OutputName, StringComparison.Ordinal))?.WlOutput;

        _layerSurface = globals.LayerShell.GetLayerSurface(
            WlSurface!, output, _options.Layer, _options.Namespace,
            new LayerListener(this), connection.Queue);
        _layerSurface.SetSize(_options.Width, _options.Height);
        _layerSurface.SetAnchor(_options.Anchor);
        _layerSurface.SetExclusiveZone(_options.ExclusiveZone);
        _layerSurface.SetKeyboardInteractivity(_options.KeyboardInteractivity);
        WlSurface!.Commit();
    }

    public void SetPendingAckSerial(uint serial)
    {
        _configured = true;
        _pendingAckSerial = serial;
    }

    public override PlatformRenderTargetState State =>
        _configured ? base.State : PlatformRenderTargetState.NotReadyWillWakeupRenderLoop;

    public override void OnBeforeNewBufferAttached(IRenderTarget.RenderTargetSceneInfo sceneInfo)
    {
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
