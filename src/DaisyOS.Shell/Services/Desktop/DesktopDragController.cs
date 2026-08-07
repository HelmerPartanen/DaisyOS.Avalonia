using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Input;
using DaisyOS.Core.Desktop;
using DaisyOS.Shell.ViewModels;

using Point = Avalonia.Point;

namespace DaisyOS.Shell.Services.Desktop;

public sealed class DesktopDragController
{
    private readonly DesktopMotionSettings _settings;

    private DragSession? _session;
    private DesktopViewModel? _currentViewModel;
    private DesktopGridMetrics? _currentMetrics;
    private long _dragGeneration;

    public DragSession? ActiveSession => _session;
    public DragState State => _session?.State ?? DragState.Idle;

    public event Action<DragSession>? SessionStarted;
    public event Action<DragSession>? SessionUpdated;
    public event Action<DragSession, bool>? SessionEnded;
    public event Action? ReorderReflowed;

    public DesktopDragController(DesktopMotionSettings? settings = null)
    {
        _settings = settings ?? DesktopMotionSettings.Default;
    }

    public void OnPointerPressed(
        DesktopItemViewModel item,
        Point pointerPositionInSurface,
        Point pointerPositionInItem,
        Size itemSize,
        IPointer pointer,
        IInputElement captureElement)
    {
        if (_session != null)
        {
            CancelDrag();
        }

        var generation = ++_dragGeneration;
        _session = new DragSession
        {
            SourceId = item.Id,
            OriginalIndex = 0,
            PressPoint = pointerPositionInSurface,
            GrabOffset = pointerPositionInItem,
            CurrentPointer = pointerPositionInSurface,
            CurrentDragCenter = new Point(pointerPositionInSurface.X - pointerPositionInItem.X + itemSize.Width / 2.0,
                                          pointerPositionInSurface.Y - pointerPositionInItem.Y + itemSize.Height / 2.0),
            LastPointer = pointerPositionInSurface,
            LastPointerTime = DateTimeOffset.UtcNow,
            State = DragState.Pressed,
            Generation = generation,
            ItemSize = itemSize
        };

        pointer.Capture(captureElement);
        Debug.WriteLine($"[DragController] Idle -> Pressed (Source: {item.Id})");
    }

    public void OnPointerMoved(
        Point currentPointer,
        DesktopViewModel viewModel,
        DesktopGridMetrics metrics)
    {
        if (_session == null || metrics == null || viewModel == null) return;
        _currentViewModel = viewModel;
        _currentMetrics = metrics;

        if (_session.State == DragState.Idle || _session.State == DragState.Pressed)
        {
            var delta = currentPointer - _session.PressPoint;
            var dist = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);

            if (dist >= _settings.MouseDragThreshold)
            {
                TransitionTo(DragState.Reordering);
                viewModel.BeginDrag(_session.SourceId);
                SessionStarted?.Invoke(_session);
            }
            else
            {
                return;
            }
        }

        // 1. Update position tracking and smoothed velocity EMA
        var now = DateTimeOffset.UtcNow;
        var dt = (now - _session.LastPointerTime).TotalSeconds;
        var instantaneousVelocity = dt > 0.001
            ? (currentPointer - _session.LastPointer) / dt
            : new Vector(0, 0);

        _session.SmoothedVelocity = _session.SmoothedVelocity * 0.75 + instantaneousVelocity * 0.25;
        _session.LastPointer = currentPointer;
        _session.LastPointerTime = now;
        _session.CurrentPointer = currentPointer;

        var dragTopLeft = new Point(currentPointer.X - _session.GrabOffset.X, currentPointer.Y - _session.GrabOffset.Y);
        var dragCenter = new Point(dragTopLeft.X + _session.ItemSize.Width / 2.0, dragTopLeft.Y + _session.ItemSize.Height / 2.0);
        _session.CurrentDragCenter = dragCenter;

        SessionUpdated?.Invoke(_session);

        // 2. Reorder Placeholder Movement (Allows moving to ANY cell)
        if (_session.State == DragState.Reordering)
        {
            var candidateCell = metrics.GetNearestCell(dragCenter.X, dragCenter.Y);
            if (viewModel.MovePreviewItemToCell(_session.SourceId, candidateCell, metrics))
            {
                ReorderReflowed?.Invoke();
            }
        }
    }

    public void OnPointerReleased(IPointer pointer, DesktopViewModel viewModel)
    {
        if (_session == null) return;

        var session = _session;

        viewModel.CommitReorder();

        pointer.Capture(null);
        SessionEnded?.Invoke(session, true);
        ResetInteraction();
    }

    public void CancelDrag(DesktopViewModel? viewModel = null)
    {
        if (_session == null) return;

        var session = _session;

        if (viewModel != null)
        {
            viewModel.CancelDrag();
        }

        SessionEnded?.Invoke(session, false);
        ResetInteraction();
    }

    public void OnPointerCaptureLost(DesktopViewModel? viewModel)
    {
        Debug.WriteLine("[DragController] Pointer capture lost -> CancelDrag");
        CancelDrag(viewModel);
    }

    private void ResetInteraction()
    {
        _session = null;
        _currentViewModel = null;
        _currentMetrics = null;
        Debug.WriteLine("[DragController] Interaction Reset to Idle");
    }

    private void TransitionTo(DragState nextState)
    {
        if (_session != null)
        {
            Debug.WriteLine($"[DragController] Transition: {_session.State} -> {nextState}");
            _session.State = nextState;
        }
    }
}
