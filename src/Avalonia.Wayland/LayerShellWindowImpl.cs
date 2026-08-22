using System;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Avalonia.Wayland.Server;
using Avalonia.Wayland.Server.LayerShell;
using Avalonia.Wayland.Server.Persistent;
using NWayland.Protocols.XdgShell;

namespace Avalonia.Wayland;

/// <summary>Window implementation for a configured zwlr_layer_shell_v1 surface.</summary>
internal sealed class LayerShellWindowImpl : WindowImpl
{
    private readonly LayerShellOptions _options;
    private WaylandSurfaceCreateResult<WLayerSurfaceProxy>? _layerHandle;
    private WLayerSurfaceProxy? _layerSurfaceProxy;

    public LayerShellWindowImpl(WaylandWorkerClient client, LayerShellOptions options)
        : base(client, createInitialSink: false)
    {
        _options = options;
        CurrentSink = new LayerSink(this, secondShow: false);
    }

    public override IPlatformRenderSurface[] Surfaces => _layerHandle?.GetRenderSurfaces() ?? [];

    // Layer-shell has no xdg_surface, xdg_popup parent or title-bar chrome.
    internal override WXdgShellSurfaceProxy? SurfaceProxy => null;

    public override IPopupImpl? CreatePopup() => null;

    public override void Show(bool activate, bool isDialog)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(LayerShellWindowImpl));

        if (CurrentSink is null)
            CurrentSink = new LayerSink(this, secondShow: true);

        Client.AnyThreadWakeupRenderLoop();
    }

    private void ApplyConfigure(XdgConfigureBatch batch, bool secondShow)
    {
        if (batch.Size is not { Width: > 0, Height: > 0 })
            return;

        var size = new Size(batch.Size.Width, batch.Size.Height);
        if (size == ClientSize)
            return;

        ClientSize = size;
        Resized?.Invoke(size, secondShow ? WindowResizeReason.Unspecified : WindowResizeReason.Layout);
    }

    private sealed class LayerSink : WindowBaseImpl.Sink, ILayerSurfaceEventSink
    {
        private new LayerShellWindowImpl Parent => (LayerShellWindowImpl)base.Parent;
        private WLayerSurfaceProxy? _surfaceProxy;
        private readonly bool _secondShow;
        private bool _configured;

        public LayerSink(LayerShellWindowImpl parent, bool secondShow) : base(parent)
        {
            _secondShow = secondShow;
            var handle = parent.Client.CreateLayerShellHandle(
                parent._options,
                new WLayerSurfaceEventSinkProxy(this, WaylandMarshallers.UIThread));
            _surfaceProxy = handle.Proxy;
            parent._layerHandle = handle;
            parent._layerSurfaceProxy = _surfaceProxy;
            _surfaceProxy.SetCursor(parent.CurrentCursor?.Cursor);

            // Unlike an xdg toplevel, a layer surface must not synchronously
            // wait for its first configure while Avalonia is constructing the
            // window. The configure callback is marshalled onto Avalonia's UI
            // dispatcher; blocking that dispatcher here deadlocks startup and
            // leaves a nested compositor with an empty, black scene. Until
            // KWin configures the surface WLayerSurface deliberately reports
            // NotReady, then this callback enables the first render.
        }

        protected override void DisconnectFromSurface()
        {
            var proxy = _surfaceProxy;
            _surfaceProxy = null;
            Parent._layerHandle = null;
            Parent._layerSurfaceProxy = null;
            proxy?.Disconnect();
        }

        public void OnConfigure(XdgConfigureBatch batch)
        {
            if (IsDisposed)
                return;

            Parent.ApplyConfigure(batch, secondShow: _configured || _secondShow);
            _configured = true;
            _surfaceProxy?.SetPendingAckSerial(batch.Serial);
        }

        public void OnClose()
        {
            if (IsDisposed)
                return;

            if (Parent.Closing?.Invoke(WindowCloseReason.WindowClosing) != true)
                Dispose();
        }
    }
}
