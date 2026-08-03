using Avalonia.Controls;
using DaisyOS.Shell.Views.Components.Taskbar;
using DaisyOS.Shell.Views.Components.Desktop;
using DaisyOS.Shell.Views.Components.Launcher;

namespace DaisyOS.Shell.Views
{
    public partial class ShellView : UserControl
    {
        public ShellView()
        {
            InitializeComponent();

            var taskbar = this.FindControl<TaskbarView>("Taskbar");
            var launcher = this.FindControl<LauncherView>("Launcher");

            if (taskbar != null && launcher != null)
            {
                taskbar.StartButtonClicked += async (s, e) =>
                {
                    // Toggle: fire show or hide; LauncherView guards against double-fire
                    if (launcher.IsOpen)
                        await launcher.HideAsync();
                    else
                        await launcher.ShowAsync();
                };
            }
        }
    }
}
