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
            }
        }
    }
}
