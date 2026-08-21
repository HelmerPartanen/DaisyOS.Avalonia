using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using DaisyOS.Shell.Services.Diagnostics;

namespace DaisyOS.Shell.Controls;

public partial class AppPerformanceOverlay : UserControl
{
    public static readonly StyledProperty<string> AppNameProperty =
        AvaloniaProperty.Register<AppPerformanceOverlay, string>(nameof(AppName), "App");

    public static readonly StyledProperty<bool> IsOverlayVisibleProperty =
        AvaloniaProperty.Register<AppPerformanceOverlay, bool>(nameof(IsOverlayVisible), false);

    public string AppName
    {
        get => GetValue(AppNameProperty);
        set => SetValue(AppNameProperty, value);
    }

    public bool IsOverlayVisible
    {
        get => GetValue(IsOverlayVisibleProperty);
        set => SetValue(IsOverlayVisibleProperty, value);
    }

    private AppPerformanceMonitor? _monitor;
    private DispatcherTimer? _timer;
    private bool _isCompactMode;

    static AppPerformanceOverlay()
    {
        AppNameProperty.Changed.AddClassHandler<AppPerformanceOverlay>((x, e) => x.UpdateAppName(e.GetNewValue<string>()));
        IsOverlayVisibleProperty.Changed.AddClassHandler<AppPerformanceOverlay>((x, e) => x.UpdateVisibility(e.GetNewValue<bool>()));
    }

    public AppPerformanceOverlay()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _monitor ??= new AppPerformanceMonitor();
        UpdateAppName(AppName);
        UpdateVisibility(IsOverlayVisible);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        StopTimer();
    }

    public void StartTimer()
    {
        if (_timer is null)
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1500)
            };
            _timer.Tick += (s, ev) => RefreshMetrics();
        }

        if (!_timer.IsEnabled)
        {
            _timer.Start();
            RefreshMetrics();
        }
    }

    public void StopTimer()
    {
        if (_timer is not null && _timer.IsEnabled)
        {
            _timer.Stop();
        }
    }

    public void ToggleOverlayVisibility()
    {
        IsOverlayVisible = !IsOverlayVisible;
    }

    private void UpdateVisibility(bool isVisible)
    {
        IsVisible = isVisible;
        if (isVisible)
        {
            StartTimer();
        }
        else
        {
            StopTimer();
        }
    }

    private void UpdateAppName(string name)
    {
        var txt = this.FindControl<TextBlock>("TxtAppName");
        if (txt != null)
        {
            txt.Text = string.IsNullOrWhiteSpace(name) ? "Performance" : name;
        }
    }

    private void RefreshMetrics()
    {
        if (_monitor is null || !IsVisible) return;

        try
        {
            var metrics = _monitor.Sample();

            // Detailed Panel Elements
            var txtPid = this.FindControl<TextBlock>("TxtPid");
            var txtCpu = this.FindControl<TextBlock>("TxtCpu");
            var txtRam = this.FindControl<TextBlock>("TxtRam");
            var txtThreads = this.FindControl<TextBlock>("TxtThreads");
            var txtGc = this.FindControl<TextBlock>("TxtGc");
            var txtUptime = this.FindControl<TextBlock>("TxtUptime");
            var iconCpu = this.FindControl<TextBlock>("IconCpu");

            // Compact Panel Elements
            var txtCompactCpu = this.FindControl<TextBlock>("TxtCompactCpu");
            var txtCompactRam = this.FindControl<TextBlock>("TxtCompactRam");

            if (txtPid != null) txtPid.Text = $"PID {metrics.ProcessId}";
            if (txtCpu != null) txtCpu.Text = $"{metrics.CpuUsagePercentage:F1}%";
            if (txtRam != null) txtRam.Text = $"{metrics.GcMemoryMB:F1} MB";
            if (txtThreads != null) txtThreads.Text = $"{metrics.ThreadCount}";
            if (txtGc != null) txtGc.Text = $"{metrics.WorkingSetMB:F1} MB";
            if (txtUptime != null) txtUptime.Text = $"{metrics.Uptime:hh\\:mm\\:ss}";

            if (txtCompactCpu != null) txtCompactCpu.Text = $"{metrics.CpuUsagePercentage:F0}%";
            if (txtCompactRam != null) txtCompactRam.Text = $"{metrics.GcMemoryMB:F0}MB";

            // Dynamic CPU color always resolves through the active semantic theme.
            if (iconCpu != null)
            {
                var resourceKey = metrics.CpuUsagePercentage > 60.0
                    ? "StatusDangerBrush"
                    : metrics.CpuUsagePercentage > 25.0
                        ? "StatusWarningBrush"
                        : "StatusSuccessBrush";
                iconCpu.Foreground = this.FindResource(resourceKey) as IBrush;
            }
        }
        catch
        {
        }
    }

    private void OnMinimizeOverlayClicked(object? sender, RoutedEventArgs e)
    {
        SetMode(compact: true);
    }

    private void OnExpandOverlayClicked(object? sender, RoutedEventArgs e)
    {
        SetMode(compact: false);
    }

    private void OnCompactPanelPressed(object? sender, PointerPressedEventArgs e)
    {
        SetMode(compact: false);
    }

    private void OnCloseOverlayClicked(object? sender, RoutedEventArgs e)
    {
        IsOverlayVisible = false;
    }

    private void SetMode(bool compact)
    {
        _isCompactMode = compact;
        var detailed = this.FindControl<Border>("DetailedPanel");
        var compactPanel = this.FindControl<Border>("CompactPanel");

        if (detailed != null) detailed.IsVisible = !compact;
        if (compactPanel != null) compactPanel.IsVisible = compact;
    }
}
