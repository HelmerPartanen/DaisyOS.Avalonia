using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DaisyOS.Shell.ViewModels;

namespace DaisyOS.Shell.Controls;

public partial class DesktopItemView : UserControl
{
    public static readonly StyledProperty<bool> IsDragProxyProperty =
        AvaloniaProperty.Register<DesktopItemView, bool>(nameof(IsDragProxy));

    public bool IsDragProxy
    {
        get => GetValue(IsDragProxyProperty);
        set => SetValue(IsDragProxyProperty, value);
    }

    public static readonly StyledProperty<bool> IsHiddenPlaceholderProperty =
        AvaloniaProperty.Register<DesktopItemView, bool>(nameof(IsHiddenPlaceholder));

    public bool IsHiddenPlaceholder
    {
        get => GetValue(IsHiddenPlaceholderProperty);
        set => SetValue(IsHiddenPlaceholderProperty, value);
    }

    private DesktopItemViewModel? _subscribedVm;

    public DesktopItemView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedVm != null)
        {
            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedVm = null;
        }

        if (DataContext is DesktopItemViewModel vm)
        {
            _subscribedVm = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            SyncProperties(vm);
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (_subscribedVm != null)
        {
            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedVm = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is DesktopItemViewModel vm)
        {
            SyncProperties(vm);
        }
    }

    private void SyncProperties(DesktopItemViewModel vm)
    {
        IsDragProxy = vm.IsDragging;
        IsHiddenPlaceholder = vm.IsDragging ? false : vm.IsHiddenPlaceholder;
    }
}
