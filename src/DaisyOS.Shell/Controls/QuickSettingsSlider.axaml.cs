using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace DaisyOS.Shell.Controls;

/// <summary>
/// Shared quick-settings range control with an icon well and a high-visibility vertical thumb.
/// </summary>
public partial class QuickSettingsSlider : UserControl
{
    public static readonly StyledProperty<string> IconProperty =
        AvaloniaProperty.Register<QuickSettingsSlider, string>(nameof(Icon), "volume_up");

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<QuickSettingsSlider, double>(nameof(Minimum), 0d);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<QuickSettingsSlider, double>(nameof(Maximum), 100d);

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<QuickSettingsSlider, double>(nameof(Value), 0d, defaultBindingMode: BindingMode.TwoWay);

    static QuickSettingsSlider()
    {
        ValueProperty.Changed.AddClassHandler<QuickSettingsSlider>((slider, _) => slider.UpdateProgressFill());
    }

    public QuickSettingsSlider()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateProgressFill();
    }

    public event EventHandler<RangeBaseValueChangedEventArgs>? ValueChanged;

    public string Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private void OnSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        SetCurrentValue(ValueProperty, e.NewValue);
        ValueChanged?.Invoke(this, e);
    }

    private void UpdateProgressFill()
    {
        var track = this.FindControl<Border>("TrackBackground");
        var fill = this.FindControl<Border>("ProgressFill");
        if (track is null || fill is null || Maximum <= Minimum)
        {
            return;
        }

        var progress = Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0d, 1d);
        fill.Width = track.Bounds.Width * progress;
    }
}
