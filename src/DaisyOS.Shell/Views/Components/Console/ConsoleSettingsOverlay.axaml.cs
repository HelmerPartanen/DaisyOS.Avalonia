using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.Shell;
using DaisyOS.Shell.Controls;
using DaisyOS.Shell.Services;
using DaisyOS.System.Audio;
using DaisyOS.System.Bluetooth;
using DaisyOS.System.Controllers;
using DaisyOS.System.Networking;
using DaisyOS.System.Processes;

namespace DaisyOS.Shell.Views.Components.Console;

/// <summary>
/// Controller-first presentation of the shell's shared Quick Settings controls. It intentionally
/// exposes only actions with a live service path; full configuration remains in desktop Settings.
/// </summary>
public partial class ConsoleSettingsOverlay : UserControl
{
    private readonly IAudioService _audioService;
    private readonly IWirelessNetworkService _wirelessNetworkService;
    private readonly IBluetoothService _bluetoothService;
    private readonly ShellSessionState _sessionState;
    private readonly List<Button> _detailActions = [];
    private CancellationTokenSource? _loadCts;
    private int _focusIndex;
    private int _detailFocusIndex;
    private bool _isRefreshing;

    public event EventHandler? Closed;
    public event EventHandler? ReturnToDesktopRequested;

    public ConsoleSettingsOverlay()
        : this(
            new LinuxAudioService(new SafeCommandRunner()),
            new LinuxWirelessNetworkService(new SafeCommandRunner()),
            new LinuxBluetoothService(new SafeCommandRunner()),
            (Application.Current as App)?.SessionState ?? new ShellSessionState())
    {
    }

    public ConsoleSettingsOverlay(
        IAudioService audioService,
        IWirelessNetworkService wirelessNetworkService,
        IBluetoothService bluetoothService,
        ShellSessionState sessionState)
    {
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        _wirelessNetworkService = wirelessNetworkService ?? throw new ArgumentNullException(nameof(wirelessNetworkService));
        _bluetoothService = bluetoothService ?? throw new ArgumentNullException(nameof(bluetoothService));
        _sessionState = sessionState ?? throw new ArgumentNullException(nameof(sessionState));

        InitializeComponent();
        Unloaded += (_, _) => CancelLoad();
    }

    public void ShowOverlay(string initialTab = "Quick settings")
    {
        IsVisible = true;
        CloseDetails();
        _focusIndex = 0;
        CancelLoad();
        _loadCts = new CancellationTokenSource();
        _ = RefreshStateAsync(_loadCts.Token);
        Dispatcher.UIThread.Post(FocusCurrentTarget, DispatcherPriority.Input);
    }

    public void SetControllerLayout(string? controllerName)
    {
        var playStation = !string.IsNullOrWhiteSpace(controllerName) &&
            (controllerName.Contains("sony", StringComparison.OrdinalIgnoreCase)
                || controllerName.Contains("dualshock", StringComparison.OrdinalIgnoreCase)
                || controllerName.Contains("dualsense", StringComparison.OrdinalIgnoreCase)
                || controllerName.Contains("playstation", StringComparison.OrdinalIgnoreCase));
        ControllerHintText.Text = playStation
            ? "D-pad  Navigate   ✕  Select   △  Details   ○  Back"
            : "D-pad  Navigate   A  Select   Y  Details   B  Back";
        DoneButtonText.Text = playStation ? "○  Back to console" : "B  Back to console";
    }

    public void HideOverlay()
    {
        if (!IsVisible)
        {
            return;
        }

        IsVisible = false;
        CancelLoad();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    public void Navigate(ControllerNavigationAction action)
    {
        if (!IsVisible)
        {
            return;
        }

        if (DetailsCard.IsVisible)
        {
            NavigateDetails(action);
            return;
        }

        if (action is ControllerNavigationAction.Back or ControllerNavigationAction.QuickSettings)
        {
            HideOverlay();
            return;
        }

        if (action == ControllerNavigationAction.Details)
        {
            OpenDetailsForCurrentTarget();
            return;
        }

        if (action == ControllerNavigationAction.PreviousSection)
        {
            _focusIndex = Math.Max(0, _focusIndex - 1);
            FocusCurrentTarget();
            return;
        }

        if (action == ControllerNavigationAction.NextSection)
        {
            _focusIndex = Math.Min(7, _focusIndex + 1);
            FocusCurrentTarget();
            return;
        }

        if (action is ControllerNavigationAction.Left or ControllerNavigationAction.Right or ControllerNavigationAction.Up or ControllerNavigationAction.Down)
        {
            MoveControllerFocus(action);
            return;
        }

        if (action == ControllerNavigationAction.Confirm)
        {
            ActivateCurrentTarget();
        }
    }

    private void NavigateDetails(ControllerNavigationAction action)
    {
        if (action is ControllerNavigationAction.Back or ControllerNavigationAction.QuickSettings)
        {
            CloseDetails();
            FocusCurrentTarget();
            return;
        }

        if (_detailActions.Count == 0)
        {
            return;
        }

        if (action == ControllerNavigationAction.Details)
        {
            return;
        }

        if (action is ControllerNavigationAction.Left or ControllerNavigationAction.Up or ControllerNavigationAction.PreviousSection)
        {
            _detailFocusIndex = Math.Max(0, _detailFocusIndex - 1);
            _detailActions[_detailFocusIndex].Focus();
            return;
        }

        if (action is ControllerNavigationAction.Right or ControllerNavigationAction.Down or ControllerNavigationAction.NextSection)
        {
            _detailFocusIndex = Math.Min(_detailActions.Count - 1, _detailFocusIndex + 1);
            _detailActions[_detailFocusIndex].Focus();
            return;
        }

        if (action == ControllerNavigationAction.Confirm)
        {
            _detailActions[_detailFocusIndex].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
    }

    private void FocusCurrentTarget()
    {
        switch (_focusIndex)
        {
            case 0: WifiTile.FocusPrimary(); break;
            case 1: BluetoothTile.FocusPrimary(); break;
            case 2: FocusTile.FocusPrimary(); break;
            case 3: GamingTile.FocusPrimary(); break;
            case 4: VolumeSlider.Focus(); break;
            case 5: OutputDetailsButton.Focus(); break;
            case 6: DesktopModeButton.Focus(); break;
            default: DoneButton.Focus(); break;
        }
    }

    private void ActivateCurrentTarget()
    {
        switch (_focusIndex)
        {
            case 0: WifiTile.ActivatePrimary(); break;
            case 1: BluetoothTile.ActivatePrimary(); break;
            case 2: FocusTile.ActivatePrimary(); break;
            case 3: GamingTile.ActivatePrimary(); break;
            case 4: VolumeSlider.Value = Math.Min(VolumeSlider.Maximum, VolumeSlider.Value + 5); break;
            case 5: _ = ShowOutputDevicesAsync(); break;
            case 6: RequestDesktopMode(); break;
            default: HideOverlay(); break;
        }
    }

    private void MoveControllerFocus(ControllerNavigationAction action)
    {
        _focusIndex = action switch
        {
            ControllerNavigationAction.Left when _focusIndex is 1 or 2 => _focusIndex - 1,
            ControllerNavigationAction.Left when _focusIndex == 5 => 4,
            ControllerNavigationAction.Right when _focusIndex is 0 or 1 => _focusIndex + 1,
            ControllerNavigationAction.Right when _focusIndex == 4 => 5,
            ControllerNavigationAction.Down when _focusIndex <= 2 => 3,
            ControllerNavigationAction.Down when _focusIndex == 3 => 4,
            ControllerNavigationAction.Down when _focusIndex is 4 or 5 => 6,
            ControllerNavigationAction.Down when _focusIndex == 6 => 7,
            ControllerNavigationAction.Up when _focusIndex == 3 => 0,
            ControllerNavigationAction.Up when _focusIndex is 4 or 5 => 3,
            ControllerNavigationAction.Up when _focusIndex == 6 => 4,
            ControllerNavigationAction.Up when _focusIndex == 7 => 6,
            _ => _focusIndex
        };
        FocusCurrentTarget();
    }

    private void OpenDetailsForCurrentTarget()
    {
        if (_focusIndex == 0)
        {
            _ = ShowWifiNetworksAsync();
        }
        else if (_focusIndex is 4 or 5)
        {
            _ = ShowOutputDevicesAsync();
        }
    }

    private async Task RefreshStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            _isRefreshing = true;
            var wifiTask = _wirelessNetworkService.GetRadioStatusAsync(cancellationToken);
            var bluetoothTask = _bluetoothService.GetAdapterStatusAsync(cancellationToken);
            var volumeTask = _audioService.GetVolumeAsync(cancellationToken);
            await Task.WhenAll(wifiTask, bluetoothTask, volumeTask);
            if (cancellationToken.IsCancellationRequested || !IsVisible) return;

            var wifi = await wifiTask;
            var bluetooth = await bluetoothTask;
            WifiTile.IsEnabled = wifi.IsAvailable;
            WifiTile.IsChecked = wifi.IsEnabled;
            WifiTile.ToolTipText = wifi.Detail;
            BluetoothTile.IsEnabled = bluetooth.IsAvailable;
            BluetoothTile.IsChecked = bluetooth.IsEnabled;
            BluetoothTile.ToolTipText = bluetooth.Detail;
            FocusTile.IsChecked = _sessionState.DoNotDisturb;
            GamingTile.IsChecked = _sessionState.GamingMode;
            VolumeSlider.IsEnabled = volumeTask.Result is not null;
            VolumeSlider.Value = volumeTask.Result ?? _sessionState.LastKnownVolume ?? 0;
        }
        catch (OperationCanceledException)
        {
            // Closing the panel cancels its deferred hardware probes.
        }
        catch
        {
            VolumeSlider.IsEnabled = false;
            ReportFailure("DaisyOS could not refresh Quick Settings.");
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async void OnWifiTileToggled(object? sender, RoutedEventArgs e)
    {
        if (sender is not QuickSettingTile tile || _isRefreshing) return;
        if (!await _wirelessNetworkService.SetEnabledAsync(tile.IsChecked))
        {
            tile.IsChecked = !tile.IsChecked;
            ReportFailure("DaisyOS could not change Wi-Fi. Check that NetworkManager is available.");
        }
    }

    private async void OnBluetoothTileToggled(object? sender, RoutedEventArgs e)
    {
        if (sender is not QuickSettingTile tile || _isRefreshing) return;
        if (!await _bluetoothService.SetEnabledAsync(tile.IsChecked))
        {
            tile.IsChecked = !tile.IsChecked;
            ReportFailure("DaisyOS could not change Bluetooth. Check that Bluetooth is available.");
        }
    }

    private void OnFocusTileToggled(object? sender, RoutedEventArgs e)
    {
        if (sender is QuickSettingTile tile) _sessionState.DoNotDisturb = tile.IsChecked;
    }

    private void OnGamingTileToggled(object? sender, RoutedEventArgs e)
    {
        if (sender is QuickSettingTile tile) _sessionState.GamingMode = tile.IsChecked;
    }

    private async void OnVolumeChanged(object? sender, EventArgs e)
    {
        if (_isRefreshing || sender is not QuickSettingsSlider slider || !slider.IsLoaded) return;
        try
        {
            await _audioService.SetVolumeAsync(slider.Value);
            _sessionState.LastKnownVolume = slider.Value;
        }
        catch
        {
            ReportFailure("DaisyOS could not change the volume.");
        }
    }

    private async void OnWifiDetailsClicked(object? sender, RoutedEventArgs e) => await ShowWifiNetworksAsync();
    private async void OnOutputDetailsClicked(object? sender, RoutedEventArgs e) => await ShowOutputDevicesAsync();

    private async Task ShowWifiNetworksAsync()
    {
        BeginDetails("Wi-Fi networks");
        try
        {
            var status = await _wirelessNetworkService.GetNetworksAsync(_loadCts?.Token ?? default);
            if (!status.IsAvailable || status.Networks.Count == 0)
            {
                AddDetailStatus(status.Detail);
                return;
            }

            foreach (var network in status.Networks.OrderByDescending(network => network.IsActive).ThenByDescending(network => network.SignalPercent))
            {
                AddWifiAction(network);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            AddDetailStatus("Wi-Fi networks could not be loaded.");
        }
        finally
        {
            FocusFirstDetail();
        }
    }

    private async Task ShowOutputDevicesAsync()
    {
        BeginDetails("Output devices");
        try
        {
            var devices = await _audioService.GetAudioDevicesAsync(_loadCts?.Token ?? default);
            if (devices.Count == 0)
            {
                AddDetailStatus("No output devices are available.");
                return;
            }

            foreach (var device in devices.OrderByDescending(device => device.IsDefault).ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var button = CreateDetailButton(device.IsDefault ? $"{device.Name}  •  Current output" : device.Name);
                button.IsEnabled = !device.IsDefault;
                if (device.IsDefault) _detailActions.Remove(button);
                button.Click += async (_, _) =>
                {
                    if (!await _audioService.SetDefaultAudioDeviceAsync(device.Id))
                    {
                        ReportFailure("DaisyOS could not switch to that audio output.");
                    }
                    await ShowOutputDevicesAsync();
                };
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            AddDetailStatus("Output devices could not be loaded.");
        }
        finally
        {
            FocusFirstDetail();
        }
    }

    private void BeginDetails(string title)
    {
        DetailsTitle.Text = title;
        DetailsCard.IsVisible = true;
        DetailsHost.Children.Clear();
        _detailActions.Clear();
        _detailFocusIndex = 0;
    }

    private void AddWifiAction(WirelessNetworkInfo network)
    {
        var label = network.IsActive
            ? $"{network.Ssid}  •  Connected"
            : network.Security.Equals("Open", StringComparison.OrdinalIgnoreCase)
                ? $"{network.Ssid}  •  Open • {network.SignalPercent}%"
                : $"{network.Ssid}  •  Secured • {network.SignalPercent}%";
        var button = CreateDetailButton(label);

        button.Click += async (_, _) =>
        {
            var succeeded = network.IsActive
                ? await _wirelessNetworkService.DisconnectFromNetworkAsync()
                : await _wirelessNetworkService.ConnectToNetworkAsync(network.Ssid);
            if (!succeeded)
            {
                ReportFailure(network.IsActive
                    ? "DaisyOS could not disconnect from that network."
                    : network.Security.Equals("Open", StringComparison.OrdinalIgnoreCase)
                        ? "DaisyOS could not join that network."
                        : "DaisyOS could not join that network. Enter its password once in Desktop Settings, then try again.");
            }
            await ShowWifiNetworksAsync();
        };
    }

    private Button CreateDetailButton(string text)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = text,
                FontSize = 13,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            },
            Classes = { "ConsoleSettingsListItem" }
        };
        DetailsHost.Children.Add(button);
        _detailActions.Add(button);
        return button;
    }

    private void AddDetailStatus(string text)
    {
        DetailsHost.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 13,
            Foreground = this.FindResource("TextSecondaryBrush") as IBrush,
            Margin = new Thickness(12, 10)
        });
    }

    private void FocusFirstDetail()
    {
        if (_detailActions.Count > 0)
        {
            _detailFocusIndex = 0;
            Dispatcher.UIThread.Post(() => _detailActions[0].Focus(), DispatcherPriority.Input);
        }
    }

    private void CloseDetails()
    {
        DetailsCard.IsVisible = false;
        DetailsHost.Children.Clear();
        _detailActions.Clear();
    }

    private void OnDetailsCloseClicked(object? sender, RoutedEventArgs e)
    {
        CloseDetails();
        FocusCurrentTarget();
    }

    private void OnReturnToDesktopClicked(object? sender, RoutedEventArgs e) => RequestDesktopMode();

    private void RequestDesktopMode()
    {
        HideOverlay();
        ReturnToDesktopRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => HideOverlay();
    private void OnBackdropPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) => HideOverlay();

    private void CancelLoad()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
    }

    private static void ReportFailure(string message) => (Application.Current as App)?.Feedback.Show(message);
}
