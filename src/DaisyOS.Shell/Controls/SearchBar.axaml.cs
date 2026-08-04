using Avalonia.Controls;
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
            input.GotFocus += (_, _) => SetFocusVisualState(isFocused: true);
            input.LostFocus += (_, _) => SetFocusVisualState(isFocused: false);
        }
    }

    /// <summary>Places the keyboard cursor in the launcher search field when the launcher opens.</summary>
    public void FocusInput() => this.FindControl<TextBox>("SearchInput")?.Focus();

    private void SetFocusVisualState(bool isFocused)
    {
        var frame = this.FindControl<Border>("SearchFrame");
        var outline = this.FindControl<Border>("FocusOutline");
        var resourceKey = isFocused ? "AppPrimaryContainerBrush" : "TaskbarBorderBrush";

        if (frame is not null)
        {
            frame.BorderBrush = frame.FindResource(resourceKey) as IBrush;
        }

        if (outline is not null)
        {
            outline.Opacity = isFocused ? 1 : 0;
            outline.BorderBrush = outline.FindResource("AppPrimaryContainerBrush") as IBrush;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
