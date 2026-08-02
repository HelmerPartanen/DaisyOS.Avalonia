using Avalonia;
using Avalonia.Controls;
using DaisyOS.Core.Desktop;
using DaisyOS.System.Display;
using DaisyOS.Shell.ViewModels;

namespace DaisyOS.Shell.Views.Components.Desktop
{
    public partial class DesktopView : UserControl
    {
        private DesktopViewModel _viewModel;
        private AvaloniaMetricsProvider _metricsProvider;
        
        private DesktopGridMetrics? _currentMetrics;
        private DesktopItemViewModel? _draggedItem;
        private Avalonia.Point _dragStartPointerPos;
        private Avalonia.Point _dragStartItemPos;
        private bool _isDragging;

        // The fill-order slot last previewed under the pointer, so we only recompute the
        // reflow when the pointer actually moves into a different slot.
        private int? _lastHoverSlot;

        public DesktopView()
        {
            InitializeComponent();
            
            _viewModel = new DesktopViewModel();
            _metricsProvider = new AvaloniaMetricsProvider();
            
            DataContext = _viewModel;
            
            // Listen to layout changes to rebuild the grid
            this.SizeChanged += DesktopView_SizeChanged;
        }

        private void DesktopView_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            RebuildGrid();
        }

        private void RebuildGrid()
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
            
            // Note: 48px bottom inset to account exactly for the taskbar height.
            var workArea = new DaisyOS.Core.Desktop.Rect(0, 0, Bounds.Width, Bounds.Height - 48);
            
            // Assume 96 DPI for this example Avalonia control view.
            double dpi = 96.0;
            
            double cellW = _metricsProvider.GetCellWidth(dpi);
            double cellH = _metricsProvider.GetCellHeight(dpi);
            
            // Windows desktop grid places remaining space on the right/bottom.
            // Start with a small inset on the top-left edge.
            _currentMetrics = new DesktopGridMetrics("primary", workArea, dpi, cellW, cellH, edgeInsetX: 2, edgeInsetY: 2);
            
            _viewModel.CalculateLayout(_currentMetrics);
        }
        
        private void ItemsControl_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e.Source is Control control && control.DataContext is DesktopItemViewModel item)
            {
                if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
                
                _draggedItem = item;
                _dragStartPointerPos = e.GetPosition(this);
                _dragStartItemPos = new Avalonia.Point(item.X, item.Y);
                _isDragging = false;
                _lastHoverSlot = null;
                
                e.Pointer.Capture((Avalonia.Input.InputElement)sender!);
                e.Handled = true;
            }
        }

        private void ItemsControl_PointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
        {
            if (_draggedItem == null || _currentMetrics == null) return;
            
            var currentPointer = e.GetPosition(this);
            var dx = currentPointer.X - _dragStartPointerPos.X;
            var dy = currentPointer.Y - _dragStartPointerPos.Y;
            
            if (!_isDragging && (global::System.Math.Abs(dx) > 3 || global::System.Math.Abs(dy) > 3))
            {
                _isDragging = true;
                _draggedItem.IsDragging = true;
            }
            
            if (_isDragging)
            {
                // Free-form movement during drag
                _draggedItem.X = _dragStartItemPos.X + dx;
                _draggedItem.Y = _dragStartItemPos.Y + dy;

                var hoverCell = _currentMetrics.GetNearestCell(currentPointer.X, currentPointer.Y);
                var hoverSlot = _currentMetrics.GetSlotIndex(hoverCell);

                if (_lastHoverSlot != hoverSlot)
                {
                    _lastHoverSlot = hoverSlot;
                    _viewModel.PreviewReflow(_draggedItem, hoverSlot, _currentMetrics);
                }
            }
        }

        private void ItemsControl_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
        {
            if (_draggedItem == null || _currentMetrics == null) return;
            
            if (_isDragging)
            {
                // Resolve the drop slot from release position and commit - this reflows the
                // other items around it (matching whatever was last previewed) and places the
                // dragged item into the resulting gap.
                var pointerPos = e.GetPosition(this);
                var releaseCell = _currentMetrics.GetNearestCell(pointerPos.X, pointerPos.Y);
                var releaseSlot = _currentMetrics.GetSlotIndex(releaseCell);
                
                _viewModel.CommitItemMove(_draggedItem, releaseSlot, _currentMetrics);
            }
            
            _draggedItem.IsDragging = false;
            _draggedItem = null;
            _lastHoverSlot = null;
            _isDragging = false;
            e.Pointer.Capture(null);
        }
    }
}