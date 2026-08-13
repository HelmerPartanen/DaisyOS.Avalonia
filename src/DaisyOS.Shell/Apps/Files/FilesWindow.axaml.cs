using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DaisyOS.System.Processes;

namespace DaisyOS.Shell.Apps.Files;

public partial class FilesWindow : Window
{
    private readonly ObservableCollection<FileEntry> _entries = [];
    private readonly Stack<string> _history = new();
    private string _currentPath;

    public FilesWindow() : this(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) { }

    internal FilesWindow(string initialPath)
    {
        InitializeComponent();
        EntriesList.ItemsSource = _entries;
        _currentPath = initialPath;
        NavigateTo(initialPath, addHistory: false);
    }

    private void NavigateTo(string path, bool addHistory = true)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                StatusText.Text = "That folder is not available.";
                return;
            }

            if (addHistory && !string.Equals(fullPath, _currentPath, StringComparison.Ordinal)) _history.Push(_currentPath);
            _currentPath = fullPath;
            PathBox.Text = fullPath;
            _entries.Clear();
            foreach (var entry in Directory.EnumerateFileSystemEntries(fullPath)
                         .OrderBy(entry => !Directory.Exists(entry))
                         .ThenBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase))
            {
                _entries.Add(new FileEntry(entry, Path.GetFileName(entry), Directory.Exists(entry)));
            }
            StatusText.Text = $"{_entries.Count} {(_entries.Count == 1 ? "item" : "items")}";
        }
        catch (UnauthorizedAccessException)
        {
            StatusText.Text = "DaisyOS does not have permission to open that folder.";
        }
        catch (IOException)
        {
            StatusText.Text = "This folder could not be read.";
        }
    }

    private void OnEntryDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (EntriesList.SelectedItem is not FileEntry entry) return;
        if (entry.IsDirectory)
        {
            NavigateTo(entry.Path);
            return;
        }

        var result = new SafeProcessLauncher().Launch("xdg-open", [entry.Path]);
        if (!result.Succeeded) StatusText.Text = "DaisyOS could not open that file with its default application.";
    }

    private void OnPathKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        NavigateTo(PathBox.Text ?? _currentPath);
        e.Handled = true;
    }

    private void OnBackClicked(object? sender, RoutedEventArgs e)
    {
        if (_history.TryPop(out var path)) NavigateTo(path, addHistory: false);
    }

    private void OnRefreshClicked(object? sender, RoutedEventArgs e) => NavigateTo(_currentPath, addHistory: false);
    private void OnHomeClicked(object? sender, RoutedEventArgs e) => NavigateTo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    private void OnDesktopClicked(object? sender, RoutedEventArgs e) => NavigateTo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop"));
    private void OnDownloadsClicked(object? sender, RoutedEventArgs e) => NavigateTo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
    private void OnDocumentsClicked(object? sender, RoutedEventArgs e) => NavigateTo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents"));

    private sealed record FileEntry(string Path, string Name, bool IsDirectory)
    {
        public string Icon => IsDirectory ? "folder" : "description";
        public string Detail => IsDirectory ? "Folder" : "File";
    }
}
