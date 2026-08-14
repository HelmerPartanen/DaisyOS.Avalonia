using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Threading;
using DaisyOS.Core.Models;
using DaisyOS.Shell.Services;
using DaisyOS.System.Processes;

namespace DaisyOS.Shell.ViewModels;

public enum LauncherAllAppsViewMode
{
    Alphabetical,
    ByCategory
}

public class LauncherViewModel : INotifyPropertyChanged
{
    private static readonly Dictionary<string, string> CategoryKeywordMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "DEVELOPMENT", "Development" },
        { "PROGRAMMING", "Development" },
        { "IDE", "Development" },
        { "GRAPHICS", "Graphics & Design" },
        { "PHOTOGRAPHY", "Graphics & Design" },
        { "DESIGN", "Graphics & Design" },
        { "OFFICE", "Office & Productivity" },
        { "TEXTEDITOR", "Office & Productivity" },
        { "DOCUMENT", "Office & Productivity" },
        { "NETWORK", "Internet & Network" },
        { "WEB", "Internet & Network" },
        { "INTERNET", "Internet & Network" },
        { "AUDIO", "Sound & Video" },
        { "VIDEO", "Sound & Video" },
        { "PLAYER", "Sound & Video" },
        { "MEDIA", "Sound & Video" },
        { "GAME", "Games" },
        { "SYSTEM", "System Tools" },
        { "TERMINAL", "System Tools" },
        { "FILEMANAGER", "System Tools" },
        { "CORE", "System Tools" },
        { "UTILITY", "Utilities & Tools" },
        { "SETTINGS", "Utilities & Tools" }
    };

    private readonly LinuxAppLauncherService _launcherService;
    private readonly ShellSessionState _sessionState;
    private LauncherAllAppsViewMode _selectedViewMode = LauncherAllAppsViewMode.Alphabetical;
    private string _searchText = string.Empty;
    
    private List<AppGroupViewModel>? _alphabeticalGroupsCache;
    private List<AppGroupViewModel>? _categoryGroupsCache;

    public ObservableCollection<LauncherItemViewModel> MostUsedApps { get; } = new();
    public ObservableCollection<LauncherItemViewModel> AllApps { get; } = new();
    public ObservableCollection<AppGroupViewModel> ActiveGroups { get; } = new();

    public bool IsAlphabeticalMode => _selectedViewMode == LauncherAllAppsViewMode.Alphabetical;
    public bool IsCategoryMode => _selectedViewMode == LauncherAllAppsViewMode.ByCategory;
    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchText);
    public bool ShowNoResults => HasSearchQuery && ActiveGroups.Count == 0;
    public bool HasMostUsedApps => MostUsedApps.Count > 0;
    public bool ShowMostUsedPlaceholder => !HasMostUsedApps;

    public string LibraryTitle => HasSearchQuery
        ? "Search results"
        : IsCategoryMode ? "Browse by category" : "All applications";

    public string LibrarySubtitle => HasSearchQuery
        ? $"Matches for \u201c{SearchText.Trim()}\u201d"
        : "Everything installed on this device";

    public string SelectedViewModeText => _selectedViewMode == LauncherAllAppsViewMode.Alphabetical 
        ? "View: Alphabetical" 
        : "View: Category";

    public LauncherAllAppsViewMode SelectedViewMode
    {
        get => _selectedViewMode;
        set
        {
            if (_selectedViewMode != value)
            {
                _selectedViewMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedViewModeText));
                OnPropertyChanged(nameof(IsAlphabeticalMode));
                OnPropertyChanged(nameof(IsCategoryMode));
                OnPropertyChanged(nameof(LibraryTitle));
                OnPropertyChanged(nameof(LibrarySubtitle));
                UpdateActiveGroups();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_searchText, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _searchText = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSearchQuery));
            OnPropertyChanged(nameof(ShowNoResults));
            OnPropertyChanged(nameof(LibraryTitle));
            OnPropertyChanged(nameof(LibrarySubtitle));
            UpdateActiveGroups();
        }
    }

    public LauncherViewModel(ShellSessionState? sessionState = null)
    {
        _launcherService = new LinuxAppLauncherService();
        _sessionState = sessionState ?? new ShellSessionState();

        _ = LoadApplicationsAsync();
    }

    public void SetViewAlphabetical() => SelectedViewMode = LauncherAllAppsViewMode.Alphabetical;
    public void SetViewCategory() => SelectedViewMode = LauncherAllAppsViewMode.ByCategory;

    private async Task LoadApplicationsAsync()
    {
        try
        {
            var items = await Task.Run(BuildApplicationCatalog).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() => ApplyApplicationCatalog(items));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load launcher applications: {ex.Message}");
        }
    }

    private IReadOnlyList<AppEntry> BuildApplicationCatalog()
    {
        return _launcherService.GetAvailableApps()
            .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private void ApplyApplicationCatalog(IReadOnlyList<AppEntry> apps)
    {
        if (apps.Count == 0)
        {
            return;
        }

        foreach (var app in apps)
        {
            // SvgImage and other Avalonia image sources are UI-thread-affine.
            var iconPath = DesktopItemLoader.ResolveIconPath(app.Icon);
            var iconBitmap = string.IsNullOrEmpty(iconPath) ? null : DesktopItemLoader.LoadBitmapSafe(iconPath);
            AllApps.Add(new LauncherItemViewModel(app, iconBitmap));
        }

        RefreshMostUsedApps();

        // Build group caches once upon loading
        _alphabeticalGroupsCache = AllApps
            .GroupBy(item => GetGroupLetter(item.Name))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new AppGroupViewModel(g.Key, g))
            .ToList();

        _categoryGroupsCache = AllApps
            .GroupBy(item => ResolveCategoryName(item.App.Categories))
            .OrderBy(g => GetCategoryOrder(g.Key))
            .Select(g => new AppGroupViewModel(g.Key, g.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)))
            .ToList();

        UpdateActiveGroups();
    }

    private void UpdateActiveGroups()
    {
        ActiveGroups.Clear();

        var sourceCache = _selectedViewMode == LauncherAllAppsViewMode.Alphabetical
            ? _alphabeticalGroupsCache
            : _categoryGroupsCache;

        if (sourceCache != null)
        {
            foreach (var group in sourceCache)
            {
                var items = group.Items.Where(MatchesSearch).ToList();
                if (items.Count > 0)
                {
                    ActiveGroups.Add(new AppGroupViewModel(group.Header, items));
                }
            }
        }

        OnPropertyChanged(nameof(ShowNoResults));
    }

    private bool MatchesSearch(LauncherItemViewModel item)
    {
        if (!HasSearchQuery)
        {
            return true;
        }

        var query = SearchText.Trim();
        return item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || item.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || item.App.Categories?.Any(category => category.Contains(query, StringComparison.CurrentCultureIgnoreCase)) == true;
    }

    public async Task<AppLaunchResult?> LaunchAppAsync(LauncherItemViewModel? item)
    {
        if (item == null) return null;
        try
        {
            return await _launcherService.LaunchAsync(item.App);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to launch app {item.Name}: {ex.Message}");
            return new AppLaunchResult(false, $"{item.Name} could not be opened.");
        }
    }

    public void RecordSuccessfulLaunch(LauncherItemViewModel item)
    {
        _sessionState.RecordApplicationLaunch(item.App.Id);
        RefreshMostUsedApps();
    }

    private void RefreshMostUsedApps()
    {
        MostUsedApps.Clear();
        var appsById = AllApps.ToDictionary(item => item.App.Id, StringComparer.Ordinal);

        foreach (var applicationId in _sessionState.GetMostUsedApplicationIds(10))
        {
            if (appsById.TryGetValue(applicationId, out var item))
            {
                MostUsedApps.Add(item);
            }
        }

        OnPropertyChanged(nameof(HasMostUsedApps));
        OnPropertyChanged(nameof(ShowMostUsedPlaceholder));
    }

    private static string GetGroupLetter(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "#";
        var first = char.ToUpperInvariant(name[0]);
        return char.IsLetter(first) ? first.ToString() : "#";
    }

    private static string ResolveCategoryName(IReadOnlyList<string>? categories)
    {
        if (categories == null || categories.Count == 0) return "Utilities & Tools";

        foreach (var cat in categories)
        {
            if (string.IsNullOrWhiteSpace(cat)) continue;
            
            // Fast lookup in static dictionary
            foreach (var kvp in CategoryKeywordMap)
            {
                if (cat.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }
        }

        return "Utilities & Tools";
    }

    private static int GetCategoryOrder(string catName) => catName switch
    {
        "Office & Productivity" => 1,
        "Internet & Network" => 2,
        "Development" => 3,
        "Graphics & Design" => 4,
        "Sound & Video" => 5,
        "Games" => 6,
        "System Tools" => 7,
        "Utilities & Tools" => 8,
        _ => 9
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
