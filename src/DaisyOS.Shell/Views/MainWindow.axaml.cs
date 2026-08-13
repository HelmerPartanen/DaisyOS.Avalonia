using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

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

            _ = this.FindControl<ShellView>("Shell")?.PlayTopEdgeMetaballAsync();
            e.Handled = true;
        }
    }
}
