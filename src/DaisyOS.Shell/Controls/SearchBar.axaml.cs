using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace DaisyOS.Shell.Controls;

public partial class SearchBar : UserControl
{
    public SearchBar()
    {
        InitializeComponent();

        var input = this.FindControl<TextBox>("SearchInput");
        if (input is not null)
        {
            input.GotFocus += OnSearchInputFocusChanged;
            input.LostFocus += OnSearchInputFocusChanged;
        }
    }

    /// <summary>Places the keyboard cursor in the launcher search field when the launcher opens.</summary>
    public void FocusInput() => this.FindControl<TextBox>("SearchInput")?.Focus();

    private void OnSearchInputFocusChanged(object? sender, RoutedEventArgs e)
    {
        var frame = this.FindControl<Border>("SearchFrame");
        var outline = this.FindControl<Border>("FocusOutline");
        var isFocused = sender is TextBox { IsFocused: true };
        var resourceKey = isFocused ? "AppPrimaryContainerBrush" : "TaskbarBorderBrush";

        if (frame is not null)
        {
            frame.BorderBrush = frame.FindResource(resourceKey) as IBrush;
        }

        if (outline is not null)
        {
            outline.IsVisible = isFocused;
            outline.BorderBrush = outline.FindResource("AppPrimaryContainerBrush") as IBrush;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
