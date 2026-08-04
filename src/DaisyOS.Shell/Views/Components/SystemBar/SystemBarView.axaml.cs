using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.System.Audio;
using DaisyOS.System.Processes;

namespace DaisyOS.Shell.Views.Components.SystemBar;

public partial class SystemBarView : UserControl
{
    private readonly DispatcherTimer _clockTimer;
    private readonly IAudioService _audioService = new LinuxAudioService(new SafeCommandRunner());

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

    private async void OnOutputDevicesButtonClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetOutputDevicesPageVisible(true);
        await LoadOutputDevicesAsync();
    }

    private void OnOutputDevicesBackClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        SetOutputDevicesPageVisible(false);

    private async Task LoadOutputDevicesAsync()
    {
        var host = this.FindControl<StackPanel>("OutputDevicesHost");
        if (host is null)
        {
            return;
        }

        host.Children.Clear();
        host.Children.Add(new TextBlock
        {
            Text = "Loading devices…",
            Foreground = this.FindResource("TextSecondaryBrush") as IBrush,
            Margin = new Avalonia.Thickness(10, 8)
        });

        IReadOnlyList<AudioDeviceInfo> devices;
        try
        {
            devices = await _audioService.GetAudioDevicesAsync();
        }
        catch
        {
            devices = Array.Empty<AudioDeviceInfo>();
        }

        host.Children.Clear();
        if (devices.Count == 0)
        {
            host.Children.Add(new TextBlock
            {
                Text = "No output devices available",
                Foreground = this.FindResource("TextSecondaryBrush") as IBrush,
                Margin = new Avalonia.Thickness(10, 8)
            });
            return;
        }

        foreach (var device in devices.OrderByDescending(device => device.IsDefault).ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            host.Children.Add(CreateOutputDeviceButton(device));
        }
    }

    private Button CreateOutputDeviceButton(AudioDeviceInfo device)
    {
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        content.Children.Add(new TextBlock
        {
            Text = "speaker",
            FontFamily = new FontFamily("avares://DaisyOS.Shell/Assets/fonts#Material Symbols Rounded"),
            FontSize = 18,
            VerticalAlignment = VerticalAlignment.Center
        });
        var name = new TextBlock
        {
            Text = FormatOutputDeviceName(device.Name),
            FontSize = 13,
            Margin = new Avalonia.Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(name, 1);
        content.Children.Add(name);

        var button = new Button
        {
            Content = content
        };
        button.Classes.Add("OutputDeviceItem");
        if (device.IsDefault)
        {
            button.Classes.Add("SelectedOutputDevice");
        }
        button.Click += async (_, _) =>
        {
            await _audioService.SetDefaultAudioDeviceAsync(device.Id);
            await RefreshOutputDevicesAsync();
        };
        return button;
    }

    private void SetOutputDevicesPageVisible(bool visible)
    {
        var mainPage = this.FindControl<StackPanel>("QuickSettingsMainPage");
        var outputPage = this.FindControl<StackPanel>("OutputDevicesPage");
        if (mainPage is not null)
        {
            mainPage.IsVisible = !visible;
        }

        if (outputPage is not null)
        {
            outputPage.IsVisible = visible;
        }
    }

    private static string FormatOutputDeviceName(string name)
    {
        var parts = name.Split(" - ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 &&
               parts[0].Equals("FL", StringComparison.OrdinalIgnoreCase) &&
               parts[1].Equals("Output", StringComparison.OrdinalIgnoreCase)
            ? string.Join(" - ", parts.Skip(2))
            : name;
    }

    private async Task RefreshOutputDevicesAsync()
    {
        await LoadOutputDevicesAsync();
    }

}
