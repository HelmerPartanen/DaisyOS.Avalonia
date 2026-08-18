using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Directory = global::System.IO.Directory;
using File = global::System.IO.File;
using Path = global::System.IO.Path;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DaisyOS.Shell.Apps.Files;

public sealed class FilesTabViewModel : INotifyPropertyChanged
{
    private string _currentPath = string.Empty;
    private string _title = "New Tab";
    private string _searchQuery = string.Empty;
    private string _sortColumn = "Name";
    private bool _sortAscending = true;
    private bool _isGridView;
    private bool _isActive;
    private FileEntryViewModel? _selectedItem;
    private string _statusText = string.Empty;

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    private readonly Stack<string> _history = new();
    private readonly Stack<string> _forwardHistory = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<FileEntryViewModel> Entries { get; } = [];
    public ObservableCollection<FileEntryViewModel> FilteredEntries { get; } = [];

    public string CurrentPath
    {
        get => _currentPath;
        private set
        {
            if (SetProperty(ref _currentPath, value))
            {
                var folderName = global::System.IO.Path.GetFileName(value);
                Title = string.IsNullOrEmpty(folderName) ? value : folderName;
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

    public FileEntryViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                UpdateStatusText();
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
        NavigateTo(initialPath, addHistory: false);
    }

    public void NavigateTo(string path, bool addHistory = true)
    {
        try
        {
            var fullPath = global::System.IO.Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                StatusText = "Folder does not exist.";
                return;
            }

            if (addHistory && !string.Equals(fullPath, CurrentPath, StringComparison.OrdinalIgnoreCase))
            {
                _history.Push(CurrentPath);
                _forwardHistory.Clear();
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoForward));
            }

            CurrentPath = fullPath;
            Entries.Clear();

            try
            {
                foreach (var entryPath in Directory.EnumerateFileSystemEntries(fullPath))
                {
                    Entries.Add(new FileEntryViewModel(entryPath));
                }
            }
            catch (UnauthorizedAccessException)
            {
                StatusText = "Permission denied.";
            }

            ApplyFilterAndSort();
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    public void GoBack()
    {
        if (_history.TryPop(out var previousPath))
        {
            _forwardHistory.Push(CurrentPath);
            NavigateTo(previousPath, addHistory: false);
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
        }
    }

    public void GoForward()
    {
        if (_forwardHistory.TryPop(out var nextPath))
        {
            _history.Push(CurrentPath);
            NavigateTo(nextPath, addHistory: false);
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
        }
    }

    public void GoUp()
    {
        var parent = Directory.GetParent(CurrentPath);
        if (parent != null)
        {
            NavigateTo(parent.FullName);
        }
    }

    public void Refresh() => NavigateTo(CurrentPath, addHistory: false);

    public void ApplyFilterAndSort()
    {
        IEnumerable<FileEntryViewModel> query = Entries;

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            query = query.Where(e => e.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
        }

        // Always put directories first, then apply column sort
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
        int count = FilteredEntries.Count;
        string itemsText = count == 1 ? "1 item" : $"{count} items";

        if (SelectedItem != null)
        {
            string detail = SelectedItem.IsDirectory ? "Folder" : SelectedItem.DisplaySize;
            StatusText = $"{itemsText}  |  Selected: {SelectedItem.Name} ({detail})";
        }
        else
        {
            StatusText = itemsText;
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
