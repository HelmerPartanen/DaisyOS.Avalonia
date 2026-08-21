using System;
using Avalonia;
using DaisyOS.Core.Desktop;
using DaisyOS.Shell.ViewModels;
using Point = Avalonia.Point;

namespace DaisyOS.Shell.Services.Desktop;

public sealed class DesktopDragController
{
    private readonly DesktopMotionSettings _settings;
    private DesktopItemViewModel? _pressedItem;
    private DesktopItemViewModel? _draggedItem;
    private Size _pressedItemSize = new(74, 88);
    private Point _pressPoint;
    private Point _grabOffset;

    public DragSession? ActiveSession { get; private set; }
    public DragState State { get; private set; } = DragState.Idle;

    public event Action<DragSession>? SessionStarted;
    public event Action<DragSession>? SessionUpdated;
    public event Action<DragSession, bool>? SessionEnded;
    public event Action? ReorderReflowed;

    public DesktopDragController(DesktopMotionSettings? settings = null)
    {
        _settings = settings ?? DesktopMotionSettings.Default;
    }

    public void OnPointerPressed(
        DesktopItemViewModel? item,
        Point pointerPositionInSurface,
        Point pointerPositionInItem,
        Size itemSize,
        DesktopViewModel viewModel,
        DesktopGridMetrics? metrics,
        Action? invalidatePanel = null)
    {
        if (item is null) return;

        _pressedItem = item;
        _pressedItemSize = itemSize;
        _pressPoint = pointerPositionInSurface;
        _grabOffset = pointerPositionInItem;

        StartDrag(item, pointerPositionInSurface, viewModel, metrics, itemSize, invalidatePanel);
    }

    private void StartDrag(
        DesktopItemViewModel item,
        Point pointerPos,
        DesktopViewModel viewModel,
        DesktopGridMetrics? metrics,
        Size itemSize,
        Action? invalidatePanel)
    {
        if (ActiveSession != null || _draggedItem != null) return;

        int originalIndex = viewModel.GetCommittedIndex(item.Id);
        _draggedItem = item;
        item.IsDragging = true;
        item.IsHiddenPlaceholder = true;
        item.DragX = pointerPos.X - _grabOffset.X;
        item.DragY = pointerPos.Y - _grabOffset.Y;

        State = DragState.Reordering;
        ActiveSession = new DragSession
        {
            SourceId = item.Id,
            OriginalIndex = originalIndex,
            PressPoint = _pressPoint,
            GrabOffset = _grabOffset,
            CurrentPointer = pointerPos,
            CurrentDragCenter = new Point(item.DragX + itemSize.Width / 2.0, item.DragY + itemSize.Height / 2.0),
            PreviewIndex = originalIndex,
            State = DragState.Reordering,
            ItemSize = itemSize
        };

        SessionStarted?.Invoke(ActiveSession);
        invalidatePanel?.Invoke();
    }

    public void OnPointerMoved(
        Point currentPointer,
        DesktopViewModel viewModel,
        DesktopGridMetrics? metrics,
        Action? invalidatePanel = null)
    {
        if (_draggedItem == null && _pressedItem != null)
        {
            StartDrag(_pressedItem, currentPointer, viewModel, metrics, _pressedItemSize, invalidatePanel);
        }

        if (_draggedItem != null && ActiveSession != null && metrics != null)
        {
            _draggedItem.DragX = currentPointer.X - _grabOffset.X;
            _draggedItem.DragY = currentPointer.Y - _grabOffset.Y;

            double cellWidth = metrics.CellWidth;
            double cellHeight = metrics.CellHeight;
            double centerX = _draggedItem.DragX + cellWidth / 2.0;
            double centerY = _draggedItem.DragY + cellHeight / 2.0;

            ActiveSession.CurrentPointer = currentPointer;
            ActiveSession.CurrentDragCenter = new Point(centerX, centerY);

            GridCell targetCell = metrics.GetNearestCell(centerX, centerY);
            if (metrics.IsValid(targetCell))
            {
                bool moved = viewModel.ReorderItem(_draggedItem.Id, targetCell, metrics);
                if (moved)
                {
                    ReorderReflowed?.Invoke();
                }
            }

            SessionUpdated?.Invoke(ActiveSession);
            invalidatePanel?.Invoke();
        }
    }

    public void OnPointerReleased(
        DesktopViewModel? viewModel,
        DesktopGridMetrics? metrics,
        Action? invalidatePanel = null)
    {
        if (_draggedItem != null && viewModel != null && metrics != null)
        {
            var cell = viewModel.GetCommittedCell(_draggedItem.Id);
            if (metrics.IsValid(cell))
            {
                var origin = metrics.GetCellOrigin(cell);
                _draggedItem.DragX = origin.X;
                _draggedItem.DragY = origin.Y;
            }

            _draggedItem.IsDragging = false;
            _draggedItem.IsHiddenPlaceholder = false;

            if (ActiveSession != null)
            {
                SessionEnded?.Invoke(ActiveSession, true);
            }
        }

        _draggedItem = null;
        ActiveSession = null;
        _pressedItem = null;
        State = DragState.Idle;
        invalidatePanel?.Invoke();
    }

    public void CancelDrag(DesktopViewModel? viewModel = null, Action? invalidatePanel = null)
    {
        if (_draggedItem != null)
        {
            _draggedItem.IsDragging = false;
            _draggedItem.IsHiddenPlaceholder = false;
            if (ActiveSession != null)
            {
                SessionEnded?.Invoke(ActiveSession, false);
            }
        }

        _draggedItem = null;
        ActiveSession = null;
        _pressedItem = null;
        State = DragState.Idle;
        invalidatePanel?.Invoke();
    }

    public void OnPointerCaptureLost(DesktopViewModel? viewModel, Action? invalidatePanel = null)
    {
        CancelDrag(viewModel, invalidatePanel);
    }
}
