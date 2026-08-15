using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.Shell.Controls;
using DaisyOS.System.Audio;

namespace DaisyOS.Shell.Views.Components.Console;

public partial class ConsoleQuickMenuOverlay : UserControl
{
    private readonly IAudioService? _audioService;

    public event EventHandler? Closed;
    public event EventHandler? GoHomeRequested;
    public event EventHandler? ReturnToDesktopRequested;
    public event EventHandler? RestartRequested;
    public event EventHandler? ShutdownRequested;

    private int _activeIconIndex = 0;
    private bool _isCardFocused = false;
    private bool _isMicMuted = false;

    private readonly string[] _quickCategories = new[]
    {
        "Home", "Switcher", "Notifications", "Sound", "Mic", "Controller", "Power"
    };

    public ConsoleQuickMenuOverlay()
    {
        InitializeComponent();
    }

    public ConsoleQuickMenuOverlay(IAudioService audioService) : this()
    {
        _audioService = audioService;
    }

    public void ShowQuickMenu()
    {
        IsVisible = true;
        SelectCategory(_quickCategories[_activeIconIndex]);

        Dispatcher.UIThread.Post(() =>
        {
            FocusCurrentCategoryButton();
        }, DispatcherPriority.Input);
    }

    public void HideQuickMenu()
    {
        IsVisible = false;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    public void Navigate(ControllerNavigationAction action)
    {
        switch (action)
        {
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
                break;

            case ControllerNavigationAction.Up:
                if (!_isCardFocused)
                {
                    _isCardFocused = true;
                    FocusFirstControlInActiveCard();
                }
                break;

            case ControllerNavigationAction.Down:
                if (_isCardFocused)
                {
                    _isCardFocused = false;
                    FocusCurrentCategoryButton();
                }
                break;

            case ControllerNavigationAction.Confirm:
                var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
                if (!_isCardFocused)
                {
                    ExecuteCategoryAction(_quickCategories[_activeIconIndex]);
                }
                else if (focused is Button btn)
                {
                    btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                break;

            case ControllerNavigationAction.Back:
            case ControllerNavigationAction.OpenConsole:
                HideQuickMenu();
                break;
        }
    }

    private void SelectCategory(string category)
    {
        if (QuickBarCategoryTitle != null)
        {
            QuickBarCategoryTitle.Text = category;
        }

        // Highlight Active Icon Button
        if (BtnQuickHome != null) BtnQuickHome.Classes.Set("Active", category == "Home");
        if (BtnQuickSwitcher != null) BtnQuickSwitcher.Classes.Set("Active", category == "Switcher");
        if (BtnQuickNotifications != null) BtnQuickNotifications.Classes.Set("Active", category == "Notifications");
        if (BtnQuickSound != null) BtnQuickSound.Classes.Set("Active", category == "Sound");
        if (BtnQuickMic != null) BtnQuickMic.Classes.Set("Active", category == "Mic");
        if (BtnQuickController != null) BtnQuickController.Classes.Set("Active", category == "Controller");
        if (BtnQuickPower != null) BtnQuickPower.Classes.Set("Active", category == "Power");

        // Toggle Dynamic Cards
        if (CardHomePanel != null) CardHomePanel.IsVisible = category == "Home";
        if (CardSwitcherPanel != null) CardSwitcherPanel.IsVisible = category == "Switcher";
        if (CardNotificationsPanel != null) CardNotificationsPanel.IsVisible = category == "Notifications";
        if (CardSoundPanel != null) CardSoundPanel.IsVisible = category == "Sound";
        if (CardMicPanel != null) CardMicPanel.IsVisible = category == "Mic";
        if (CardControllerPanel != null) CardControllerPanel.IsVisible = category == "Controller";
        if (CardPowerPanel != null) CardPowerPanel.IsVisible = category == "Power";
    }

    private void FocusCurrentCategoryButton()
    {
        _isCardFocused = false;
        switch (_activeIconIndex)
        {
            case 0: BtnQuickHome?.Focus(); break;
            case 1: BtnQuickSwitcher?.Focus(); break;
            case 2: BtnQuickNotifications?.Focus(); break;
            case 3: BtnQuickSound?.Focus(); break;
            case 4: BtnQuickMic?.Focus(); break;
            case 5: BtnQuickController?.Focus(); break;
            case 6: BtnQuickPower?.Focus(); break;
        }
    }

    private void FocusFirstControlInActiveCard()
    {
        _isCardFocused = true;
        var category = _quickCategories[_activeIconIndex];
        switch (category)
        {
            case "Sound": QuickMasterVolumeSlider?.Focus(); break;
            case "Mic": QuickMicVolumeSlider?.Focus(); break;
            case "MicMute": QuickMuteMicBtn?.Focus(); break;
            case "Power": OnQuickRestartClicked(null, new RoutedEventArgs()); break;
            default: FocusCurrentCategoryButton(); break;
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

    private void OnQuickMicVolumeChanged(object? sender, double value)
    {
        // Mic volume adjustment
    }

    private void OnQuickToggleMicMute(object? sender, RoutedEventArgs e)
    {
        _isMicMuted = !_isMicMuted;
        if (QuickMuteMicIcon != null) QuickMuteMicIcon.Text = _isMicMuted ? "mic_off" : "mic";
        if (QuickMuteMicText != null) QuickMuteMicText.Text = _isMicMuted ? "Unmute Microphone" : "Mute Microphone";
    }

    private void OnQuickRestartClicked(object? sender, RoutedEventArgs e)
    {
        HideQuickMenu();
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
        ShutdownRequested?.Invoke(this, EventArgs.Empty);
    }
}
