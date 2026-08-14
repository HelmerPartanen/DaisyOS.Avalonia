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

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space || (e.KeyModifiers & KeyModifiers.Control) == 0)
            {
                return;
            }

            _ = (Application.Current as App)?.PlayTopEdgeMetaballAsync();
            e.Handled = true;
        }
    }
}
