using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DaisyOS.Shell;
using DaisyOS.Core.Desktop;
using DaisyOS.System.Display;
using DaisyOS.Shell.Services.Wallpaper;
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
        private App? _app;

        // The fill-order slot last previewed under the pointer, so we only recompute the
        // reflow when the pointer actually moves into a different slot.
        private int? _lastHoverSlot;

        // High-performance wallpaper image service
        private WallpaperImageService? _wallpaperImageService;
        private WallpaperRenderMetrics _currentWallpaperMetrics;
        private bool _wallpaperMetricsInitialized;

        public DesktopView()
        {
            InitializeComponent();
            
            _viewModel = new DesktopViewModel();
            _metricsProvider = new AvaloniaMetricsProvider();
            
            DataContext = _viewModel;
            _app = Application.Current as App;
            if (_app is not null)
            {
                _app.WallpaperChanged += OnWallpaperChanged;
            }

            Unloaded += (_, _) =>
            {
                if (_app is not null)
                {
                    _app.WallpaperChanged -= OnWallpaperChanged;
                }
                _wallpaperImageService?.Dispose();
            };
            
            // Listen to layout changes to rebuild the grid
            this.SizeChanged += DesktopView_SizeChanged;
        }

        private async void OnWallpaperChanged(object? sender, string wallpaperUri)
        {
            var wallpaperImage = this.FindControl<Image>("WallpaperImage");
            if (wallpaperImage is null)
            {
                return;
            }

            try
            {
                // Create or update the wallpaper image service
                _wallpaperImageService?.Dispose();
                _wallpaperImageService = new WallpaperImageService(wallpaperUri);
                _wallpaperImageService.WallpaperBitmapChanged += (s, bitmap) =>
                {
                    // Update the image source on UI thread
                    if (Dispatcher.UIThread.CheckAccess())
                    {
                        wallpaperImage.Source = bitmap;
                    }
                };

                // Calculate current metrics
                if (!_wallpaperMetricsInitialized)
                {
                    UpdateWallpaperMetrics();
                    _wallpaperMetricsInitialized = true;
                }

                // Get the wallpaper bitmap asynchronously
                if (_currentWallpaperMetrics.PhysicalWidth > 0 && _currentWallpaperMetrics.PhysicalHeight > 0)
                {
                    var bitmap = await _wallpaperImageService.GetWallpaperAsync(_currentWallpaperMetrics);
                    if (bitmap is not null)
                    {
                        wallpaperImage.Source = bitmap;
                    }
                }
            }
            catch
            {
                // Keep the current wallpaper visible if an asset cannot be loaded.
            }
        }

        private void UpdateWallpaperMetrics()
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0)
            {
                return;
            }

            // Get the scaling factor from the current monitor
            // In a real implementation, this would come from display services
            double scaling = 1.0; // Default to 1.0 for now

            // Calculate physical pixel dimensions
            int physicalWidth = (int)Math.Round(Bounds.Width * scaling);
            int physicalHeight = (int)Math.Round(Bounds.Height * scaling);

            _currentWallpaperMetrics = WallpaperRenderMetrics.FromPixelSize(
                physicalWidth,
                physicalHeight,
                scaling,
                Stretch.UniformToFill);
        }

        private void DesktopView_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            RebuildGrid();
            
            // Update wallpaper metrics if the size changed
            if (_wallpaperImageService is not null && _wallpaperMetricsInitialized)
            {
                UpdateWallpaperMetrics();
                _ = RefreshWallpaperAsync();
            }
        }

        private async Task RefreshWallpaperAsync()
        {
            if (_wallpaperImageService is null || _currentWallpaperMetrics.PhysicalWidth <= 0)
            {
                return;
            }

            try
            {
                var wallpaperImage = this.FindControl<Image>("WallpaperImage");
                if (wallpaperImage is null)
                {
                    return;
                }

                var bitmap = await _wallpaperImageService.GetWallpaperAsync(_currentWallpaperMetrics);
                if (bitmap is not null)
                {
                    wallpaperImage.Source = bitmap;
                }
            }
            catch
            {
                // Ignore refresh errors
            }
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
                _viewModel.BeginDrag(_draggedItem, _currentMetrics);
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
