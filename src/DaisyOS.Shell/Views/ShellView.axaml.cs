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
                taskbar.StartButtonClicked += (s, e) =>
                {
                    launcher.IsVisible = !launcher.IsVisible;
                };
            }
        }
    }
}
