using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DaisyOS.Shell;

namespace DaisyOS.Shell.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        }

        public ShellView ShellContent => Shell;

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.R && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                ShellContent.ToggleMetricsPanel();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape && (Application.Current as App)?.DismissTransientShellSurfaces() == true)
            {
                e.Handled = true;
            }
        }
    }
}
