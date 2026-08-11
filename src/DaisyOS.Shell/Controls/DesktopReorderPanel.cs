using System;
using Avalonia;
using Avalonia.Controls;
using DaisyOS.Core.Desktop;
using DaisyOS.Shell.Services.Desktop;
using DaisyOS.Shell.ViewModels;

using Rect = Avalonia.Rect;

namespace DaisyOS.Shell.Controls;

public class DesktopReorderPanel : Panel
{
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
        MetricsProperty.Changed.AddClassHandler<DesktopReorderPanel>((x, _) => x.InvalidateArrange());
        ViewModelProperty.Changed.AddClassHandler<DesktopReorderPanel>((x, _) => x.InvalidateArrange());
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        foreach (var child in Children)
        {
            ItemMotionAnimator.Attach(child, DesktopMotionSettings.Default.ReorderAnimationDuration);
        }
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
            if (child.DataContext is DesktopItemViewModel item)
            {
                var cell = ViewModel.GetPreviewCell(item.Id);
                var origin = Metrics.GetCellOrigin(cell);

                item.X = origin.X;
                item.Y = origin.Y;

                child.Arrange(new Rect(origin.X, origin.Y, cellWidth, cellHeight));
            }
            else
            {
                child.Arrange(new Rect(0, 0, cellWidth, cellHeight));
            }
        }

        return finalSize;
    }
}
