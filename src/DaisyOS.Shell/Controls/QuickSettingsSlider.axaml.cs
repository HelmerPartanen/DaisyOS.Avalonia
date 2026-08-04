using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DaisyOS.Shell.Controls;

/// <summary>
/// Shared quick-settings range control with an icon well and a high-visibility vertical thumb.
/// </summary>
public partial class QuickSettingsSlider : UserControl
{
    private const double IconSegmentWidth = 40d;
    private bool _isAdjustingFromTrack;
    private readonly Border? _trackBackground;
    private readonly Border? _progressFill;
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
        _trackBackground = this.FindControl<Border>("TrackBackground");
        _progressFill = this.FindControl<Border>("ProgressFill");
        SizeChanged += (_, _) => UpdateProgressFill();
        AddHandler(PointerPressedEvent, OnTrackPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnTrackPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnTrackPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, (_, _) => _isAdjustingFromTrack = false);
    }

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

    private void UpdateProgressFill()
    {
        if (_trackBackground is null || _progressFill is null || Maximum <= Minimum)
        {
            return;
        }

        var progress = Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0d, 1d);
        var adjustableTrackWidth = Math.Max(0d, _trackBackground.Bounds.Width - IconSegmentWidth);
        _progressFill.Width = IconSegmentWidth + (adjustableTrackWidth * progress);
    }

    private void OnTrackPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isAdjustingFromTrack = true;
        e.Pointer.Capture(this);
        UpdateValueFromPointer(e);
        e.Handled = true;
    }

    private void OnTrackPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isAdjustingFromTrack)
        {
            return;
        }

        UpdateValueFromPointer(e);
        e.Handled = true;
    }

    private void OnTrackPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isAdjustingFromTrack)
        {
            return;
        }

        UpdateValueFromPointer(e);
        _isAdjustingFromTrack = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void UpdateValueFromPointer(PointerEventArgs e)
    {
        if (_trackBackground is null || _trackBackground.Bounds.Width <= 0d)
        {
            return;
        }

        var position = e.GetPosition(_trackBackground).X;
        var adjustableTrackWidth = Math.Max(1d, _trackBackground.Bounds.Width - IconSegmentWidth);
        var progress = Math.Clamp((position - IconSegmentWidth) / adjustableTrackWidth, 0d, 1d);
        SetCurrentValue(ValueProperty, Minimum + ((Maximum - Minimum) * progress));
    }
}
