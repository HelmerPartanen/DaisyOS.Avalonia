using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DaisyOS.Shell.Apps.System.Settings
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private void OnNavCategoryClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is Button clickedBtn)
            {
                var parent = clickedBtn.Parent as StackPanel;
                if (parent != null)
                {
                    foreach (var child in parent.Children)
                    {
                        if (child is Button btn)
                        {
                            btn.Classes.Remove("Active");
                        }
                    }
                }

                clickedBtn.Classes.Add("Active");

                if (SystemListView != null && SystemDetailView != null)
                {
                    SystemListView.IsVisible = true;
                    SystemDetailView.IsVisible = false;
                }
            }
        }

        private void OnSystemSettingItemClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string settingKey })
            {
                if (SystemListView != null && SystemDetailView != null)
                {
                    SystemListView.IsVisible = false;
                    SystemDetailView.IsVisible = true;

                    switch (settingKey)
                    {
                        case "Power":
                            DetailTitleText.Text = "Power & Battery";
                            break;
                        case "Multitasking":
                            DetailTitleText.Text = "Multitasking & Workspaces";
                            break;
                        case "Performance":
                            DetailTitleText.Text = "System Performance";
                            break;
                        case "Recovery":
                            DetailTitleText.Text = "Recovery & Maintenance";
                            break;
                        case "Clipboard":
                            DetailTitleText.Text = "Clipboard & History";
                            break;
                    }
                }
            }
        }

        private void OnBackToSystemListClicked(object? sender, RoutedEventArgs e)
        {
            if (SystemListView != null && SystemDetailView != null)
            {
                SystemListView.IsVisible = true;
                SystemDetailView.IsVisible = false;
            }
        }
    }
}
