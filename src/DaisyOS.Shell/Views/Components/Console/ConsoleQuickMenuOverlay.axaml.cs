using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.Core.Services.Gaming;
using DaisyOS.Shell.Controls;
using DaisyOS.System.Audio;
using DaisyOS.System.Diagnostics;
using DaisyOS.System.Gaming;
using DaisyOS.System.Power;
using DaisyOS.System.Processes;
using DaisyOS.System.Session;

namespace DaisyOS.Shell.Views.Components.Console;

public partial class ConsoleQuickMenuOverlay : UserControl
{
    private readonly IAudioService? _audioService;
    private readonly IPowerService? _powerService;
    private readonly IUserSessionService? _userSessionService;
    private readonly IGameDiscoveryService? _gameDiscoveryService;
    private readonly ISystemMetricsService? _systemMetricsService;

    public event EventHandler? Closed;
    public event EventHandler? GoHomeRequested;
    public event EventHandler? ReturnToDesktopRequested;
    public event EventHandler? RestartRequested;
    public event EventHandler? ShutdownRequested;

    private int _activeIconIndex = 0;
    private bool _isCardFocused = false;
    private bool _isMicMuted = false;
    private CancellationTokenSource? _loadCts;

    private readonly string[] _quickCategories = new[]
    {
        "Home", "Switcher", "Notifications", "Sound", "Controller", "Power"
    };

    public ConsoleQuickMenuOverlay()
        : this(
            new LinuxAudioService(new SafeCommandRunner()),
            new LinuxPowerService(new SafeCommandRunner()),
            new LinuxUserSessionService(),
            new LinuxGameDiscoveryService(),
            new LinuxSystemMetricsService())
    {
    }

    public ConsoleQuickMenuOverlay(
        IAudioService audioService,
        IPowerService powerService,
        IUserSessionService userSessionService,
        IGameDiscoveryService gameDiscoveryService,
        ISystemMetricsService systemMetricsService)
    {
        _audioService = audioService;
        _powerService = powerService;
        _userSessionService = userSessionService;
        _gameDiscoveryService = gameDiscoveryService;
        _systemMetricsService = systemMetricsService;
        
        InitializeComponent();
        UpdateControllerHintIcons("DualSense Wireless Controller");
    }

    public void ShowQuickMenu()
    {
        IsVisible = true;
        Opacity = 1;
        IsHitTestVisible = true;

        SelectCategory(_quickCategories[_activeIconIndex]);
        UpdateControllerHintIcons("DualSense Wireless Controller");

        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        _ = LoadDataAsync(_loadCts.Token);

        Dispatcher.UIThread.Post(() =>
        {
            FocusCurrentCategoryButton();
        }, DispatcherPriority.Input);
    }

    private async Task LoadDataAsync(CancellationToken token)
    {
        try
        {
            if (_systemMetricsService != null)
            {
                var metrics = await _systemMetricsService.GetStatusAsync(token);
                Dispatcher.UIThread.Post(() =>
                {
                    if (SystemStatusDescription != null)
                    {
                        SystemStatusDescription.Text = $"CPU: {metrics.CpuUsagePercent ?? 0:F0}% • RAM: {(metrics.MemoryUsagePercent ?? 0):F0}%";
                    }
                    if (SystemStatusPillText != null)
                    {
                        SystemStatusPillText.Text = "Performance Mode Active";
                    }
                });
            }

            if (_gameDiscoveryService != null)
            {
                var games = await _gameDiscoveryService.DiscoverGamesAsync(token);
                Dispatcher.UIThread.Post(() =>
                {
                    if (RecentGamesStack != null)
                    {
                        RecentGamesStack.Children.Clear();
                        var recentGames = games.Take(4).ToList();
                        if (recentGames.Count == 0)
                        {
                            RecentGamesStack.Children.Add(new TextBlock
                            {
                                Text = "No recent games found.",
                                Foreground = new SolidColorBrush(Color.Parse("#888888")),
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                            });
                        }
                        else
                        {
                            foreach (var game in recentGames)
                            {
                                var btn = new Button
                                {
                                    Width = 140,
                                    Height = 80,
                                    CornerRadius = new Avalonia.CornerRadius(12),
                                    Content = new TextBlock
                                    {
                                        Text = game.Title,
                                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                                        TextAlignment = Avalonia.Media.TextAlignment.Center,
                                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                                        FontSize = 13,
                                        FontWeight = Avalonia.Media.FontWeight.SemiBold,
                                        Foreground = new SolidColorBrush(Colors.White)
                                    },
                                    Background = new SolidColorBrush(Color.Parse("#1AFFFFFF")),
                                    BorderThickness = new Avalonia.Thickness(0),
                                    ClipToBounds = true
                                };
                                RecentGamesStack.Children.Add(btn);
                            }
                        }
                    }
                });
            }
            if (_audioService != null)
            {
                var masterVol = await _audioService.GetVolumeAsync(token);
                var inputVol = await _audioService.GetInputVolumeAsync(token);
                var isInputMuted = await _audioService.GetInputMutedAsync(token);
                
                Dispatcher.UIThread.Post(() =>
                {
                    if (QuickMasterVolumeSlider != null && masterVol.HasValue)
                    {
                        QuickMasterVolumeSlider.Value = masterVol.Value;
                    }
                    if (QuickMicVolumeSlider != null && inputVol.HasValue)
                    {
                        QuickMicVolumeSlider.Value = inputVol.Value;
                    }
                    _isMicMuted = isInputMuted;
                    if (QuickMuteMicIcon != null) QuickMuteMicIcon.Text = _isMicMuted ? "mic_off" : "mic";
                    if (QuickMuteMicText != null) QuickMuteMicText.Text = _isMicMuted ? "Unmute Microphone" : "Mute Microphone";
                });
            }
        }
        catch { }
    }

    private string _activeControllerName = "DualSense Wireless Controller";

    public void UpdateControllerHintIcons(string? name = null)
    {
        if (!string.IsNullOrEmpty(name))
        {
            _activeControllerName = name;
        }

        var type = ControllerConnectionStatus.DetectType(_activeControllerName);
        var isPlayStation = type == ControllerType.PlayStation;
        var prefix = isPlayStation
            ? "avares://DaisyOS.Shell/Assets/PlaystationController/"
            : "avares://DaisyOS.Shell/Assets/XboxController/";

        try
        {
            if (HintTabLeftImage != null)
                HintTabLeftImage.Source = new Bitmap(AssetLoader.Open(new Uri(prefix + (isPlayStation ? "plain-L1.png" : "left-bumper.png"))));

            if (HintTabRightImage != null)
                HintTabRightImage.Source = new Bitmap(AssetLoader.Open(new Uri(prefix + (isPlayStation ? "plain-R1.png" : "right-bumper.png"))));

            if (HintCategoryDpadImage != null)
                HintCategoryDpadImage.Source = new Bitmap(AssetLoader.Open(new Uri(prefix + (isPlayStation ? "plain-bottom.png" : "dpad.png"))));

            if (HintConfirmImage != null)
                HintConfirmImage.Source = new Bitmap(AssetLoader.Open(new Uri(prefix + (isPlayStation ? "outline-blue-cross.png" : "a-filled-green.png"))));

            if (HintBackImage != null)
                HintBackImage.Source = new Bitmap(AssetLoader.Open(new Uri(prefix + (isPlayStation ? "outline-red-circle.png" : "b-filled red.png"))));
        }
        catch
        {
            // Graceful fallback
        }

        bool onBottomBar = !_isCardFocused;

        if (HintTabLeftImage != null) HintTabLeftImage.IsVisible = onBottomBar;
        if (HintTabRightImage != null) HintTabRightImage.IsVisible = onBottomBar;
        if (HintSlashText != null) HintSlashText.IsVisible = onBottomBar;
        if (HintCategoryDpadImage != null) HintCategoryDpadImage.IsVisible = !onBottomBar;

        if (HintNavText != null)
        {
            HintNavText.Text = onBottomBar ? "Change Category" : "Navigate";
        }

        if (HintConfirmText != null)
        {
            HintConfirmText.Text = onBottomBar ? "Open Section" : "Select Action";
        }

        if (HintBackText != null)
        {
            HintBackText.Text = onBottomBar ? "Close" : "Back to Tabs";
        }
    }

    public async void HideQuickMenu()
    {
        Opacity = 0;
        IsHitTestVisible = false;
        Closed?.Invoke(this, EventArgs.Empty);

        await Task.Delay(250);
        if (Opacity == 0) IsVisible = false;
    }

    public void Navigate(ControllerNavigationAction action)
    {
        switch (action)
        {
            case ControllerNavigationAction.PreviousSection:
                _activeIconIndex = (_activeIconIndex - 1 + _quickCategories.Length) % _quickCategories.Length;
                SelectCategory(_quickCategories[_activeIconIndex]);
                if (_isCardFocused)
                {
                    FocusFirstControlInActiveCard();
                }
                else
                {
                    FocusCurrentCategoryButton();
                }
                break;

            case ControllerNavigationAction.NextSection:
                _activeIconIndex = (_activeIconIndex + 1) % _quickCategories.Length;
                SelectCategory(_quickCategories[_activeIconIndex]);
                if (_isCardFocused)
                {
                    FocusFirstControlInActiveCard();
                }
                else
                {
                    FocusCurrentCategoryButton();
                }
                break;

            case ControllerNavigationAction.Left:
                var focusedL = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
                if (!_isCardFocused)
                {
                    _activeIconIndex = (_activeIconIndex - 1 + _quickCategories.Length) % _quickCategories.Length;
                    SelectCategory(_quickCategories[_activeIconIndex]);
                    FocusCurrentCategoryButton();
                }
                else if (focusedL is ConsolePillSlider pillL)
                {
                    pillL.Value = Math.Max(pillL.Minimum, pillL.Value - pillL.Step);
                }
                else
                {
                    MoveCardFocus(-1);
                }
                break;

            case ControllerNavigationAction.Right:
                var focusedR = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
                if (!_isCardFocused)
                {
                    _activeIconIndex = (_activeIconIndex + 1) % _quickCategories.Length;
                    SelectCategory(_quickCategories[_activeIconIndex]);
                    FocusCurrentCategoryButton();
                }
                else if (focusedR is ConsolePillSlider pillR)
                {
                    pillR.Value = Math.Min(pillR.Maximum, pillR.Value + pillR.Step);
                }
                else
                {
                    MoveCardFocus(1);
                }
                break;

            case ControllerNavigationAction.Up:
                if (!_isCardFocused)
                {
                    _isCardFocused = true;
                    FocusFirstControlInActiveCard();
                }
                else
                {
                    MoveCardFocus(-1);
                }
                break;

            case ControllerNavigationAction.Down:
                if (!_isCardFocused)
                {
                    // Already on bottom category bar
                }
                else
                {
                    MoveCardFocus(1);
                }
                break;

            case ControllerNavigationAction.Confirm:
                var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
                if (!_isCardFocused)
                {
                    _isCardFocused = true;
                    FocusFirstControlInActiveCard();
                }
                else if (focused is Button btn)
                {
                    btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                break;

            case ControllerNavigationAction.Back:
            case ControllerNavigationAction.OpenConsole:
                if (_isCardFocused)
                {
                    FocusCurrentCategoryButton();
                }
                else
                {
                    HideQuickMenu();
                }
                break;
        }
    }

    private List<IInputElement> GetFocusableControlsInActiveCard()
    {
        var controls = new List<IInputElement>();
        var category = _quickCategories[_activeIconIndex];
        switch (category)
        {
            case "Home":
                if (BtnGoHome?.IsVisible == true) controls.Add(BtnGoHome);
                if (BtnExitDesktop?.IsVisible == true) controls.Add(BtnExitDesktop);
                break;
            case "Sound":
                if (QuickMasterVolumeSlider?.IsVisible == true) controls.Add(QuickMasterVolumeSlider);
                if (QuickMicVolumeSlider?.IsVisible == true) controls.Add(QuickMicVolumeSlider);
                if (QuickMuteMicBtn?.IsVisible == true) controls.Add(QuickMuteMicBtn);
                break;
            case "Power":
                if (BtnPowerRestart?.IsVisible == true) controls.Add(BtnPowerRestart);
                if (BtnPowerDesktop?.IsVisible == true) controls.Add(BtnPowerDesktop);
                if (BtnPowerShutdown?.IsVisible == true) controls.Add(BtnPowerShutdown);
                break;
        }
        return controls;
    }

    private void MoveCardFocus(int direction)
    {
        var controls = GetFocusableControlsInActiveCard();
        if (controls.Count == 0) return;

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        int currentIndex = controls.FindIndex(c => ReferenceEquals(c, focused));

        if (currentIndex < 0)
        {
            controls[0].Focus();
            return;
        }

        int newIndex = currentIndex + direction;
        if (newIndex >= 0 && newIndex < controls.Count)
        {
            controls[newIndex].Focus();
        }
        else if (direction > 0 && newIndex >= controls.Count)
        {
            FocusCurrentCategoryButton();
        }
        else if (direction < 0 && newIndex < 0)
        {
            FocusCurrentCategoryButton();
        }
    }

    private void SelectCategory(string category)
    {
        var labelText = category == "Controller" ? "Accessories" : category;
        if (QuickBarCategoryTitle != null)
        {
            QuickBarCategoryTitle.Text = labelText;
            if (QuickBarCategoryTitle.RenderTransform is TranslateTransform tt)
            {
                tt.X = (_activeIconIndex - 2.5) * 62.0;
            }
        }

        // Highlight Active Icon Button
        if (BtnQuickHome != null) BtnQuickHome.Classes.Set("Active", category == "Home");
        if (BtnQuickSwitcher != null) BtnQuickSwitcher.Classes.Set("Active", category == "Switcher");
        if (BtnQuickNotifications != null) BtnQuickNotifications.Classes.Set("Active", category == "Notifications");
        if (BtnQuickSound != null) BtnQuickSound.Classes.Set("Active", category == "Sound");
        if (BtnQuickController != null) BtnQuickController.Classes.Set("Active", category == "Controller");
        if (BtnQuickPower != null) BtnQuickPower.Classes.Set("Active", category == "Power");

        // Toggle Dynamic Cards
        if (CardHomePanel != null) CardHomePanel.IsVisible = category == "Home";
        if (CardSwitcherPanel != null) CardSwitcherPanel.IsVisible = category == "Switcher";
        if (CardNotificationsPanel != null) CardNotificationsPanel.IsVisible = category == "Notifications";
        if (CardSoundPanel != null) CardSoundPanel.IsVisible = category == "Sound";
        if (CardControllerPanel != null) CardControllerPanel.IsVisible = category == "Controller";
        if (CardPowerPanel != null) CardPowerPanel.IsVisible = category == "Power";
    }

    private void FocusCurrentCategoryButton()
    {
        _isCardFocused = false;
        UpdateControllerHintIcons();
        switch (_activeIconIndex)
        {
            case 0: BtnQuickHome?.Focus(); break;
            case 1: BtnQuickSwitcher?.Focus(); break;
            case 2: BtnQuickNotifications?.Focus(); break;
            case 3: BtnQuickSound?.Focus(); break;
            case 4: BtnQuickController?.Focus(); break;
            case 5: BtnQuickPower?.Focus(); break;
        }
    }

    private void FocusFirstControlInActiveCard()
    {
        _isCardFocused = true;
        UpdateControllerHintIcons();
        var controls = GetFocusableControlsInActiveCard();
        if (controls.Count > 0)
        {
            controls[0].Focus();
        }
        else
        {
            FocusCurrentCategoryButton();
        }
    }

    private void ExecuteCategoryAction(string category)
    {
        if (category == "Home")
        {
            HideQuickMenu();
            GoHomeRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnIconBarItemClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            for (int i = 0; i < _quickCategories.Length; i++)
            {
                if (_quickCategories[i] == tag)
                {
                    _activeIconIndex = i;
                    SelectCategory(tag);
                    FocusCurrentCategoryButton();
                    break;
                }
            }
        }
    }

    private void OnGoHomeClicked(object? sender, RoutedEventArgs e)
    {
        HideQuickMenu();
        GoHomeRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void OnQuickMasterVolumeChanged(object? sender, double value)
    {
        if (_audioService != null)
        {
            try
            {
                await _audioService.SetVolumeAsync(value).ConfigureAwait(true);
            }
            catch
            {
                // Best effort
            }
        }
    }

    private async void OnQuickMicVolumeChanged(object? sender, double value)
    {
        if (_audioService != null)
        {
            try
            {
                await _audioService.SetInputVolumeAsync(value).ConfigureAwait(true);
            }
            catch
            {
                // Best effort
            }
        }
    }

    private async void OnQuickToggleMicMute(object? sender, RoutedEventArgs e)
    {
        if (_audioService != null)
        {
            _isMicMuted = !_isMicMuted;
            try
            {
                await _audioService.SetInputMutedAsync(_isMicMuted).ConfigureAwait(true);
            }
            catch
            {
                // Revert on failure
                _isMicMuted = !_isMicMuted;
            }
            
            if (QuickMuteMicIcon != null) QuickMuteMicIcon.Text = _isMicMuted ? "mic_off" : "mic";
            if (QuickMuteMicText != null) QuickMuteMicText.Text = _isMicMuted ? "Unmute Microphone" : "Mute Microphone";
        }
    }

    private void OnQuickRestartClicked(object? sender, RoutedEventArgs e)
    {
        HideQuickMenu();
        if (_powerService != null) _ = _powerService.RebootAsync();
        RestartRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnQuickReturnDesktopClicked(object? sender, RoutedEventArgs e)
    {
        HideQuickMenu();
        ReturnToDesktopRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnQuickShutdownClicked(object? sender, RoutedEventArgs e)
    {
        HideQuickMenu();
        if (_powerService != null) _ = _powerService.ShutdownAsync();
        ShutdownRequested?.Invoke(this, EventArgs.Empty);
    }
}
