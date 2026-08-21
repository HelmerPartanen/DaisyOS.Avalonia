using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace DaisyOS.Shell.Controls;

public partial class SearchBar : UserControl
{
    public static new readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<SearchBar, CornerRadius>(nameof(CornerRadius), new CornerRadius(10));

    public static readonly StyledProperty<string> PlaceholderTextProperty =
        AvaloniaProperty.Register<SearchBar, string>(nameof(PlaceholderText), "Search...");

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<SearchBar, string>(nameof(Text), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    public new CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public string PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

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
        var outline = this.FindControl<Border>("FocusOutline");

        if (outline is not null)
        {
            outline.Opacity = isFocused ? 1 : 0;
            outline.BorderBrush = outline.FindResource("FocusRingBrush") as IBrush;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
