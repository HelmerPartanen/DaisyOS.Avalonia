using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DaisyOS.Shell.Views.Components.Taskbar;
using DaisyOS.Shell.Views.Components.Desktop;
using DaisyOS.Shell.Views.Components.Launcher;
using DaisyOS.Shell.Services;

namespace DaisyOS.Shell.Views
{
    public partial class ShellView : UserControl
    {
        private readonly ShellSessionState _sessionState = (Application.Current as App)?.SessionState ?? new ShellSessionState();
        private readonly DispatcherTimer _feedbackTimer = new() { Interval = TimeSpan.FromSeconds(5) };

        public ShellView()
        {
            InitializeComponent();

            var taskbar = this.FindControl<TaskbarView>("Taskbar");
            var launcher = this.FindControl<LauncherView>("Launcher");

            if (taskbar != null && launcher != null)
            {
                taskbar.ApplyOrder(_sessionState.DockOrder);
                taskbar.OrderChanged += (_, order) => _sessionState.SetDockOrder(order);
                taskbar.StartButtonClicked += (_, _) => SetLauncherOpen(launcher, taskbar, !launcher.IsVisible);
                launcher.AppLaunchRequested += (_, _) => SetLauncherOpen(launcher, taskbar, false);
                taskbar.AppIconClicked += async (_, appId) => await LaunchTaskbarAppAsync(appId, launcher, taskbar);
                AddHandler(InputElement.PointerPressedEvent, (_, e) => DismissLauncherOnOutsidePress(e, launcher, taskbar), RoutingStrategies.Tunnel, handledEventsToo: true);
                AddHandler(InputElement.KeyDownEvent, (_, e) => DismissLauncherOnEscape(e, launcher, taskbar), RoutingStrategies.Bubble, handledEventsToo: true);
            }

            if (Application.Current is App app)
            {
                app.Feedback.MessageShown += OnFeedbackMessageShown;
                Unloaded += (_, _) => app.Feedback.MessageShown -= OnFeedbackMessageShown;
            }
            _feedbackTimer.Tick += (_, _) =>
            {
                _feedbackTimer.Stop();
                if (this.FindControl<Control>("FeedbackHost") is { } host) host.IsVisible = false;
            };
        }

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

        private async Task LaunchTaskbarAppAsync(string appId, LauncherView launcher, TaskbarView taskbar)
        {
            SetLauncherOpen(launcher, taskbar, false);

            if (appId == "settings" && Application.Current is App settingsApp)
            {
                settingsApp.ShowSettings();
                return;
            }

            if (appId == "notes" && Application.Current is App notesApp)
            {
                notesApp.ShowNotes();
                return;
            }

            if (appId == "calculator" && Application.Current is App calculatorApp)
            {
                calculatorApp.ShowCalculator();
                return;
            }

            if (appId == "files" && Application.Current is App filesApp)
            {
                filesApp.ShowFiles();
                return;
            }

        }

        private static void SetLauncherOpen(LauncherView launcher, TaskbarView taskbar, bool isOpen)
        {
            launcher.IsVisible = isOpen;
            taskbar.SetLauncherOpen(isOpen);

            if (isOpen)
            {
                Dispatcher.UIThread.Post(launcher.FocusSearch, DispatcherPriority.Input);
            }
        }

        private static void DismissLauncherOnEscape(KeyEventArgs e, LauncherView launcher, TaskbarView taskbar)
        {
            if (e.Key == Key.Escape && launcher.IsVisible)
            {
                SetLauncherOpen(launcher, taskbar, false);
                e.Handled = true;
            }
        }

        private static void DismissLauncherOnOutsidePress(PointerEventArgs e, LauncherView launcher, TaskbarView taskbar)
        {
            if (!launcher.IsVisible || e.Source is not Visual source)
            {
                return;
            }

            var isInsideLauncher = source == launcher || source.GetVisualAncestors().OfType<LauncherView>().Any();
            var isInsideTaskbar = source == taskbar || source.GetVisualAncestors().OfType<TaskbarView>().Any();
            if (!isInsideLauncher && !isInsideTaskbar)
            {
                SetLauncherOpen(launcher, taskbar, false);
            }
        }
    }
}
