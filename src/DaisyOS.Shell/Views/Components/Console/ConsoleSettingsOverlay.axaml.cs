using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.System.Audio;
using DaisyOS.System.Networking;
using DaisyOS.System.Processes;

namespace DaisyOS.Shell.Views.Components.Console;

public partial class ConsoleSettingsOverlay : UserControl
{
    private readonly IAudioService _audioService;
    private readonly INetworkStatusService _networkStatusService;
    private readonly IWirelessNetworkService _wirelessNetworkService;

    private bool _isMicMuted;
    private double _micVolume = 75;
    private CancellationTokenSource? _loadCts;

    public event EventHandler? Closed;
    public event EventHandler? ReturnToDesktopRequested;
    public event EventHandler<bool>? PerfOverlayToggled;

    public ConsoleSettingsOverlay()
        : this(
            new LinuxAudioService(new SafeCommandRunner()),
            new LinuxNetworkStatusService(new SafeCommandRunner()),
            new LinuxWirelessNetworkService(new SafeCommandRunner()))
    {
    }

    public ConsoleSettingsOverlay(
        IAudioService audioService,
        INetworkStatusService networkStatusService,
        IWirelessNetworkService wirelessNetworkService)
    {
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        _networkStatusService = networkStatusService ?? throw new ArgumentNullException(nameof(networkStatusService));
        _wirelessNetworkService = wirelessNetworkService ?? throw new ArgumentNullException(nameof(wirelessNetworkService));

        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void ShowOverlay(string initialTab = "Audio")
    {
        IsVisible = true;
        SelectTab(initialTab);
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        _ = LoadSettingsDataAsync(_loadCts.Token);
    }

    public void HideOverlay()
    {
        IsVisible = false;
        _loadCts?.Cancel();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _loadCts = new CancellationTokenSource();
        _ = LoadSettingsDataAsync(_loadCts.Token);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _loadCts?.Cancel();
    }

    private async Task LoadSettingsDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 1. Audio Data
            var volume = await _audioService.GetVolumeAsync(cancellationToken).ConfigureAwait(true);
            if (volume.HasValue)
            {
                var volPercent = (int)Math.Round(volume.Value);
                MasterVolumeSlider.Value = volPercent;
                VolumeValueText.Text = $"{volPercent}%";
            }

            var devices = await _audioService.GetAudioDevicesAsync(cancellationToken).ConfigureAwait(true);
            OutputDeviceCombo.ItemsSource = devices.Select(d => d.Name).ToList();
            if (devices.Count > 0)
            {
                var defaultOutput = devices.FirstOrDefault(d => d.IsDefault) ?? devices[0];
                OutputDeviceCombo.SelectedItem = defaultOutput.Name;
            }

            InputDeviceCombo.ItemsSource = new List<string> { "Built-in Microphone", "Headset Microphone", "USB Audio Input" };
            InputDeviceCombo.SelectedIndex = 0;

            // 2. Network Status
            var netStatus = await _networkStatusService.GetStatusAsync(cancellationToken).ConfigureAwait(true);
            if (netStatus.IsConnected)
            {
                NetworkStatusText.Text = $"Connected to {netStatus.DisplayName} ({netStatus.Detail})";
            }
            else
            {
                NetworkStatusText.Text = "Not connected";
            }

            await RefreshWifiNetworksAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load console settings: {ex.Message}");
        }
    }

    private async Task RefreshWifiNetworksAsync(CancellationToken cancellationToken)
    {
        try
        {
            var wifiStatus = await _wirelessNetworkService.GetNetworksAsync(cancellationToken).ConfigureAwait(true);
            WifiToggleSwitch.IsChecked = wifiStatus.IsAvailable;

            WifiListStack.Children.Clear();
            if (wifiStatus.Networks.Count == 0)
            {
                WifiListStack.Children.Add(new TextBlock
                {
                    Text = "No Wi-Fi networks found",
                    FontSize = 13,
                    Foreground = GetThemeBrush("TextSecondaryBrush", Brushes.Gray)
                });
                return;
            }

            foreach (var net in wifiStatus.Networks.Take(5))
            {
                var border = new Border
                {
                    Padding = new Thickness(12, 10),
                    CornerRadius = new CornerRadius(10),
                    Background = GetThemeBrush("SubtleSurfaceBrush", Brushes.DarkGray)
                };

                var grid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto")
                };

                var infoStack = new StackPanel { Spacing = 2 };
                infoStack.Children.Add(new TextBlock
                {
                    Text = net.Ssid,
                    FontSize = 14,
                    FontWeight = FontWeight.SemiBold
                });
                infoStack.Children.Add(new TextBlock
                {
                    Text = net.IsActive ? "Connected" : $"Signal: {net.SignalPercent}%",
                    FontSize = 12,
                    Foreground = GetThemeBrush("TextSecondaryBrush", Brushes.Gray)
                });

                var connectBtn = new Button
                {
                    Content = net.IsActive ? "Connected" : "Connect",
                    IsEnabled = !net.IsActive,
                    Classes = { "ConsoleActionBtn" },
                    Height = 32,
                    Padding = new Thickness(12, 0)
                };

                grid.Children.Add(infoStack);
                grid.Children.Add(connectBtn);
                Grid.SetColumn(connectBtn, 1);

                border.Child = grid;
                WifiListStack.Children.Add(border);
            }
        }
        catch
        {
            // Best effort
        }
    }

    private void OnTabClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tabName)
        {
            SelectTab(tabName);
        }
    }

    private void SelectTab(string tabName)
    {
        AudioPanel.IsVisible = tabName == "Audio";
        NetworkPanel.IsVisible = tabName == "Network";
        SystemPanel.IsVisible = tabName == "System";

        TabAudioBtn.Classes.Set("Active", tabName == "Audio");
        TabNetworkBtn.Classes.Set("Active", tabName == "Network");
        TabSystemBtn.Classes.Set("Active", tabName == "System");
    }

    private async void OnMasterVolumeChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        var volPercent = (int)Math.Round(e.NewValue);
        VolumeValueText.Text = $"{volPercent}%";
        try
        {
            await _audioService.SetVolumeAsync(e.NewValue).ConfigureAwait(true);
        }
        catch
        {
            // Best effort
        }
    }

    private void OnMicVolumeChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _micVolume = Math.Round(e.NewValue);
        MicVolumeText.Text = $"{_micVolume}%";
    }

    private void OnMuteMicClicked(object? sender, RoutedEventArgs e)
    {
        _isMicMuted = !_isMicMuted;
        if (_isMicMuted)
        {
            MuteMicIcon.Text = "mic_off";
            MuteMicText.Text = "Unmute Mic";
            MuteMicBtn.Classes.Add("Active");
        }
        else
        {
            MuteMicIcon.Text = "mic";
            MuteMicText.Text = "Mute Mic";
            MuteMicBtn.Classes.Remove("Active");
        }
    }

    private async void OnOutputDeviceSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (OutputDeviceCombo.SelectedItem is string deviceName)
        {
            try
            {
                var devices = await _audioService.GetAudioDevicesAsync().ConfigureAwait(true);
                var match = devices.FirstOrDefault(d => d.Name == deviceName);
                if (match != null)
                {
                    await _audioService.SetDefaultAudioDeviceAsync(match.Id).ConfigureAwait(true);
                }
            }
            catch
            {
                // Best effort
            }
        }
    }

    private void OnInputDeviceSelected(object? sender, SelectionChangedEventArgs e)
    {
        // Recorded for input audio routing
    }

    private async void OnWifiToggled(object? sender, RoutedEventArgs e)
    {
        if (WifiToggleSwitch.IsChecked.HasValue)
        {
            try
            {
                await _wirelessNetworkService.SetEnabledAsync(WifiToggleSwitch.IsChecked.Value).ConfigureAwait(true);
            }
            catch
            {
                // Best effort
            }
        }
    }

    private async void OnRefreshWifiClicked(object? sender, RoutedEventArgs e)
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        await RefreshWifiNetworksAsync(_loadCts.Token).ConfigureAwait(true);
    }

    private void OnPerfOverlayToggled(object? sender, RoutedEventArgs e)
    {
        PerfOverlayToggled?.Invoke(this, PerfOverlayToggle.IsChecked ?? false);
    }

    private void OnReturnToDesktopClicked(object? sender, RoutedEventArgs e)
    {
        HideOverlay();
        ReturnToDesktopRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnRestartClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start("systemctl", "reboot");
        }
        catch
        {
            // Best effort
        }
    }

    private void OnShutdownClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start("systemctl", "poweroff");
        }
        catch
        {
            // Best effort
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        HideOverlay();
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HideOverlay();
    }

    private static IBrush GetThemeBrush(string key, IBrush fallback)
    {
        if (Application.Current?.TryFindResource(key, out var resource) == true && resource is IBrush brush)
        {
            return brush;
        }
        return fallback;
    }
}
