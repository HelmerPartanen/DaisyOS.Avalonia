using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using DaisyOS.Core.Desktop;
using DaisyOS.Shell.Controls;
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
        public DesktopView()
        {
            InitializeComponent();

            _viewModel = new DesktopViewModel();
            _metricsProvider = new AvaloniaMetricsProvider();

            DataContext = _viewModel;

            SizeChanged += DesktopView_SizeChanged;
        }

        private void InvalidateReorderPanel()
        {
            var panel = this.FindDescendantOfType<DesktopReorderPanel>();
            panel?.InvalidateArrange();
        }

        private void DesktopView_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            RebuildGrid();
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
