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
        private XdgConfigureBatch? _initialBatch;
        private WLayerSurfaceProxy? _surfaceProxy;

        public LayerSink(LayerShellWindowImpl parent, bool secondShow) : base(parent)
        {
            var handle = parent.Client.CreateLayerShellHandle(
                parent._options,
                new WLayerSurfaceEventSinkProxy(this, WaylandMarshallers.UIThread));
            _surfaceProxy = handle.Proxy;
            parent._layerHandle = handle;
            parent._layerSurfaceProxy = _surfaceProxy;

            var initialBatch = handle.BasicInitCompleted.GetAwaiter().GetResult();
            _initialBatch = initialBatch;
            parent.ApplyConfigure(initialBatch, secondShow);
            _surfaceProxy.SetPendingAckSerial(initialBatch.Serial);
            _surfaceProxy.SetCursor(parent.CurrentCursor?.Cursor);
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

            if (ReferenceEquals(batch, _initialBatch))
            {
                _initialBatch = null;
                return;
            }

            Parent.ApplyConfigure(batch, secondShow: true);
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
