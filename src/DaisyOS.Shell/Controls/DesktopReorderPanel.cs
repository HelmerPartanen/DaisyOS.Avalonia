using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using DaisyOS.Core.Desktop;
using DaisyOS.Shell.ViewModels;
using Rect = Avalonia.Rect;
using Point = Avalonia.Point;

namespace DaisyOS.Shell.Controls;

public class DesktopReorderPanel : Panel
{
    private readonly Dictionary<DesktopItemId, Point> _previousOrigins = new();
    private bool _suppressFlipOnce;

    public static readonly StyledProperty<DesktopGridMetrics?> MetricsProperty =
        AvaloniaProperty.Register<DesktopReorderPanel, DesktopGridMetrics?>(nameof(Metrics));

    public DesktopGridMetrics? Metrics
    {
        get => GetValue(MetricsProperty);
        set => SetValue(MetricsProperty, value);
    }

    public static readonly StyledProperty<DesktopViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<DesktopReorderPanel, DesktopViewModel?>(nameof(ViewModel));

    public DesktopViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    static DesktopReorderPanel()
    {
        MetricsProperty.Changed.AddClassHandler<DesktopReorderPanel>((x, _) =>
        {
            x.SuppressFlipOnce();
            x.InvalidateArrange();
        });
        ViewModelProperty.Changed.AddClassHandler<DesktopReorderPanel>((x, _) => x.InvalidateArrange());
    }

    public void SuppressFlipOnce()
    {
        _suppressFlipOnce = true;
        _previousOrigins.Clear();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var childWidth = Metrics?.CellWidth ?? 74;
        var childHeight = Metrics?.CellHeight ?? 88;
        var childAvailableSize = new Size(childWidth, childHeight);
        foreach (var child in Children)
        {
            child.Measure(childAvailableSize);
        }
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Metrics == null || ViewModel == null)
        {
            foreach (var child in Children)
            {
                child.Arrange(new Rect(0, 0, child.DesiredSize.Width, child.DesiredSize.Height));
            }
            return finalSize;
        }

        var cellWidth = Metrics.CellWidth;
        var cellHeight = Metrics.CellHeight;

        foreach (var child in Children)
        {
            if (child is Control control && control.DataContext is DesktopItemViewModel item)
            {
                if (item.IsDragging)
                {
                    control.ZIndex = 100;
                    control.RenderTransform = null;
                    child.Arrange(new Rect(item.DragX, item.DragY, cellWidth, cellHeight));
                    _previousOrigins[item.Id] = new Point(item.DragX, item.DragY);
                    continue;
                }
                else
                {
                    control.ZIndex = 0;
                }

                var cell = ViewModel.GetCommittedCell(item.Id);
                if (!Metrics.IsValid(cell))
                {
                    child.Arrange(new Rect(0, 0, cellWidth, cellHeight));
                    continue;
                }

                var origin = Metrics.GetCellOrigin(cell);
                var newOrigin = new Point(origin.X, origin.Y);
                item.X = origin.X;
                item.Y = origin.Y;

                if (!_suppressFlipOnce && _previousOrigins.TryGetValue(item.Id, out var oldOrigin))
                {
                    double deltaX = newOrigin.X - oldOrigin.X;
                    double deltaY = newOrigin.Y - oldOrigin.Y;

                    if (Math.Abs(deltaX) > 0.5 || Math.Abs(deltaY) > 0.5)
                    {
                        double dX = Math.Abs(deltaX) < 0.001 ? 0 : -deltaX;
                        double dY = Math.Abs(deltaY) < 0.001 ? 0 : -deltaY;
                        string transformStr = FormattableString.Invariant($"translate({dX:F2}px, {dY:F2}px)");

                        var savedTransitions = control.Transitions;
                        control.Transitions = null;
                        control.RenderTransform = TransformOperations.Parse(transformStr);
                        control.Transitions = savedTransitions;

                        Dispatcher.UIThread.Post(() =>
                        {
                            control.RenderTransform = TransformOperations.Identity;
                        }, DispatcherPriority.Render);
                    }
                }

                _previousOrigins[item.Id] = newOrigin;
                child.Arrange(new Rect(origin.X, origin.Y, cellWidth, cellHeight));
            }
            else
            {
                child.Arrange(new Rect(0, 0, cellWidth, cellHeight));
            }
        }

        _suppressFlipOnce = false;
        return finalSize;
    }
}
