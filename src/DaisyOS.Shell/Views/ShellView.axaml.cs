using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace DaisyOS.Shell.Views
{
    public partial class ShellView : UserControl
    {
        private readonly DispatcherTimer _feedbackTimer = new() { Interval = TimeSpan.FromSeconds(5) };

        public ShellView()
        {
            InitializeComponent();

            if (Application.Current is App feedbackApp)
            {
                feedbackApp.Feedback.MessageShown += OnFeedbackMessageShown;
                Unloaded += (_, _) => feedbackApp.Feedback.MessageShown -= OnFeedbackMessageShown;
            }
            _feedbackTimer.Tick += (_, _) =>
            {
                _feedbackTimer.Stop();
                if (this.FindControl<Control>("FeedbackHost") is { } host) host.IsVisible = false;
            };
        }

        public Task PlayTopEdgeMetaballAsync() =>
            this.FindControl<Controls.TopEdgeMetaball>("TopEdgeMetaball")?.PlayKeyboardRevealAsync()
            ?? Task.CompletedTask;

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

    }
}
