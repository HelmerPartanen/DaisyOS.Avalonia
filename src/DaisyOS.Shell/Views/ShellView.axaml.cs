using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DaisyOS.System.Processes;
using DaisyOS.Shell.Views.Components.Taskbar;
using DaisyOS.Shell.Views.Components.Desktop;
using DaisyOS.Shell.Views.Components.Launcher;

namespace DaisyOS.Shell.Views
{
    public partial class ShellView : UserControl
    {
        private readonly LinuxAppLauncherService _appLauncher = new();

        public ShellView()
        {
            InitializeComponent();

            var taskbar = this.FindControl<TaskbarView>("Taskbar");
            var launcher = this.FindControl<LauncherView>("Launcher");

            if (taskbar != null && launcher != null)
            {
                taskbar.StartButtonClicked += (_, _) => SetLauncherOpen(launcher, taskbar, !launcher.IsVisible);
                launcher.AppLaunchRequested += (_, _) => SetLauncherOpen(launcher, taskbar, false);
                taskbar.AppIconClicked += async (_, appId) => await LaunchTaskbarAppAsync(appId, launcher, taskbar);
                AddHandler(InputElement.PointerPressedEvent, (_, e) => DismissLauncherOnOutsidePress(e, launcher, taskbar), RoutingStrategies.Tunnel, handledEventsToo: true);
                AddHandler(InputElement.KeyDownEvent, (_, e) => DismissLauncherOnEscape(e, launcher, taskbar), RoutingStrategies.Bubble, handledEventsToo: true);
            }
        }

        private async Task LaunchTaskbarAppAsync(string appId, LauncherView launcher, TaskbarView taskbar)
        {
            SetLauncherOpen(launcher, taskbar, false);

            if (appId == "settings" && Application.Current is App app)
            {
                app.ShowSettings();
                return;
            }

            var matchingApp = appId switch
            {
                "files" => FindInstalledApp("dolphin", "nautilus", "thunar", "files"),
                _ => null
            };

            if (matchingApp is not null)
            {
                await _appLauncher.LaunchAsync(matchingApp);
                return;
            }

            if (appId == "files")
            {
                // xdg-open honors the user's preferred graphical file manager.
                new SafeProcessLauncher().Launch("xdg-open", [Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)]);
            }
        }

        private DaisyOS.Core.Models.AppEntry? FindInstalledApp(params string[] candidates) =>
            _appLauncher.GetAvailableApps().FirstOrDefault(app =>
                candidates.Any(candidate =>
                    app.Id.Contains(candidate, StringComparison.OrdinalIgnoreCase) ||
                    app.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)));

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
