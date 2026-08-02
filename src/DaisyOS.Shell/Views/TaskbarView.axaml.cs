using Avalonia.Controls;

namespace DaisyOS.Shell.Views
{
    public partial class TaskbarView : UserControl
    {
        public event global::System.EventHandler<Avalonia.Interactivity.RoutedEventArgs>? StartButtonClicked;

        public TaskbarView()
        {
            InitializeComponent();
            
            var startBtn = this.FindControl<Button>("StartButton");
            if (startBtn != null)
            {
                startBtn.Click += (s, e) => StartButtonClicked?.Invoke(this, e);
            }
        }
    }
}
