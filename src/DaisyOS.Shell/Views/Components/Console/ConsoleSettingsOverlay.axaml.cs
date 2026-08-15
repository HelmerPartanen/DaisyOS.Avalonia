using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.System.Audio;
using DaisyOS.System.Controllers;
using DaisyOS.System.Networking;
using DaisyOS.System.Processes;

namespace DaisyOS.Shell.Views.Components.Console;

public partial class ConsoleSettingsOverlay : UserControl
{
    private readonly IAudioService _audioService;
    private readonly INetworkStatusService _networkStatusService;
    private readonly IWirelessNetworkService _wirelessNetworkService;
    private IControllerInputService _controllerInputService;

    private bool _isMicMuted;
    private double _micVolume = 75;
    private CancellationTokenSource? _loadCts;
    private ControllerKeybindingsConfig _keybindingsConfig = new();
    private string? _rebindingAction;
    private string? _activeControllerName;

    public event EventHandler? Closed;
    public event EventHandler? ReturnToDesktopRequested;
    public event EventHandler<bool>? PerfOverlayToggled;

    public ConsoleSettingsOverlay()
        : this(
            new LinuxAudioService(new SafeCommandRunner()),
            new LinuxNetworkStatusService(new SafeCommandRunner()),
            new LinuxWirelessNetworkService(new SafeCommandRunner()),
            new LinuxControllerInputService())
    {
    }

    public ConsoleSettingsOverlay(
        IAudioService audioService,
        INetworkStatusService networkStatusService,
        IWirelessNetworkService wirelessNetworkService)
        : this(audioService, networkStatusService, wirelessNetworkService, new LinuxControllerInputService())
    {
    }

    public ConsoleSettingsOverlay(
        IAudioService audioService,
        INetworkStatusService networkStatusService,
        IWirelessNetworkService wirelessNetworkService,
        IControllerInputService controllerInputService)
    {
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        _networkStatusService = networkStatusService ?? throw new ArgumentNullException(nameof(networkStatusService));
        _wirelessNetworkService = wirelessNetworkService ?? throw new ArgumentNullException(nameof(wirelessNetworkService));
        _controllerInputService = controllerInputService ?? throw new ArgumentNullException(nameof(controllerInputService));

        InitializeComponent();
        _controllerInputService.RawInputReceived += OnRawControllerInputReceived;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void SetInputService(IControllerInputService controllerInputService)
    {
        if (_controllerInputService != null)
        {
            _controllerInputService.RawInputReceived -= OnRawControllerInputReceived;
        }

        _controllerInputService = controllerInputService ?? throw new ArgumentNullException(nameof(controllerInputService));
        _controllerInputService.RawInputReceived += OnRawControllerInputReceived;
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
        _rebindingAction = null;
        if (_controllerInputService != null) _controllerInputService.IsRebinding = false;
        _loadCts?.Cancel();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    public void Navigate(ControllerNavigationAction action)
    {
        if (_controllerInputService != null && _controllerInputService.IsRebinding)
        {
            return;
        }

        if (action is ControllerNavigationAction.Back or ControllerNavigationAction.QuickSettings)
        {
            HideOverlay();
        }
    }

    public void SetControllerLayout(string? name)
    {
        _activeControllerName = name;
        var type = ControllerConnectionStatus.DetectType(name);
        var isPlayStation = type == ControllerType.PlayStation;
        var prefix = isPlayStation
            ? "avares://DaisyOS.Shell/Assets/PlaystationController/"
            : "avares://DaisyOS.Shell/Assets/XboxController/";

        try
        {
            if (HintDpadImage != null)
                HintDpadImage.Source = new Bitmap(AssetLoader.Open(new Uri(prefix + (isPlayStation ? "plain-bottom.png" : "dpad.png"))));

            if (HintConfirmImage != null)
                HintConfirmImage.Source = new Bitmap(AssetLoader.Open(new Uri(prefix + (isPlayStation ? "outline-blue-cross.png" : "a-filled-green.png"))));

            if (HintDetailsImage != null)
                HintDetailsImage.Source = new Bitmap(AssetLoader.Open(new Uri(prefix + (isPlayStation ? "outline-green-triangle.png" : "y-filled-yellow.png"))));

            if (HintBackImage != null)
                HintBackImage.Source = new Bitmap(AssetLoader.Open(new Uri(prefix + (isPlayStation ? "outline-red-circle.png" : "b-filled red.png"))));
        }
        catch
        {
            // Graceful fallback
        }
    }

    public void SetControllerLayout(ControllerConnectionStatus status)
    {
        SetControllerLayout(status?.Name);
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

                var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };

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
        ControlsPanel.IsVisible = tabName == "Controls";
        SystemPanel.IsVisible = tabName == "System";

        TabAudioBtn.Classes.Set("Active", tabName == "Audio");
        TabNetworkBtn.Classes.Set("Active", tabName == "Network");
        TabControlsBtn.Classes.Set("Active", tabName == "Controls");
        TabSystemBtn.Classes.Set("Active", tabName == "System");

        if (tabName == "Controls")
        {
            BuildKeybindingsListUI();
        }
    }

    private void LoadKeybindingsConfig()
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var configFile = Path.Combine(home, ".config", "daisyos", "controller_keybindings.json");
            if (File.Exists(configFile))
            {
                var json = File.ReadAllText(configFile);
                var loaded = JsonSerializer.Deserialize<ControllerKeybindingsConfig>(json);
                if (loaded != null) _keybindingsConfig = loaded;
            }
        }
        catch
        {
            _keybindingsConfig = new ControllerKeybindingsConfig();
        }
    }

    private void SaveKeybindingsConfig()
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var configDir = Path.Combine(home, ".config", "daisyos");
            Directory.CreateDirectory(configDir);
            var configFile = Path.Combine(configDir, "controller_keybindings.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_keybindingsConfig, options);
            File.WriteAllText(configFile, json);

            _controllerInputService?.ReloadKeybindings();
        }
        catch
        {
            // Best effort
        }
    }

    public static string GetButtonLabel(byte buttonNumber, ControllerType type)
    {
        if (type == ControllerType.PlayStation)
        {
            return buttonNumber switch
            {
                0 => "Cross (✕)",
                1 => "Circle (○)",
                2 => "Triangle (△)",
                3 => "Square (▫)",
                4 => "L1",
                5 => "R1",
                6 => "L2",
                7 => "R2",
                8 => "Share",
                9 => "Options",
                10 => "PS Button",
                11 => "L3",
                12 => "R3",
                13 => "Touchpad",
                _ => $"Button {buttonNumber}"
            };
        }

        return buttonNumber switch
        {
            0 => "A",
            1 => "B",
            2 => "X",
            3 => "Y",
            4 => "LB",
            5 => "RB",
            6 => "View / Select",
            7 => "Menu / Start",
            8 => "Guide / Xbox",
            9 => "LS",
            10 => "RS",
            _ => $"Button {buttonNumber}"
        };
    }

    private void BuildKeybindingsListUI()
    {
        LoadKeybindingsConfig();
        KeybindingsListStack.Children.Clear();

        var type = ControllerConnectionStatus.DetectType(_activeControllerName);

        var actions = new (string Action, string Label, string DefaultDesc)[]
        {
            ("Confirm", "Select / Launch Game", type == ControllerType.PlayStation ? "Cross (✕)" : "A"),
            ("Back", "Back / Cancel Overlay", type == ControllerType.PlayStation ? "Circle (○)" : "B"),
            ("QuickSettings", "Toggle Quick Settings", type == ControllerType.PlayStation ? "Square (▫)" : "X"),
            ("Details", "Show Details & Info", type == ControllerType.PlayStation ? "Triangle (△)" : "Y"),
            ("PreviousSection", "Switch Tab Left", type == ControllerType.PlayStation ? "L1" : "LB"),
            ("NextSection", "Switch Tab Right", type == ControllerType.PlayStation ? "R1" : "RB"),
            ("OpenConsole", "Toggle Console Mode", type == ControllerType.PlayStation ? "PS Button" : "Guide / Xbox")
        };

        foreach (var act in actions)
        {
            var border = new Border
            {
                Padding = new Thickness(12, 10),
                CornerRadius = new CornerRadius(10),
                Background = GetThemeBrush("SubtleSurfaceBrush", Brushes.DarkGray)
            };

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };

            var labelStack = new StackPanel { Spacing = 2 };
            labelStack.Children.Add(new TextBlock { Text = act.Action, FontSize = 14, FontWeight = FontWeight.Bold });
            labelStack.Children.Add(new TextBlock
            {
                Text = act.Label,
                FontSize = 12,
                Foreground = GetThemeBrush("TextSecondaryBrush", Brushes.Gray)
            });

            var boundText = GetBoundButtonText(act.Action) ?? act.DefaultDesc;

            var rebindBtn = new Button
            {
                Content = _rebindingAction == act.Action ? "PRESS BUTTON..." : $"Remap ({boundText})",
                Classes = { "ConsoleActionBtn" },
                Height = 32,
                Padding = new Thickness(12, 0)
            };

            var actionName = act.Action;
            rebindBtn.Click += (s, e) =>
            {
                _rebindingAction = actionName;
                if (_controllerInputService != null) _controllerInputService.IsRebinding = true;
                rebindBtn.Content = "PRESS BUTTON...";
                LiveTestFeedText.Text = $"Listening... Press any button on your controller to bind to '{actionName}'.";
            };

            grid.Children.Add(labelStack);
            grid.Children.Add(rebindBtn);
            Grid.SetColumn(rebindBtn, 1);

            border.Child = grid;
            KeybindingsListStack.Children.Add(border);
        }
    }

    private string? GetBoundButtonText(string actionName)
    {
        var type = ControllerConnectionStatus.DetectType(_activeControllerName);
        var buttonMap = type == ControllerType.PlayStation ? _keybindingsConfig.PlayStationButtons : _keybindingsConfig.XboxButtons;

        foreach (var kvp in buttonMap)
        {
            if (string.Equals(kvp.Value, actionName, StringComparison.OrdinalIgnoreCase))
            {
                return GetButtonLabel((byte)kvp.Key, type);
            }
        }
        return null;
    }

    private void OnRawControllerInputReceived(object? sender, RawControllerInputEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (e.EventType == 1 && e.Value == 1) // Button Pressed
            {
                var type = ControllerConnectionStatus.DetectType(_activeControllerName);
                var buttonLabel = GetButtonLabel(e.Number, type);
                var mapped = GetActionForButton(e.Number, type);

                if (LiveTestFeedText != null)
                {
                    LiveTestFeedText.Text = $"[LIVE FEED] {buttonLabel} Pressed! -> Mapped Action: {mapped}";
                }

                if (!string.IsNullOrEmpty(_rebindingAction))
                {
                    var targetAction = _rebindingAction;
                    if (type == ControllerType.PlayStation)
                    {
                        _keybindingsConfig.PlayStationButtons[e.Number] = targetAction;
                    }
                    else
                    {
                        _keybindingsConfig.XboxButtons[e.Number] = targetAction;
                    }
                    _keybindingsConfig.EvdevKeys[e.EvdevCode != 0 ? e.EvdevCode : (ushort)(0x130 + e.Number)] = targetAction;

                    SaveKeybindingsConfig();
                    _rebindingAction = null;
                    if (_controllerInputService != null) _controllerInputService.IsRebinding = false;
                    BuildKeybindingsListUI();

                    if (LiveTestFeedText != null)
                    {
                        LiveTestFeedText.Text = $"Successfully bound {buttonLabel} to '{targetAction}'!";
                    }
                }
            }
        });
    }

    private string GetActionForButton(byte buttonNumber, ControllerType type)
    {
        var buttonMap = type == ControllerType.PlayStation ? _keybindingsConfig.PlayStationButtons : _keybindingsConfig.XboxButtons;
        if (buttonMap.TryGetValue(buttonNumber, out var act)) return act;
        return "Unmapped";
    }

    private void OnResetKeybindingsClicked(object? sender, RoutedEventArgs e)
    {
        _keybindingsConfig = new ControllerKeybindingsConfig();
        SaveKeybindingsConfig();
        BuildKeybindingsListUI();
        if (LiveTestFeedText != null)
        {
            LiveTestFeedText.Text = "Reset keybindings to factory defaults!";
        }
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
