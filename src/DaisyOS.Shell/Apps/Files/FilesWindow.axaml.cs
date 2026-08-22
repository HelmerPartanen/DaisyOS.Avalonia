using System;
using Directory = global::System.IO.Directory;
using File = global::System.IO.File;
using Path = global::System.IO.Path;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DaisyOS.Shell.Apps;
using DaisyOS.System.Processes;

namespace DaisyOS.Shell.Apps.Files;

public partial class FilesWindow : Window
{
    private readonly FilesViewModel _viewModel;
    private readonly SystemAppWindowChrome _windowChrome;

    public FilesWindow() : this(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) { }

    internal FilesWindow(string initialPath)
    {
        InitializeComponent();
        _viewModel = new FilesViewModel(initialPath);
        DataContext = _viewModel;
        _windowChrome = new SystemAppWindowChrome(this, AppFrame, FrameHighlight, ContentFrame, TitleBar, ContentLayout,
            [ResizeTop, ResizeBottom, ResizeLeft, ResizeRight, ResizeTopLeft, ResizeTopRight, ResizeBottomLeft, ResizeBottomRight],
            "44,52,42,*,26", "0,52,42,*,26");

        Activated += (_, _) => AppFrame.Classes.Set("WindowFocused", true);
        Deactivated += (_, _) => AppFrame.Classes.Set("WindowFocused", false);

        Loaded += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                GC.Collect(2, GCCollectionMode.Optimized, false);
            }, DispatcherPriority.Background);
        };
    }

    public void TogglePerformanceOverlay()
    {
        PerfOverlay.ToggleOverlayVisibility();
    }

    protected override void OnClosed(EventArgs e)
    {
        _windowChrome.Dispose();
        base.OnClosed(e);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source &&
            source is not TextBox &&
            source is not DaisyOS.Shell.Controls.SearchBar &&
            !source.GetVisualAncestors().Any(v => v is DaisyOS.Shell.Controls.SearchBar || v is TextBox))
        {
            FocusManager?.Focus(null);
        }
    }

    private void OnMinimizeClicked(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaximizeClicked(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnTabClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: FilesTabViewModel tab })
        {
            _viewModel.ActiveTab = tab;
        }
    }

    private void OnCloseTabClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: FilesTabViewModel tab })
        {
            _viewModel.CloseTab(tab);
        }
    }

    private void OnNewTabClicked(object? sender, RoutedEventArgs e) => _viewModel.NewTab();

    private void OnBackClicked(object? sender, RoutedEventArgs e) => _viewModel.ActiveTab?.GoBack();
    private void OnForwardClicked(object? sender, RoutedEventArgs e) => _viewModel.ActiveTab?.GoForward();
    private void OnUpClicked(object? sender, RoutedEventArgs e) => _viewModel.ActiveTab?.GoUp();
    private void OnRefreshClicked(object? sender, RoutedEventArgs e) => _viewModel.ActiveTab?.Refresh();

    private void OnPathKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox textBox || _viewModel.ActiveTab is null) return;
        _viewModel.ActiveTab.NavigateToAsync(textBox.Text ?? _viewModel.ActiveTab.CurrentPath);
        PathBox.IsVisible = false;
        e.Handled = true;
    }

    private void OnPathBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        PathBox.IsVisible = false;
    }

    private void OnBreadcrumbBackgroundPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            PathBox.IsVisible = true;
            PathBox.Focus();
        }
    }

    private void OnBreadcrumbClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && _viewModel.ActiveTab != null)
        {
            _viewModel.ActiveTab.NavigateToAsync(path);
        }
    }

    private void OnEntryDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.ActiveTab?.SelectedItems.FirstOrDefault() is not FileEntryViewModel entry) return;
        if (entry.IsDirectory)
        {
            _viewModel.ActiveTab.NavigateToAsync(entry.Path);
            return;
        }

        var result = new SafeProcessLauncher().Launch("xdg-open", [entry.Path]);
        if (!result.Succeeded && (Application.Current as App)?.Feedback is { } feedback)
        {
            feedback.Show("DaisyOS could not open that file.");
        }
    }

    private void OnHomeClicked(object? sender, RoutedEventArgs e) =>
        _viewModel.ActiveTab?.NavigateToAsync(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    private void OnDesktopClicked(object? sender, RoutedEventArgs e) =>
        _viewModel.ActiveTab?.NavigateToAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop"));

    private void OnDownloadsClicked(object? sender, RoutedEventArgs e) =>
        _viewModel.ActiveTab?.NavigateToAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));

    private void OnDocumentsClicked(object? sender, RoutedEventArgs e) =>
        _viewModel.ActiveTab?.NavigateToAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents"));

    private void OnPicturesClicked(object? sender, RoutedEventArgs e) =>
        _viewModel.ActiveTab?.NavigateToAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures"));

    private void OnMusicClicked(object? sender, RoutedEventArgs e) =>
        _viewModel.ActiveTab?.NavigateToAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music"));

    private void OnRootClicked(object? sender, RoutedEventArgs e) =>
        _viewModel.ActiveTab?.NavigateToAsync("/");

    private void OnSortNameClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.ActiveTab != null) _viewModel.ActiveTab.SortColumn = "Name";
    }

    private void OnSortDateClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.ActiveTab != null) _viewModel.ActiveTab.SortColumn = "Date";
    }

    private void OnSortTypeClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.ActiveTab != null) _viewModel.ActiveTab.SortColumn = "Type";
    }

    private void OnSortSizeClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.ActiveTab != null) _viewModel.ActiveTab.SortColumn = "Size";
    }

    private void OnToggleViewModeClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.ActiveTab != null)
        {
            _viewModel.ActiveTab.IsGridView = !_viewModel.ActiveTab.IsGridView;
        }
    }

    private async void OnNewFolderClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.ActiveTab is not null)
            await _viewModel.ActiveTab.CreateNewFolderAsync();
    }

    private async void OnDeleteClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.ActiveTab is not null)
            await _viewModel.ActiveTab.DeleteSelectedAsync();
    }

    private async void OnCutClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.ActiveTab is not null)
            await _viewModel.ActiveTab.CutSelectedAsync();
    }

    private async void OnCopyClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.ActiveTab is not null)
            await _viewModel.ActiveTab.CopySelectedAsync();
    }

    private async void OnPasteClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.ActiveTab is not null)
            await _viewModel.ActiveTab.PasteAsync();
    }

    private void OnResizeTopPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.North, e);
    }

    private void OnResizeBottomPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.South, e);
    }

    private void OnResizeLeftPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.West, e);
    }

    private void OnResizeRightPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.East, e);
    }

    private void OnResizeTopLeftPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.NorthWest, e);
    }

    private void OnResizeTopRightPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.NorthEast, e);
    }

    private void OnResizeBottomLeftPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.SouthWest, e);
    }

    private void OnResizeBottomRightPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.SouthEast, e);
    }
}
