using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DaisyOS.Core.Desktop;
using DaisyOS.Shell.Controls;
using DaisyOS.Shell.Services.Desktop;
using DaisyOS.Shell.ViewModels;
using DaisyOS.System.Display;
using DaisyOS.System.Processes;
using Point = Avalonia.Point;

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

        public DesktopView()
        {
            InitializeComponent();

            _viewModel = new DesktopViewModel();
            _metricsProvider = new AvaloniaMetricsProvider();
            _dragController = new DesktopDragController();

            DataContext = _viewModel;

            SizeChanged += DesktopView_SizeChanged;

            AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
            AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
            AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
            AddHandler(PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Bubble, handledEventsToo: true);
            AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        private void InvalidateReorderPanel()
        {
            var panel = this.FindDescendantOfType<DesktopReorderPanel>();
            panel?.InvalidateArrange();
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var sourceVisual = e.Source as Visual;
            var itemView = sourceVisual?.FindAncestorOfType<DesktopItemView>();
            var itemVm = itemView?.DataContext as DesktopItemViewModel;

            var surface = (Control?)this.FindControl<Grid>("InteractionSurface") ?? this;
            Point posInSurface = e.GetPosition(surface);
            Point posInItem = itemView != null ? e.GetPosition(itemView) : new Point(0, 0);
            Size itemSize = itemView != null ? itemView.Bounds.Size : new Size(74, 88);

            if (itemVm != null)
            {
                e.Pointer.Capture(surface);
            }

            _dragController.OnPointerPressed(
                itemVm,
                posInSurface,
                posInItem,
                itemSize,
                _viewModel,
                CurrentMetrics,
                InvalidateReorderPanel);
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var surface = (Control?)this.FindControl<Grid>("InteractionSurface") ?? this;
            Point posInSurface = e.GetPosition(surface);

            _dragController.OnPointerMoved(
                posInSurface,
                _viewModel,
                CurrentMetrics,
                InvalidateReorderPanel);
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            e.Pointer.Capture(null);
            _dragController.OnPointerReleased(
                _viewModel,
                CurrentMetrics,
                InvalidateReorderPanel);
        }

        private void OnDesktopItemDoubleTapped(object? sender, TappedEventArgs e)
        {
            var item = (e.Source as Visual)?.FindAncestorOfType<DesktopItemView>()?.DataContext as DesktopItemViewModel;
            if (item?.Id.Value != DesktopItemViewModel.TrashItemId)
            {
                return;
            }

            var result = new SafeProcessLauncher().Launch("xdg-open", ["trash:///"]);
            if (!result.Succeeded)
            {
                (Application.Current as App)?.Feedback.Show("DaisyOS could not open Trash.");
            }
        }

        private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            _dragController.OnPointerCaptureLost(
                _viewModel,
                InvalidateReorderPanel);
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _viewModel.IsEditMode = false;
                _dragController.CancelDrag(_viewModel, InvalidateReorderPanel);
            }
        }

        private void DesktopView_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            RebuildGrid();
        }

        private void RebuildGrid()
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

            _dragController.CancelDrag(_viewModel, InvalidateReorderPanel);
            var panel = this.FindDescendantOfType<DesktopReorderPanel>();
            panel?.SuppressFlipOnce();

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
