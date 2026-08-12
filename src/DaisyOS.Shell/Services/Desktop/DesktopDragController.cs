using System;
using Avalonia;
using Avalonia.Input;
using DaisyOS.Core.Desktop;
using DaisyOS.Shell.ViewModels;
using Point = Avalonia.Point;

namespace DaisyOS.Shell.Services.Desktop;

public sealed class DesktopDragController
{
    public DragSession? ActiveSession => null;
    public DragState State => DragState.Idle;

#pragma warning disable CS0067
    public event Action<DragSession>? SessionStarted;
    public event Action<DragSession>? SessionUpdated;
    public event Action<DragSession, bool>? SessionEnded;
    public event Action? ReorderReflowed;
#pragma warning restore CS0067

    public DesktopDragController(DesktopMotionSettings? settings = null)
    {
    }

    public void OnPointerPressed(
        DesktopItemViewModel item,
        Point pointerPositionInSurface,
        Point pointerPositionInItem,
        Size itemSize,
        IPointer pointer,
        IInputElement captureElement)
    {
    }

    public void OnPointerMoved(
        Point currentPointer,
        DesktopViewModel viewModel,
        DesktopGridMetrics metrics)
    {
    }

    public void OnPointerReleased(IPointer pointer, DesktopViewModel viewModel)
    {
    }

    public void CancelDrag(DesktopViewModel? viewModel = null)
    {
    }

    public void OnPointerCaptureLost(DesktopViewModel? viewModel)
    {
    }
}
