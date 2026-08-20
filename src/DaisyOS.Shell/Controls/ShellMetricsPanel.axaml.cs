using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DaisyOS.Shell.Services.Diagnostics;

namespace DaisyOS.Shell.Controls;

public partial class ShellMetricsPanel : UserControl
{
    public static readonly StyledProperty<bool> IsPanelOpenProperty =
        AvaloniaProperty.Register<ShellMetricsPanel, bool>(nameof(IsPanelOpen), false);

    public bool IsPanelOpen
    {
        get => GetValue(IsPanelOpenProperty);
        set => SetValue(IsPanelOpenProperty, value);
    }

    private ShellMetricsMonitor? _monitor;
    private DispatcherTimer? _timer;
    private string _currentSort = "Cpu";

    static ShellMetricsPanel()
    {
        IsPanelOpenProperty.Changed.AddClassHandler<ShellMetricsPanel>((x, e) => x.UpdateVisibility(e.GetNewValue<bool>()));
    }

    public ShellMetricsPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void InitializeMonitor(ShellMetricsMonitor monitor)
    {
        _monitor = monitor;
        if (IsPanelOpen)
        {
            RefreshMetrics();
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _monitor ??= new ShellMetricsMonitor();
        StartTimer();
        UpdateVisibility(IsPanelOpen);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        StopTimer();
    }

    public void TogglePanel()
    {
        IsPanelOpen = !IsPanelOpen;
    }

    public void ShowPanel()
    {
        IsPanelOpen = true;
    }

    public void HidePanel()
    {
        IsPanelOpen = false;
    }

    private void StartTimer()
    {
        if (_timer is null)
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            _timer.Tick += (s, ev) => RefreshMetrics();
        }

        if (!_timer.IsEnabled)
        {
            _timer.Start();
            RefreshMetrics();
        }
    }

    private void StopTimer()
    {
        if (_timer is not null && _timer.IsEnabled)
        {
            _timer.Stop();
        }
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

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        HidePanel();
    }

    private void OnSortCpuClicked(object? sender, RoutedEventArgs e)
    {
        SetSortMode("Cpu");
    }

    private void OnSortMemoryClicked(object? sender, RoutedEventArgs e)
    {
        SetSortMode("Memory");
    }

    private void OnSortComplexityClicked(object? sender, RoutedEventArgs e)
    {
        SetSortMode("Complexity");
    }

    private void SetSortMode(string mode)
    {
        _currentSort = mode;

        var btnCpu = this.FindControl<Button>("BtnSortCpu");
        var btnMem = this.FindControl<Button>("BtnSortMemory");
        var btnComp = this.FindControl<Button>("BtnSortComplexity");

        if (btnCpu != null) UpdateChipStyle(btnCpu, mode == "Cpu");
        if (btnMem != null) UpdateChipStyle(btnMem, mode == "Memory");
        if (btnComp != null) UpdateChipStyle(btnComp, mode == "Complexity");

        RefreshMetrics();
    }

    private static void UpdateChipStyle(Button button, bool isActive)
    {
        if (isActive)
        {
            button.Classes.Add("Active");
        }
        else
        {
            button.Classes.Remove("Active");
        }
    }

    public void RefreshMetrics()
    {
        if (_monitor is null || !IsVisible) return;

        try
        {
            var snapshot = _monitor.Sample(_currentSort);

            // Update Shell Summary Header & Cards
            var txtPid = this.FindControl<TextBlock>("TxtPid");
            var txtCpu = this.FindControl<TextBlock>("TxtShellCpu");
            var txtGc = this.FindControl<TextBlock>("TxtGcRam");
            var txtWs = this.FindControl<TextBlock>("TxtWorkingSet");
            var txtThreads = this.FindControl<TextBlock>("TxtThreads");
            var txtHandles = this.FindControl<TextBlock>("TxtHandles");
            var txtUptime = this.FindControl<TextBlock>("TxtUptime");
            var iconCpu = this.FindControl<TextBlock>("IconCpu");

            if (txtPid != null) txtPid.Text = $"PID {snapshot.ProcessId}";
            if (txtCpu != null) txtCpu.Text = $"{snapshot.CpuUsagePercentage:F1}%";
            if (txtGc != null) txtGc.Text = $"{snapshot.GcMemoryMB:F1} MB";
            if (txtWs != null) txtWs.Text = $"({snapshot.WorkingSetMB:F0} MB WS)";
            if (txtThreads != null) txtThreads.Text = $"{snapshot.ThreadCount} thr";
            if (txtHandles != null) txtHandles.Text = $"{snapshot.HandleCount} hnd";
            if (txtUptime != null) txtUptime.Text = $"{snapshot.Uptime:hh\\:mm\\:ss}";

            if (iconCpu != null)
            {
                var resourceKey = snapshot.CpuUsagePercentage > 60.0
                    ? "DangerBrush"
                    : snapshot.CpuUsagePercentage > 25.0
                        ? "WarningBrush"
                        : "SuccessBrush";
                iconCpu.Foreground = this.FindResource(resourceKey) as IBrush;
            }

            // Render Component Breakdown Cards
            var container = this.FindControl<StackPanel>("ComponentsContainer");
            if (container == null) return;

            container.Children.Clear();

            double maxVal = _currentSort switch
            {
                "Memory" => snapshot.Components.Max(c => c.EstimatedMemoryMB),
                "Complexity" => snapshot.Components.Max(c => (double)c.VisualElementCount),
                _ => snapshot.Components.Max(c => c.RelativeCpuPercent)
            };
            if (maxVal <= 0) maxVal = 1.0;

            int rank = 1;
            foreach (var comp in snapshot.Components)
            {
                var card = CreateComponentCard(comp, rank++, maxVal);
                container.Children.Add(card);
            }
        }
        catch
        {
            // Robust against transient sampling updates
        }
    }

    private Control CreateComponentCard(ShellComponentMetrics comp, int rank, double maxVal)
    {
        var border = new Border
        {
            Background = this.FindResource("OverlaySurfaceSubtleBrush") as IBrush,
            BorderBrush = this.FindResource("DividerBrush") as IBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = CornerRadius.Parse("8"),
            Padding = new Thickness(10, 8)
        };

        var mainGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
        };

        // Rank & Icon Badge
        var iconBorder = new Border
        {
            Background = this.FindResource("TertiarySurfaceBrush") as IBrush,
            CornerRadius = CornerRadius.Parse("6"),
            Padding = new Thickness(6),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var iconText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(comp.Icon) ? "widgets" : comp.Icon,
            FontFamily = new FontFamily("avares://DaisyOS.Shell/Assets/fonts#Material Symbols Rounded"),
            FontSize = 18,
            Foreground = this.FindResource("AccentBrush") as IBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        iconBorder.Child = iconText;
        Grid.SetRow(iconBorder, 0);
        Grid.SetColumn(iconBorder, 0);
        mainGrid.Children.Add(iconBorder);

        // Name, Category & Status Tag
        var nameStack = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };

        var titleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };

        var rankText = new TextBlock
        {
            Text = $"#{rank}",
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = this.FindResource("TextTertiaryBrush") as IBrush,
            VerticalAlignment = VerticalAlignment.Center
        };

        var nameText = new TextBlock
        {
            Text = comp.Name,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = this.FindResource("TextPrimaryBrush") as IBrush,
            VerticalAlignment = VerticalAlignment.Center
        };

        titleRow.Children.Add(rankText);
        titleRow.Children.Add(nameText);
        nameStack.Children.Add(titleRow);

        var detailsRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        var catText = new TextBlock
        {
            Text = comp.Category,
            FontSize = 10,
            Foreground = this.FindResource("TextTertiaryBrush") as IBrush
        };

        var statusBorder = new Border
        {
            CornerRadius = CornerRadius.Parse("4"),
            Padding = new Thickness(4, 1),
            Background = comp.Status switch
            {
                "Active" => new SolidColorBrush(Color.Parse("#2010B981")),
                "Idle" => new SolidColorBrush(Color.Parse("#20F59E0B")),
                _ => new SolidColorBrush(Color.Parse("#206B7280"))
            }
        };
        var statusText = new TextBlock
        {
            Text = comp.Status,
            FontSize = 9,
            FontWeight = FontWeight.Bold,
            Foreground = comp.Status switch
            {
                "Active" => this.FindResource("SuccessBrush") as IBrush,
                "Idle" => this.FindResource("WarningBrush") as IBrush,
                _ => this.FindResource("TextTertiaryBrush") as IBrush
            }
        };
        statusBorder.Child = statusText;

        detailsRow.Children.Add(catText);
        detailsRow.Children.Add(statusBorder);
        nameStack.Children.Add(detailsRow);

        Grid.SetRow(nameStack, 0);
        Grid.SetColumn(nameStack, 1);
        mainGrid.Children.Add(nameStack);

        // Numeric Metrics (CPU %, RAM MB, Elements)
        var metricsStack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 1
        };

        var primaryMetric = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = this.FindResource("TextPrimaryBrush") as IBrush
        };

        var secondaryMetric = new TextBlock
        {
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = this.FindResource("TextSecondaryBrush") as IBrush
        };

        if (_currentSort == "Memory")
        {
            primaryMetric.Text = $"{comp.EstimatedMemoryMB:F1} MB";
            secondaryMetric.Text = $"{comp.RelativeCpuPercent:F1}% CPU | {comp.VisualElementCount} elem";
        }
        else if (_currentSort == "Complexity")
        {
            primaryMetric.Text = $"{comp.VisualElementCount} elem";
            secondaryMetric.Text = $"{comp.RelativeCpuPercent:F1}% CPU | {comp.EstimatedMemoryMB:F1} MB";
        }
        else
        {
            primaryMetric.Text = $"{comp.RelativeCpuPercent:F1}% CPU";
            secondaryMetric.Text = $"{comp.EstimatedMemoryMB:F1} MB | {comp.VisualElementCount} elem";
        }

        metricsStack.Children.Add(primaryMetric);
        metricsStack.Children.Add(secondaryMetric);

        Grid.SetRow(metricsStack, 0);
        Grid.SetColumn(metricsStack, 2);
        mainGrid.Children.Add(metricsStack);

        // Progress / Load Bar below
        double ratio = _currentSort switch
        {
            "Memory" => comp.EstimatedMemoryMB / maxVal,
            "Complexity" => comp.VisualElementCount / maxVal,
            _ => comp.RelativeCpuPercent / maxVal
        };
        ratio = Math.Clamp(ratio, 0.04, 1.0);

        var progressTrack = new Border
        {
            Background = this.FindResource("TertiarySurfaceBrush") as IBrush,
            CornerRadius = CornerRadius.Parse("3"),
            Height = 4,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var barBrush = comp.RelativeCpuPercent > 20.0
            ? this.FindResource("DangerBrush") as IBrush
            : (comp.RelativeCpuPercent > 5.0
                ? this.FindResource("WarningBrush") as IBrush
                : this.FindResource("AccentBrush") as IBrush);

        var progressBar = new Border
        {
            Background = barBrush,
            CornerRadius = CornerRadius.Parse("3"),
            Height = 4,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        // Using RelativePanel or layout percentage via Width fraction
        progressBar.Width = Math.Max(12, 380 * ratio);

        progressTrack.Child = progressBar;
        Grid.SetRow(progressTrack, 1);
        Grid.SetColumn(progressTrack, 0);
        Grid.SetColumnSpan(progressTrack, 3);
        mainGrid.Children.Add(progressTrack);

        border.Child = mainGrid;
        return border;
    }
}
