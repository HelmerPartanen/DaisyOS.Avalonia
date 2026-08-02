using Avalonia;
using Avalonia.Controls;
using DaisyOS.Core.Desktop;
using DaisyOS.System.Display;
using DaisyOS.Shell.ViewModels;

namespace DaisyOS.Shell.Views
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
            }
            
            if (_isDragging)
            {
                // Free-form movement during drag
                _draggedItem.X = _dragStartItemPos.X + dx;
                _draggedItem.Y = _dragStartItemPos.Y + dy;
            }
        }

        private void ItemsControl_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
        {
            if (_draggedItem == null || _currentMetrics == null) return;
            
            if (_isDragging)
            {
                // Snap to nearest grid cell based on pointer release position
                var pointerPos = e.GetPosition(this);
                var nearestCell = _currentMetrics.GetNearestCell(pointerPos.X, pointerPos.Y);
                
                // Commit to view model
                _viewModel.CommitItemMove(_draggedItem, nearestCell, _currentMetrics);
            }
            
            _draggedItem = null;
            _isDragging = false;
            e.Pointer.Capture(null);
        }
    }
}
