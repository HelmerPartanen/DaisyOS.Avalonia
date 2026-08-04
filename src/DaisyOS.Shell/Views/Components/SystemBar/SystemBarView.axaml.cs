using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace DaisyOS.Shell.Views.Components.SystemBar;

public partial class SystemBarView : UserControl
{
    private readonly DispatcherTimer _clockTimer;

    public SystemBarView()
    {
        InitializeComponent();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        UpdateClock();
        _clockTimer.Start();

    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _clockTimer.Stop();

    private void UpdateClock()
    {
        var now = DateTime.Now;
        var culture = CultureInfo.CurrentCulture;
        var clockText = this.FindControl<TextBlock>("ClockText");
        var calendarHeading = this.FindControl<TextBlock>("CalendarHeading");

        if (clockText is not null)
        {
            var date = now.ToString("ddd MMM d", culture);
            var time = now.ToString("HH.mm", culture);
            clockText.Text = $"{date}  {time}";
        }

        if (calendarHeading is not null)
        {
            calendarHeading.Text = now.ToString("D", culture);
        }
    }

    private void OnVolumeChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        var volumeValue = this.FindControl<TextBlock>("VolumeValue");
        if (volumeValue is not null)
        {
            volumeValue.Text = $"{Math.Round(e.NewValue):0}%";
        }
    }
}
