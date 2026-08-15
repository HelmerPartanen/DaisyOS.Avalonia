using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.System.Controllers;
using DaisyOS.Shell.Views.Components.Console;
using DaisyOS.Shell.Views.Components.Launcher;
using DaisyOS.Shell.Views.Components.SystemBar;
using DaisyOS.Shell.Views.Components.Taskbar;

namespace DaisyOS.Shell.Views
{
    public partial class ShellView : UserControl
    {
        private readonly DispatcherTimer _feedbackTimer = new() { Interval = TimeSpan.FromSeconds(5) };
        private readonly DispatcherTimer _controllerPollTimer = new() { Interval = TimeSpan.FromSeconds(2) };
        private static readonly TimeSpan BlackFadeDuration = TimeSpan.FromMilliseconds(640);
        private static readonly TimeSpan ConsoleLoadingDuration = TimeSpan.FromMilliseconds(720);
        private static readonly TimeSpan DesktopRestoreDuration = TimeSpan.FromMilliseconds(360);
        private readonly IControllerService _controllerService;
        private CancellationTokenSource? _controllerCancellation;
        private bool _controllerConnected;
        private bool _checkingController;
        private bool _consoleMode;
        private bool _consoleTransitionInProgress;

        public ShellView()
            : this(new LinuxControllerService())
        {
        }

        public ShellView(IControllerService controllerService)
        {
            _controllerService = controllerService ?? throw new ArgumentNullException(nameof(controllerService));
            InitializeComponent();
            AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            if (Application.Current is App feedbackApp)
            {
                feedbackApp.Feedback.MessageShown += OnFeedbackMessageShown;
                Unloaded += (_, _) => feedbackApp.Feedback.MessageShown -= OnFeedbackMessageShown;
            }
            _feedbackTimer.Tick += (_, _) =>
            {
                _feedbackTimer.Stop();
                if (this.FindControl<Control>("FeedbackHost") is { } host) host.IsVisible = false;
            };
            _controllerPollTimer.Tick += async (_, _) => await RefreshControllerStateAsync();
        }

        public TaskbarView Taskbar => TaskbarContent;
        public SystemBarView SystemBar => SystemBarContent;
        public LauncherView Launcher => LauncherContent;
        public bool IsLauncherVisible => LauncherContent.IsVisible;

        private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _controllerCancellation = new CancellationTokenSource();
            _controllerPollTimer.Start();
            _ = RefreshControllerStateAsync();
        }

        private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _controllerPollTimer.Stop();
            _controllerCancellation?.Cancel();
            _controllerCancellation?.Dispose();
            _controllerCancellation = null;
        }

        private async Task RefreshControllerStateAsync()
        {
            if (_checkingController || _controllerCancellation is null)
            {
                return;
            }

            _checkingController = true;
            try
            {
                var status = await _controllerService.GetConnectionStatusAsync(_controllerCancellation.Token);
                await Dispatcher.UIThread.InvokeAsync(() => ApplyControllerStatusAsync(status));
            }
            catch (OperationCanceledException)
            {
                // Shell shutdown supersedes a pending probe.
            }
            catch (Exception ex)
            {
                // Hardware probing is best effort. Preserve the current shell mode if it fails.
                Console.WriteLine($"Failed to read controller state: {ex.Message}");
            }
            finally
            {
                _checkingController = false;
            }
        }

        private async Task ApplyControllerStatusAsync(ControllerConnectionStatus status)
        {
            _controllerConnected = status.IsConnected;
            if (status.IsConnected)
            {
                ConsoleHome.ControllerName = status.Name ?? "Game controller";
            }

            await ReconcileConsoleModeAsync();
        }

        private async Task ReconcileConsoleModeAsync()
        {
            if (_consoleTransitionInProgress)
            {
                return;
            }

            _consoleTransitionInProgress = true;
            try
            {
                // Keep the black transition active while an input device settles. A controller
                // may appear and disappear during a Bluetooth reconnect; only reveal a mode
                // after the newest observed state has been applied.
                while (_consoleMode != _controllerConnected)
                {
                    var enteringConsoleMode = _controllerConnected;
                    ConsoleTransitionTitle.Text = enteringConsoleMode ? "Switching to console mode" : "Returning to desktop";
                    ConsoleTransitionDetail.Text = enteringConsoleMode
                        ? $"{ConsoleHome.ControllerName} connected"
                        : "Controller disconnected";
                    ConsoleTransition.IsVisible = true;
                    await Task.Delay(24);
                    ConsoleTransition.Opacity = 1;
                    await Task.Delay(BlackFadeDuration);

                    if (enteringConsoleMode)
                    {
                        (Application.Current as App)?.DismissTransientShellSurfaces();
                        // Disable the entire desktop tree before revealing console mode. The black
                        // screen above both roots makes this hand-off visually continuous.
                        DesktopExperience.IsHitTestVisible = false;
                        DesktopExperience.IsVisible = false;
                        ConsoleHome.IsVisible = true;
                        ShellWallpaper.SetConsoleParallaxEnabled(true);
                        await Task.Delay(ConsoleLoadingDuration);
                    }
                    else
                    {
                        ConsoleHome.IsVisible = false;
                        ShellWallpaper.SetConsoleParallaxEnabled(false);
                        DesktopExperience.IsVisible = true;
                        DesktopExperience.IsHitTestVisible = true;
                        await Task.Delay(DesktopRestoreDuration);
                    }

                    // Do not expose a mode which was superseded while its transition was running.
                    // Revert the covered surface to the known mode, then the loop can reconcile
                    // any still-pending change without flashing either experience.
                    if (enteringConsoleMode != _controllerConnected)
                    {
                        ConsoleHome.IsVisible = _consoleMode;
                        DesktopExperience.IsVisible = !_consoleMode;
                        DesktopExperience.IsHitTestVisible = !_consoleMode;
                        ShellWallpaper.SetConsoleParallaxEnabled(_consoleMode);
                    }
                    else
                    {
                        _consoleMode = enteringConsoleMode;
                    }

                    ConsoleTransition.Opacity = 0;
                    await Task.Delay(BlackFadeDuration);
                    ConsoleTransition.IsVisible = false;
                }
            }
            finally
            {
                _consoleTransitionInProgress = false;

                // A probe can complete between the final condition and this assignment.
                if (_consoleMode != _controllerConnected)
                {
                    _ = ReconcileConsoleModeAsync();
                }
            }
        }

        public void ShowLauncher()
        {
            LauncherContent.IsVisible = true;
            Dispatcher.UIThread.Post(LauncherContent.FocusSearch, DispatcherPriority.Input);
        }

        public void HideLauncher() => LauncherContent.IsVisible = false;

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source is not Visual source)
            {
                return;
            }

            // The taskbar/System Bar toggles must see their own press before they decide
            // whether to open or close an overlay. Everything else is an outside click.
            if (IsLauncherVisible && !IsWithin(source, LauncherContent) && !IsWithin(source, TaskbarContent))
            {
                (Application.Current as App)?.HideLauncher();
            }

            if (SystemBar.IsQuickSettingsVisible && !IsWithin(source, SystemBarContent))
            {
                (Application.Current as App)?.DismissTransientShellSurfaces();
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_consoleMode)
            {
                ShellWallpaper.SetParallaxTarget(e.GetPosition(this), Bounds.Size);
            }
        }

        private static bool IsWithin(Visual source, Visual container) =>
            ReferenceEquals(source, container) || source.GetVisualAncestors().Any(ancestor => ReferenceEquals(ancestor, container));

        private void OnFeedbackMessageShown(object? sender, string message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (this.FindControl<TextBlock>("FeedbackText") is { } text) text.Text = message;
                if (this.FindControl<Control>("FeedbackHost") is { } host) host.IsVisible = true;
                _feedbackTimer.Stop();
                _feedbackTimer.Start();
            });
        }

    }
}
