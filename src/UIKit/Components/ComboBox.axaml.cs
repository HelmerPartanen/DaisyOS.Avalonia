using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.Selection;
using System.Collections;

namespace UIKit.Components;

public partial class ComboBox : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<ComboBox, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<ComboBox, int>(nameof(SelectedIndex), 0);
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<ComboBox, object?>(nameof(SelectedItem));

    public ComboBox()
    {
        ItemsSource = new[] { "Balanced", "Performance", "Battery saver" };
        InitializeComponent();
        SynchronizeSelection();
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty || change.Property == SelectedIndexProperty)
            SynchronizeSelection();
    }

    private void PickerButton_OnClick(object? sender, RoutedEventArgs e) =>
        OptionsPopup.IsOpen = !OptionsPopup.IsOpen;

    private void Picker_OnGotFocus(object? sender, RoutedEventArgs e)
    {
        FocusOutline.Classes.Set("Focused", true);
        Field.Classes.Set("Focused", true);
    }

    private void Picker_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        FocusOutline.Classes.Set("Focused", false);
        Field.Classes.Set("Focused", false);
    }

    private void OptionsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SelectedItem = OptionsList.SelectedItem;
        SelectedIndex = OptionsList.SelectedIndex;
        OptionsPopup.IsOpen = false;
    }

    private void SynchronizeSelection()
    {
        if (ItemsSource is not IList items || items.Count == 0)
        {
            SelectedItem = null;
            return;
        }

        var index = Math.Clamp(SelectedIndex, 0, items.Count - 1);
        if (SelectedIndex != index) SelectedIndex = index;
        SelectedItem = items[index];
        if (OptionsList is not null) OptionsList.SelectedIndex = index;
    }
}
