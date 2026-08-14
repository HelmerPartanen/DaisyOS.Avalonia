using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using DaisyOS.Shell;
using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.System.Audio;
using DaisyOS.System.Bluetooth;
using DaisyOS.System.Networking;
using DaisyOS.System.Processes;
using DaisyOS.Shell.Controls;
using DaisyOS.Shell.Services;
using DaisyOS.System.Power;

namespace DaisyOS.Shell.Views.Components.SystemBar;

public partial class SystemBarView : UserControl
{
    private readonly DispatcherTimer _clockTimer;
    private readonly IAudioService _audioService = new LinuxAudioService(new SafeCommandRunner());
    private readonly IWirelessNetworkService _wirelessNetworkService = new LinuxWirelessNetworkService(new SafeCommandRunner());
    private readonly IBluetoothService _bluetoothService = new LinuxBluetoothService(new SafeCommandRunner());
    private readonly IBatteryStatusService _batteryService = new LinuxBatteryStatusService();
    private readonly ShellSessionState _sessionState = (Application.Current as App)?.SessionState ?? new ShellSessionState();
    private CancellationTokenSource? _volumeUpdateCancellation;
    private TextBlock? _clockText;
    private TextBlock? _calendarHeading;
    private string? _lastClockValue;
    private string? _lastCalendarHeading;
    private string? _demoConnectedWifi = "DottOS Guest";
    private Task? _quickSettingsRefreshTask;
    private DateTimeOffset _lastQuickSettingsRefresh = DateTimeOffset.MinValue;
    private static readonly TimeSpan QuickSettingsRefreshInterval = TimeSpan.FromSeconds(15);

    public event EventHandler? QuickSettingsRequested;
    public event EventHandler? QuickSettingsDismissRequested;

    public SystemBarView()
    {
        InitializeComponent();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public bool IsQuickSettingsVisible => this.FindControl<Control>("QuickSettingsPanel")?.IsVisible == true;

    /// <summary>Shows Quick Settings as an overlay within the shell window.</summary>
    public void ShowQuickSettingsPanel()
    {
        this.FindControl<Control>("QuickSettingsPanel")!.IsVisible = true;
        SetQuickSettingsPage(QuickSettingsPage.Main);
        QueueQuickSettingsRefresh(force: true);
    }

    public void HideQuickSettingsPanel() =>
        this.FindControl<Control>("QuickSettingsPanel")!.IsVisible = false;

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _clockText ??= this.FindControl<TextBlock>("ClockText");
        _calendarHeading ??= this.FindControl<TextBlock>("CalendarHeading");
        UpdateClock();
        _clockTimer.Start();
        // Let first paint win. Fast adapter state preloads after the shell is interactive so
        // opening Quick Settings never waits behind a Wi-Fi scan or paired-device enumeration.
        Dispatcher.UIThread.Post(() => QueueQuickSettingsRefresh(), DispatcherPriority.Background);
    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _clockTimer.Stop();
        _volumeUpdateCancellation?.Cancel();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        var culture = CultureInfo.CurrentCulture;
        // System Bar uses the stable, compact 24-hour clock used throughout the shell.
        // The calendar flyout retains the localized full-date heading.
        var clockValue = now.ToString("HH.mm", CultureInfo.InvariantCulture);
        if (_clockText is not null && !string.Equals(clockValue, _lastClockValue, StringComparison.Ordinal))
        {
            _lastClockValue = clockValue;
            _clockText.Text = clockValue;
        }

        var calendarValue = now.ToString("D", culture);
        if (_calendarHeading is not null && !string.Equals(calendarValue, _lastCalendarHeading, StringComparison.Ordinal))
        {
            _lastCalendarHeading = calendarValue;
            _calendarHeading.Text = calendarValue;
        }
    }

    private async void OnThemeTileToggled(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not QuickSettingTile tile || Application.Current is not App app)
        {
            return;
        }

        await app.SetShellThemeAsync(tile.IsChecked ? ThemeVariant.Dark : ThemeVariant.Light);
    }

    private async void OnWifiTileToggled(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not QuickSettingTile tile) return;
        if (!await _wirelessNetworkService.SetEnabledAsync(tile.IsChecked))
        {
            tile.IsChecked = !tile.IsChecked;
            ReportFailure("DaisyOS could not change Wi-Fi. Check that NetworkManager is available.");
        }
        QueueQuickSettingsRefresh(force: true);
    }

    private async void OnBluetoothTileToggled(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not QuickSettingTile tile) return;
        if (!await _bluetoothService.SetEnabledAsync(tile.IsChecked))
        {
            tile.IsChecked = !tile.IsChecked;
            ReportFailure("DaisyOS could not change Bluetooth. Check that Bluetooth is available.");
        }
        QueueQuickSettingsRefresh(force: true);
    }

    private void OnFocusTileToggled(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is QuickSettingTile tile) _sessionState.DoNotDisturb = tile.IsChecked;
    }

    private void OnGamingTileToggled(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is QuickSettingTile tile) _sessionState.GamingMode = tile.IsChecked;
    }

    private async void OnVolumeChanged(object? sender, EventArgs e)
    {
        if (sender is not QuickSettingsSlider slider || !slider.IsLoaded) return;
        _volumeUpdateCancellation?.Cancel();
        _volumeUpdateCancellation?.Dispose();
        _volumeUpdateCancellation = new CancellationTokenSource();
        try
        {
            await Task.Delay(80, _volumeUpdateCancellation.Token);
            await _audioService.SetVolumeAsync(slider.Value, _volumeUpdateCancellation.Token);
            _sessionState.LastKnownVolume = slider.Value;
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            ReportFailure("DaisyOS could not change the volume.");
        }
    }

    private void OnSettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        QuickSettingsDismissRequested?.Invoke(this, EventArgs.Empty);
        (Application.Current as App)?.ShowSettings();
    }

    private void QueueQuickSettingsRefresh(bool force = false)
    {
        if (_quickSettingsRefreshTask is { IsCompleted: false }) return;
        if (!force && DateTimeOffset.UtcNow - _lastQuickSettingsRefresh < QuickSettingsRefreshInterval) return;
        _quickSettingsRefreshTask = RefreshQuickSettingsStateAsync();
    }

    private async Task RefreshQuickSettingsStateAsync()
    {
        try
        {
            // Detail discovery is intentionally deferred to the chevrons. These calls are
            // bounded adapter/radio probes and complete without network scanning.
            var wifiTask = _wirelessNetworkService.GetRadioStatusAsync();
            var bluetoothTask = _bluetoothService.GetAdapterStatusAsync();
            var batteryTask = _batteryService.GetStatusAsync();
            var volumeTask = _audioService.GetVolumeAsync();
            await Task.WhenAll(wifiTask, bluetoothTask, batteryTask, volumeTask);

            var wifi = await wifiTask;
            var bluetooth = await bluetoothTask;
            var battery = await batteryTask;
            var volume = await volumeTask;
            if (this.FindControl<QuickSettingTile>("WifiTile") is { } wifiTile)
            {
                wifiTile.IsEnabled = wifi.IsAvailable;
                wifiTile.IsChecked = wifi.IsEnabled;
                wifiTile.ToolTipText = wifi.Detail;
            }
            if (this.FindControl<QuickSettingTile>("BluetoothTile") is { } bluetoothTile)
            {
                bluetoothTile.IsEnabled = bluetooth.IsAvailable;
                bluetoothTile.IsChecked = bluetooth.IsEnabled;
                bluetoothTile.ToolTipText = bluetooth.Detail;
            }
            if (this.FindControl<QuickSettingTile>("FocusTile") is { } focusTile) focusTile.IsChecked = _sessionState.DoNotDisturb;
            if (this.FindControl<QuickSettingTile>("GamingTile") is { } gamingTile) gamingTile.IsChecked = _sessionState.GamingMode;
            if (this.FindControl<QuickSettingTile>("ThemeTile") is { } themeTile)
            {
                themeTile.IsChecked = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
                themeTile.ToolTipText = themeTile.IsChecked ? "Use light theme" : "Use dark theme";
            }
            if (this.FindControl<QuickSettingsSlider>("VolumeSlider") is { } volumeSlider)
            {
                volumeSlider.IsEnabled = volume is not null;
                volumeSlider.Value = volume ?? _sessionState.LastKnownVolume ?? 0;
            }
            if (this.FindControl<TextBlock>("BatteryText") is { } batteryText)
            {
                batteryText.Text = battery.DisplayText;
                ToolTip.SetTip(batteryText, battery.Detail);
            }
            if (this.FindControl<StackPanel>("BatteryStatusHost") is { } batteryStatusHost)
            {
                // A desktop on mains power has no battery status to communicate. Keeping the
                // footer quiet is clearer than presenting an unavailable hardware warning.
                batteryStatusHost.IsVisible = battery.IsPresent;
            }
        }
        catch
        {
            // Individual controls retain an explicit unavailable/disabled state rather than invented data.
            if (this.FindControl<QuickSettingsSlider>("VolumeSlider") is { } volumeSlider) volumeSlider.IsEnabled = false;
        }
        finally
        {
            _lastQuickSettingsRefresh = DateTimeOffset.UtcNow;
        }
    }

    private async void OnOutputDevicesButtonClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetQuickSettingsPage(QuickSettingsPage.OutputDevices);
        await LoadOutputDevicesAsync();
    }

    private void OnOutputDevicesBackClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        SetQuickSettingsPage(QuickSettingsPage.Main);

    private async void OnWifiNetworksButtonClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetQuickSettingsPage(QuickSettingsPage.WifiNetworks);
        await LoadWifiNetworksAsync();
    }

    private void OnWifiNetworksBackClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        SetQuickSettingsPage(QuickSettingsPage.Main);

    private async void OnBluetoothDevicesButtonClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetQuickSettingsPage(QuickSettingsPage.BluetoothDevices);
        await LoadBluetoothDevicesAsync();
    }

    private void OnBluetoothDevicesBackClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        SetQuickSettingsPage(QuickSettingsPage.Main);

    private void OnQuickSettingsRequested(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        QuickSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCalendarFlyoutOpened(object? sender, EventArgs e) =>
        SetFlyoutButtonActive("CalendarButton", true);

    private void OnCalendarFlyoutClosed(object? sender, EventArgs e) =>
        SetFlyoutButtonActive("CalendarButton", false);

    private void SetFlyoutButtonActive(string buttonName, bool isActive) =>
        this.FindControl<Button>(buttonName)?.Classes.Set("ShellButtonActive", isActive);

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
            Text = device.IconGlyph,
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
        button.Classes.Add("ShellButton");
        button.Classes.Add("OutputDeviceItem");
        if (device.IsDefault)
        {
            button.Classes.Add("SelectedOutputDevice");
        }
        button.Click += async (_, _) =>
        {
            if (!await _audioService.SetDefaultAudioDeviceAsync(device.Id))
            {
                ToolTip.SetTip(button, "DaisyOS could not switch to this output.");
                ReportFailure("DaisyOS could not switch to that audio output.");
            }
            await RefreshOutputDevicesAsync();
        };
        return button;
    }

    private void SetQuickSettingsPage(QuickSettingsPage page)
    {
        var mainPage = this.FindControl<Grid>("QuickSettingsMainPage");
        var outputPage = this.FindControl<StackPanel>("OutputDevicesPage");
        var wifiPage = this.FindControl<StackPanel>("WifiNetworksPage");
        var bluetoothPage = this.FindControl<StackPanel>("BluetoothDevicesPage");

        if (mainPage is not null) mainPage.IsVisible = page == QuickSettingsPage.Main;
        if (outputPage is not null) outputPage.IsVisible = page == QuickSettingsPage.OutputDevices;
        if (wifiPage is not null) wifiPage.IsVisible = page == QuickSettingsPage.WifiNetworks;
        if (bluetoothPage is not null) bluetoothPage.IsVisible = page == QuickSettingsPage.BluetoothDevices;
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

    private async Task LoadWifiNetworksAsync()
    {
        var host = this.FindControl<StackPanel>("WifiNetworksHost");
        if (host is null) return;

        host.Children.Clear();
        host.Children.Add(CreateStatusText("Loading networks…"));

        WirelessNetworkStatus status;
        try
        {
            status = IsDemoWifiEnabled()
                ? CreateDemoWifiStatus()
                : await _wirelessNetworkService.GetNetworksAsync();
        }
        catch
        {
            status = new WirelessNetworkStatus(false, Array.Empty<WirelessNetworkInfo>(), "Wi-Fi networks could not be loaded.");
        }

        host.Children.Clear();
        if (!status.IsAvailable || status.Networks.Count == 0)
        {
            host.Children.Add(CreateStatusText(status.Detail));
            return;
        }

        foreach (var network in status.Networks)
        {
            host.Children.Add(CreateWifiNetworkButton(network));
        }
    }

    private Button CreateWifiNetworkButton(WirelessNetworkInfo network)
    {
        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("18,10,*"),
            RowDefinitions = new RowDefinitions(network.IsActive ? "20,16" : "36"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var signalIcon = new WifiSignalIcon
        {
            SignalPercent = network.SignalPercent,
            Width = 18,
            Height = 18,
            ActiveBrush = this.FindResource("TextPrimaryBrush") as IBrush ?? Brushes.White,
            InactiveBrush = this.FindResource("TextTertiaryBrush") as IBrush ?? Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center,
        };
        content.Children.Add(signalIcon);
        Grid.SetRowSpan(signalIcon, network.IsActive ? 2 : 1);

        var name = new TextBlock
        {
            Text = network.Ssid,
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(name, 2);
        content.Children.Add(name);

        if (network.IsActive)
        {
            var detail = new TextBlock
            {
                Text = "Connected",
                FontSize = 11,
                Foreground = this.FindResource("TextSecondaryBrush") as IBrush ?? Brushes.Gray,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(detail, 1);
            Grid.SetColumn(detail, 2);
            content.Children.Add(detail);
        }

        var button = CreateQuickSettingsListButton(content, false);
        button.Classes.Set("ConnectedWifiNetwork", network.IsActive);
        if (IsDemoWifiEnabled())
        {
            button.Click += async (_, _) =>
            {
                _demoConnectedWifi = network.IsActive ? null : network.Ssid;
                await LoadWifiNetworksAsync();
            };
        }
        else if (network.IsActive)
        {
            button.Click += async (_, _) =>
            {
                await _wirelessNetworkService.DisconnectFromNetworkAsync();
                await LoadWifiNetworksAsync();
            };
        }
        else if (network.Security.Equals("Open", StringComparison.OrdinalIgnoreCase))
        {
            button.Click += async (_, _) =>
            {
                await _wirelessNetworkService.ConnectToNetworkAsync(network.Ssid);
                await LoadWifiNetworksAsync();
            };
        }
        else
        {
            ToolTip.SetTip(button, "A password is required to join this network.");
        }

        return button;
    }

    private static bool IsDemoWifiEnabled() =>
        string.Equals(Environment.GetEnvironmentVariable("DAISYOS_MOCK_WIFI"), "1", StringComparison.Ordinal);

    private WirelessNetworkStatus CreateDemoWifiStatus()
    {
        var networks = new[]
        {
            new WirelessNetworkInfo("DottOS Guest", 92, "WPA2", _demoConnectedWifi == "DottOS Guest"),
            new WirelessNetworkInfo("Hemppu 5G", 68, "WPA2", _demoConnectedWifi == "Hemppu 5G"),
            new WirelessNetworkInfo("Coffee Shop Wi-Fi", 43, "Open", _demoConnectedWifi == "Coffee Shop Wi-Fi"),
            new WirelessNetworkInfo("Neighbourhood Wi-Fi", 16, "WPA2", _demoConnectedWifi == "Neighbourhood Wi-Fi"),
        };

        return new WirelessNetworkStatus(true, networks, "Demo Wi-Fi networks are enabled.");
    }

    private async Task LoadBluetoothDevicesAsync()
    {
        var host = this.FindControl<StackPanel>("BluetoothDevicesHost");
        if (host is null) return;

        host.Children.Clear();
        host.Children.Add(CreateStatusText("Loading devices…"));

        BluetoothStatus status;
        try
        {
            status = IsDemoBluetoothEnabled()
                ? CreateDemoBluetoothStatus()
                : await _bluetoothService.GetStatusAsync();
        }
        catch
        {
            status = new BluetoothStatus(false, false, false, Array.Empty<BluetoothDevice>(), "Bluetooth devices could not be loaded.");
        }

        host.Children.Clear();
        if (!status.IsAvailable || !status.IsEnabled || status.Devices.Count == 0)
        {
            host.Children.Add(CreateStatusText(status.Detail));
            return;
        }

        foreach (var device in status.Devices.OrderByDescending(device => device.IsConnected).ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            host.Children.Add(CreateBluetoothDeviceButton(device));
        }
    }

    private Button CreateBluetoothDeviceButton(BluetoothDevice device)
    {
        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("18,10,*"),
            RowDefinitions = new RowDefinitions(device.IsConnected ? "20,16" : "36"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var icon = new TextBlock
        {
            Text = BluetoothIconFor(device),
            FontFamily = new FontFamily("avares://DaisyOS.Shell/Assets/fonts#Material Symbols Rounded"),
            FontSize = 18,
            VerticalAlignment = VerticalAlignment.Center,
        };
        content.Children.Add(icon);
        Grid.SetRowSpan(icon, device.IsConnected ? 2 : 1);

        var name = new TextBlock
        {
            Text = device.Name,
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(name, 2);
        content.Children.Add(name);

        if (device.IsConnected)
        {
            var detail = new TextBlock
            {
                Text = "Connected",
                FontSize = 11,
                Foreground = this.FindResource("TextSecondaryBrush") as IBrush ?? Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(detail, 1);
            Grid.SetColumn(detail, 2);
            content.Children.Add(detail);
        }

        var button = CreateQuickSettingsListButton(content, false);
        button.Classes.Set("ConnectedBluetoothDevice", device.IsConnected);
        button.Click += async (_, _) =>
        {
            if (!await _bluetoothService.SetConnectedAsync(device.Address, !device.IsConnected))
            {
                ToolTip.SetTip(button, "DaisyOS could not change this device connection.");
                ReportFailure("DaisyOS could not change that Bluetooth connection.");
            }
            await LoadBluetoothDevicesAsync();
        };
        return button;
    }

    private static bool IsDemoBluetoothEnabled() =>
        string.Equals(Environment.GetEnvironmentVariable("DAISYOS_MOCK_BLUETOOTH"), "1", StringComparison.Ordinal);

    private static BluetoothStatus CreateDemoBluetoothStatus() =>
        new(
            IsAvailable: true,
            IsEnabled: true,
            IsDiscovering: false,
            Devices:
            [
                new BluetoothDevice("10:24:98:7A:3B:01", "DottBuds Pro", true, true, "Audio"),
                new BluetoothDevice("10:24:98:7A:3B:02", "MX Master 3S", false, true, "Mouse"),
                new BluetoothDevice("10:24:98:7A:3B:03", "Keychron K3", false, true, "Keyboard"),
                new BluetoothDevice("10:24:98:7A:3B:04", "DualSense Wireless Controller", false, true, "Controller"),
            ],
            Detail: "Demo Bluetooth devices are enabled.");

    private static string BluetoothIconFor(BluetoothDevice device)
    {
        var type = device.Type;
        var name = device.Name;
        if (Contains(type, "audio") || Contains(type, "headphone") || Contains(type, "headset") || Contains(name, "bud")) return "headphones";
        if (Contains(type, "keyboard") || Contains(name, "keyboard") || Contains(name, "keychron")) return "keyboard";
        if (Contains(type, "mouse") || Contains(name, "mouse") || Contains(name, "master")) return "mouse";
        if (Contains(type, "phone") || Contains(name, "phone")) return "smartphone";
        if (Contains(type, "controller") || Contains(type, "gamepad") || Contains(name, "controller") || Contains(name, "dualsense")) return "sports_esports";
        return "bluetooth";
    }

    private static bool Contains(string value, string fragment) =>
        value.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    private static Button CreateQuickSettingsListButton(Control content, bool isSelected)
    {
        var button = new Button { Content = content };
        button.Classes.Add("ShellButton");
        button.Classes.Add("OutputDeviceItem");
        if (isSelected) button.Classes.Add("SelectedOutputDevice");
        return button;
    }

    private TextBlock CreateStatusText(string text) => new()
    {
        Text = text,
        Foreground = this.FindResource("TextSecondaryBrush") as IBrush,
        Margin = new Avalonia.Thickness(10, 8),
        TextWrapping = TextWrapping.Wrap,
    };

    private static void ReportFailure(string message) => (Application.Current as App)?.Feedback.Show(message);

    private enum QuickSettingsPage
    {
        Main,
        OutputDevices,
        WifiNetworks,
        BluetoothDevices,
    }

}
