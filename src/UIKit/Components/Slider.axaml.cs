using Avalonia;
using Avalonia.Controls;

namespace UIKit.Components;

public partial class Slider : UserControl
{
    public static readonly StyledProperty<double> MinimumProperty = AvaloniaProperty.Register<Slider, double>(nameof(Minimum));
    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<Slider, double>(nameof(Maximum), 100);
    public static readonly StyledProperty<double> StepProperty = AvaloniaProperty.Register<Slider, double>(nameof(Step), 1);
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<Slider, double>(nameof(Value), 62);

    public Slider() => InitializeComponent();

    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
}
