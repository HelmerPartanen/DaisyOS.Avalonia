using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DaisyOS.Core.Desktop;
using DaisyOS.Shell.Controls;
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

        private void InvalidateReorderPanel()
        {
            var panel = this.FindDescendantOfType<DesktopReorderPanel>();
            panel?.InvalidateArrange();
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

            var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
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
            var dpi = (TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0) * 96.0;

            double cellW = _metricsProvider.GetCellWidth(dpi);
            double cellH = _metricsProvider.GetCellHeight(dpi);

            CurrentMetrics = new DesktopGridMetrics("primary", workArea, dpi, cellW, cellH, edgeInsetX: 2, edgeInsetY: 2);
            _viewModel.CalculateLayout(CurrentMetrics);
            InvalidateReorderPanel();
        }
    }
}
