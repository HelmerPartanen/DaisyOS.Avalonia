using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Avalonia.Threading;
using DaisyOS.Shell.Apps.Files.Services;
using Directory = System.IO.Directory;
using File = System.IO.File;
using Path = System.IO.Path;

namespace DaisyOS.Shell.Apps.Files;

public record BreadcrumbSegment(string Name, string Path);

public sealed class FilesTabViewModel : INotifyPropertyChanged
{
    private string _currentPath = string.Empty;
    private string _title = "New Tab";
    private string _searchQuery = string.Empty;
    private string _sortColumn = "Name";
    private bool _sortAscending = true;
    private bool _isGridView;
    private bool _isActive;
    private bool _isLoading;
    private bool _showHiddenFiles;
    private string _statusText = string.Empty;

    private CancellationTokenSource? _loadCts;

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                UpdateStatusText();
            }
        }
    }

    private readonly Stack<string> _history = new();
    private readonly Stack<string> _forwardHistory = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<FileEntryViewModel> Entries { get; } = new();
    public ObservableCollection<FileEntryViewModel> FilteredEntries { get; } = new();
    
    public ObservableCollection<FileEntryViewModel> SelectedItems { get; } = new();
    public ObservableCollection<BreadcrumbSegment> Breadcrumbs { get; } = new();

    public string CurrentPath
    {
        get => _currentPath;
        private set
        {
            if (SetProperty(ref _currentPath, value))
            {
                var folderName = Path.GetFileName(value);
                Title = string.IsNullOrEmpty(folderName) ? value : folderName;
                UpdateBreadcrumbs();
                OnPropertyChanged(nameof(CanGoUp));
            }
        }
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyFilterAndSort();
            }
        }
    }

    public string SortColumn
    {
        get => _sortColumn;
        set
        {
            if (_sortColumn == value)
            {
                SortAscending = !SortAscending;
            }
            else
            {
                _sortColumn = value;
                _sortAscending = true;
                OnPropertyChanged(nameof(SortColumn));
            }
            ApplyFilterAndSort();
        }
    }

    public bool SortAscending
    {
        get => _sortAscending;
        set
        {
            if (SetProperty(ref _sortAscending, value))
            {
                ApplyFilterAndSort();
            }
        }
    }

    public bool IsGridView
    {
        get => _isGridView;
        set => SetProperty(ref _isGridView, value);
    }

    public bool ShowHiddenFiles
    {
        get => _showHiddenFiles;
        set
        {
            if (SetProperty(ref _showHiddenFiles, value))
            {
                ApplyFilterAndSort();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool CanGoBack => _history.Count > 0;
    public bool CanGoForward => _forwardHistory.Count > 0;
    public bool CanGoUp => !string.IsNullOrEmpty(CurrentPath) && Directory.GetParent(CurrentPath) != null;

    public FilesTabViewModel(string initialPath)
    {
        SelectedItems.CollectionChanged += (s, e) => UpdateStatusText();
        NavigateToAsync(initialPath, addHistory: false);
    }

    private void UpdateBreadcrumbs()
    {
        Breadcrumbs.Clear();
        if (string.IsNullOrEmpty(CurrentPath)) return;

        var parts = CurrentPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        
        string currentBuiltPath = "/";
        Breadcrumbs.Add(new BreadcrumbSegment("Root", currentBuiltPath));

        foreach (var part in parts)
        {
            currentBuiltPath = Path.Combine(currentBuiltPath, part);
            Breadcrumbs.Add(new BreadcrumbSegment(part, currentBuiltPath));
        }
    }

    public async void NavigateToAsync(string path, bool addHistory = true)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                StatusText = "Folder does not exist.";
                return;
            }

            if (addHistory && !string.Equals(fullPath, CurrentPath, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(CurrentPath)) _history.Push(CurrentPath);
                _forwardHistory.Clear();
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoForward));
            }

            CurrentPath = fullPath;
            
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            IsLoading = true;
            Entries.Clear();
            FilteredEntries.Clear();
            SelectedItems.Clear();

            await Task.Run(() =>
            {
                try
                {
                    var items = Directory.EnumerateFileSystemEntries(fullPath).ToList();
                    
                    const int ChunkSize = 50;
                    for (int i = 0; i < items.Count; i += ChunkSize)
                    {
                        if (token.IsCancellationRequested) break;

                        var chunk = items.Skip(i).Take(ChunkSize).Select(p => new FileEntryViewModel(p)).ToList();
                        
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (token.IsCancellationRequested) return;
                            foreach (var item in chunk) Entries.Add(item);
                            ApplyFilterAndSort(); 
                        });
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Dispatcher.UIThread.Post(() => StatusText = "Permission denied.");
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() => StatusText = $"Error: {ex.Message}");
                }
            }, token);

            if (!token.IsCancellationRequested)
            {
                IsLoading = false;
                Dispatcher.UIThread.Post(ApplyFilterAndSort);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Navigation Error: {ex.Message}";
            IsLoading = false;
        }
    }

    public void GoBack()
    {
        if (_history.TryPop(out var previousPath))
        {
            _forwardHistory.Push(CurrentPath);
            NavigateToAsync(previousPath, addHistory: false);
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
        }
    }

    public void GoForward()
    {
        if (_forwardHistory.TryPop(out var nextPath))
        {
            _history.Push(CurrentPath);
            NavigateToAsync(nextPath, addHistory: false);
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
        }
    }

    public void GoUp()
    {
        var parent = Directory.GetParent(CurrentPath);
        if (parent != null)
        {
            NavigateToAsync(parent.FullName);
        }
    }

    public void Refresh() => NavigateToAsync(CurrentPath, addHistory: false);

    public async Task CreateNewFolderAsync()
    {
        string newFolderPath = "New Folder";
        int count = 1;
        while (Directory.Exists(Path.Combine(CurrentPath, newFolderPath)))
        {
            newFolderPath = $"New Folder ({count++})";
        }
        
        if (await FileSystemService.CreateFolderAsync(CurrentPath, newFolderPath))
        {
            Refresh();
        }
    }

    public async Task DeleteSelectedAsync()
    {
        var items = SelectedItems.ToList();
        foreach (var item in items)
        {
            await FileSystemService.MoveToTrashAsync(item.Path);
        }
        Refresh();
    }

    public async Task CopySelectedAsync()
    {
        var paths = SelectedItems.Select(x => x.Path).ToList();
        if (paths.Any())
        {
            await ClipboardService.SetFilesAsync(paths, ClipboardOperation.Copy);
        }
    }

    public async Task CutSelectedAsync()
    {
        var paths = SelectedItems.Select(x => x.Path).ToList();
        if (paths.Any())
        {
            await ClipboardService.SetFilesAsync(paths, ClipboardOperation.Cut);
        }
    }

    public async Task PasteAsync()
    {
        var (files, op) = await ClipboardService.GetFilesAsync();
        if (!files.Any()) return;

        foreach (var file in files)
        {
            var destPath = Path.Combine(CurrentPath, Path.GetFileName(file));
            if (op == ClipboardOperation.Cut)
            {
                await FileSystemService.MoveAsync(file, destPath, async (path) => ConflictResolution.Replace);
            }
            else
            {
                await FileSystemService.CopyAsync(file, destPath, async (path) => ConflictResolution.Replace);
            }
        }
        
        if (op == ClipboardOperation.Cut)
        {
            ClipboardService.ClearInternal();
        }
        Refresh();
    }

    public void ApplyFilterAndSort()
    {
        IEnumerable<FileEntryViewModel> query = Entries;

        if (!ShowHiddenFiles)
        {
            query = query.Where(e => !e.IsHidden);
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            query = query.Where(e => e.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
        }

        query = SortColumn switch
        {
            "Date" => SortAscending
                ? query.OrderBy(e => !e.IsDirectory).ThenBy(e => e.LastWriteTime)
                : query.OrderBy(e => !e.IsDirectory).ThenByDescending(e => e.LastWriteTime),
            "Type" => SortAscending
                ? query.OrderBy(e => !e.IsDirectory).ThenBy(e => e.DisplayType)
                : query.OrderBy(e => !e.IsDirectory).ThenByDescending(e => e.DisplayType),
            "Size" => SortAscending
                ? query.OrderBy(e => !e.IsDirectory).ThenBy(e => e.Length)
                : query.OrderBy(e => !e.IsDirectory).ThenByDescending(e => e.Length),
            _ => SortAscending
                ? query.OrderBy(e => !e.IsDirectory).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                : query.OrderBy(e => !e.IsDirectory).ThenByDescending(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
        };

        FilteredEntries.Clear();
        foreach (var item in query)
        {
            FilteredEntries.Add(item);
        }

        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        if (IsLoading)
        {
            StatusText = "Loading...";
            return;
        }

        int count = FilteredEntries.Count;
        string itemsText = count == 1 ? "1 item" : $"{count} items";

        if (SelectedItems.Count > 0)
        {
            if (SelectedItems.Count == 1)
            {
                var sel = SelectedItems[0];
                string detail = sel.IsDirectory ? "Folder" : sel.DisplaySize;
                StatusText = $"{itemsText}  |  Selected: {sel.Name} ({detail})";
            }
            else
            {
                long totalSize = SelectedItems.Where(i => !i.IsDirectory).Sum(i => i.Length);
                string sizeStr = FormatFileSize(totalSize);
                StatusText = $"{SelectedItems.Count} items selected • {sizeStr}";
            }
        }
        else
        {
            StatusText = itemsText;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
