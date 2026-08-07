using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DaisyOS.Core.Desktop;
using DaisyOS.Shell.Controls;
using DaisyOS.Shell.Services.Desktop;
using DaisyOS.Shell.Services.Wallpaper;
using DaisyOS.Shell.ViewModels;
using DaisyOS.System.Display;

namespace DaisyOS.Shell.Views.Components.Desktop
{
    public partial class DesktopView : UserControl
    {
        public static readonly DirectProperty<DesktopView, DesktopGridMetrics?> CurrentMetricsProperty =
            AvaloniaProperty.RegisterDirect<DesktopView, DesktopGridMetrics?>(
                nameof(CurrentMetrics),
                o => o.CurrentMetrics);

        private DesktopGridMetrics? _currentMetrics;
        public DesktopGridMetrics? CurrentMetrics
        {
            get => _currentMetrics;
            private set => SetAndRaise(CurrentMetricsProperty, ref _currentMetrics, value);
        }

        private readonly DesktopViewModel _viewModel;
        public DesktopViewModel ViewModel => _viewModel;

        private readonly AvaloniaMetricsProvider _metricsProvider;
        private readonly DesktopDragController _dragController;
        private App? _app;

        // Wallpaper service
        private WallpaperImageService? _wallpaperImageService;
        private WallpaperRenderMetrics _currentWallpaperMetrics;
        private bool _wallpaperMetricsInitialized;

        public DesktopView()
        {
            InitializeComponent();

            _viewModel = new DesktopViewModel();
            _metricsProvider = new AvaloniaMetricsProvider();
            _dragController = new DesktopDragController();

            _dragController.SessionStarted += OnDragSessionStarted;
            _dragController.SessionUpdated += OnDragSessionUpdated;
            _dragController.SessionEnded += OnDragSessionEnded;
            _dragController.ReorderReflowed += OnReorderReflowed;

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

            SizeChanged += DesktopView_SizeChanged;
        }

        private void OnDragSessionStarted(DragSession session)
        {
            var item = _viewModel.GetItem(session.SourceId);
            if (item == null) return;

            DragProxyView.DataContext = item;
            DragProxyView.IsHiddenPlaceholder = false; // Drag proxy visual must ALWAYS remain visible!
            DragProxyContainer.IsVisible = true;
            UpdateProxyPosition(session);
        }

        private void OnDragSessionUpdated(DragSession session)
        {
            UpdateProxyPosition(session);
        }

        private void OnDragSessionEnded(DragSession session, bool isSuccess)
        {
            DragProxyContainer.IsVisible = false;
            DragProxyView.DataContext = null;
            InvalidateReorderPanel();
        }

        private void OnReorderReflowed()
        {
            InvalidateReorderPanel();
        }

        private void InvalidateReorderPanel()
        {
            var panel = this.FindDescendantOfType<DesktopReorderPanel>();
            panel?.InvalidateArrange();
        }

        private void UpdateProxyPosition(DragSession session)
        {
            double posX = session.CurrentPointer.X - session.GrabOffset.X;
            double posY = session.CurrentPointer.Y - session.GrabOffset.Y;

            Canvas.SetLeft(DragProxyContainer, posX);
            Canvas.SetTop(DragProxyContainer, posY);
        }

        private async void OnWallpaperChanged(object? sender, string wallpaperUri)
        {
            var wallpaperImage = this.FindControl<Image>("WallpaperImage");
            if (wallpaperImage is null) return;

            try
            {
                _wallpaperImageService?.Dispose();
                _wallpaperImageService = new WallpaperImageService(wallpaperUri);
                _wallpaperImageService.WallpaperBitmapChanged += (s, bitmap) =>
                {
                    if (Dispatcher.UIThread.CheckAccess())
                    {
                        wallpaperImage.Source = bitmap;
                    }
                };

                if (!_wallpaperMetricsInitialized)
                {
                    UpdateWallpaperMetrics();
                    _wallpaperMetricsInitialized = true;
                }

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
            }
        }

        private void UpdateWallpaperMetrics()
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

            double scaling = 1.0;
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

            if (_wallpaperImageService is not null && _wallpaperMetricsInitialized)
            {
                UpdateWallpaperMetrics();
                _ = RefreshWallpaperAsync();
            }
        }

        private async Task RefreshWallpaperAsync()
        {
            if (_wallpaperImageService is null || _currentWallpaperMetrics.PhysicalWidth <= 0) return;

            try
            {
                var wallpaperImage = this.FindControl<Image>("WallpaperImage");
                if (wallpaperImage is null) return;

                var bitmap = await _wallpaperImageService.GetWallpaperAsync(_currentWallpaperMetrics);
                if (bitmap is not null)
                {
                    wallpaperImage.Source = bitmap;
                }
            }
            catch
            {
            }
        }

        private void RebuildGrid()
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

            var workArea = new DaisyOS.Core.Desktop.Rect(0, 0, Bounds.Width, Bounds.Height - 48);
            double dpi = 96.0;

            double cellW = _metricsProvider.GetCellWidth(dpi);
            double cellH = _metricsProvider.GetCellHeight(dpi);

            CurrentMetrics = new DesktopGridMetrics("primary", workArea, dpi, cellW, cellH, edgeInsetX: 2, edgeInsetY: 2);
            _viewModel.CalculateLayout(CurrentMetrics);
            InvalidateReorderPanel();
        }

        private void OnInteractionSurfacePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

            var surfacePoint = e.GetPosition(InteractionSurface);

            Control? sourceControl = e.Source as Control;
            DesktopItemView? itemView = sourceControl as DesktopItemView ?? sourceControl?.FindAncestorOfType<DesktopItemView>();
            DesktopItemViewModel? itemVm = itemView?.DataContext as DesktopItemViewModel;

            if (itemView != null && itemVm != null)
            {
                var itemPoint = e.GetPosition(itemView);
                var itemSize = itemView.Bounds.Size;
                if (itemSize.Width <= 0 || itemSize.Height <= 0)
                {
                    itemSize = new Size(74, 88);
                }

                _dragController.OnPointerPressed(
                    itemVm,
                    surfacePoint,
                    itemPoint,
                    itemSize,
                    e.Pointer,
                    InteractionSurface);

                e.Handled = true;
            }
        }

        private void OnInteractionSurfacePointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragController.State == DragState.Idle || CurrentMetrics == null) return;

            var surfacePoint = e.GetPosition(InteractionSurface);
            _dragController.OnPointerMoved(surfacePoint, _viewModel, CurrentMetrics);
        }

        private void OnInteractionSurfacePointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_dragController.State == DragState.Idle) return;

            _dragController.OnPointerReleased(e.Pointer, _viewModel);
            e.Handled = true;
        }

        private void OnInteractionSurfacePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (_dragController.State != DragState.Idle)
            {
                _dragController.OnPointerCaptureLost(_viewModel);
            }
        }

        private void OnInteractionSurfaceKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _dragController.State != DragState.Idle)
            {
                _dragController.CancelDrag(_viewModel);
                e.Handled = true;
            }
        }
    }
}
