using Avalonia;
using Avalonia.Controls;

namespace UIKit.Components;

public partial class DateTimeField : UserControl
{
    public static readonly StyledProperty<string> DateTextProperty =
        AvaloniaProperty.Register<DateTimeField, string>(nameof(DateText), "30 July 2026");
    public static readonly StyledProperty<string> TimeTextProperty =
        AvaloniaProperty.Register<DateTimeField, string>(nameof(TimeText), "12:00");

    public DateTimeField() => InitializeComponent();

    public string DateText { get => GetValue(DateTextProperty); set => SetValue(DateTextProperty, value); }
    public string TimeText { get => GetValue(TimeTextProperty); set => SetValue(TimeTextProperty, value); }
}
