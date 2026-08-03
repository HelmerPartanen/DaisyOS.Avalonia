using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DaisyOS.Core.Models;
using DaisyOS.System.Processes;

namespace DaisyOS.Shell.ViewModels;

public enum LauncherAllAppsViewMode
{
    Alphabetical,
    ByCategory
}

public class LauncherViewModel : INotifyPropertyChanged
{
    private readonly LinuxAppLauncherService _launcherService;
    private LauncherAllAppsViewMode _selectedViewMode = LauncherAllAppsViewMode.Alphabetical;

    public string Username { get; }
    public string UserInitials { get; }
    public ObservableCollection<LauncherItemViewModel> PinnedApps { get; } = new();
    public ObservableCollection<LauncherItemViewModel> AllApps { get; } = new();
    public ObservableCollection<AppGroupViewModel> ActiveGroups { get; } = new();

    public bool IsAlphabeticalMode => _selectedViewMode == LauncherAllAppsViewMode.Alphabetical;
    public bool IsCategoryMode => _selectedViewMode == LauncherAllAppsViewMode.ByCategory;

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
                UpdateActiveGroups();
            }
        }
    }

    public LauncherViewModel()
    {
        _launcherService = new LinuxAppLauncherService();

        var rawUser = Environment.UserName;
        if (string.IsNullOrWhiteSpace(rawUser))
        {
            rawUser = Environment.GetEnvironmentVariable("USER") ?? "User";
        }

        Username = FormatUsername(rawUser);
        UserInitials = GetInitials(Username);

        LoadApplications();
    }

    public void SetViewAlphabetical() => SelectedViewMode = LauncherAllAppsViewMode.Alphabetical;
    public void SetViewCategory() => SelectedViewMode = LauncherAllAppsViewMode.ByCategory;

    private void LoadApplications()
    {
        var discoveredApps = _launcherService.GetAvailableApps();

        var sortedApps = discoveredApps
            .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        foreach (var app in sortedApps)
        {
            var iconPath = DesktopItemLoader.ResolveIconPath(app.Icon);
            var iconBitmap = string.IsNullOrEmpty(iconPath) ? null : DesktopItemLoader.LoadBitmapSafe(iconPath);

            var item = new LauncherItemViewModel(app, iconBitmap);
            AllApps.Add(item);
        }

        var pinnedList = AllApps
            .Where(item => item.IconBitmap != null)
            .Take(12)
            .ToList();

        if (pinnedList.Count < 6)
        {
            pinnedList = AllApps.Take(12).ToList();
        }

        foreach (var item in pinnedList)
        {
            PinnedApps.Add(item);
        }

        UpdateActiveGroups();
    }

    private void UpdateActiveGroups()
    {
        ActiveGroups.Clear();

        if (_selectedViewMode == LauncherAllAppsViewMode.Alphabetical)
        {
            var letterGroups = AllApps
                .GroupBy(item => GetGroupLetter(item.Name))
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new AppGroupViewModel(g.Key, g));

            foreach (var group in letterGroups)
            {
                ActiveGroups.Add(group);
            }
        }
        else
        {
            var catGroups = AllApps
                .GroupBy(item => ResolveCategoryName(item.App.Categories))
                .OrderBy(g => GetCategoryOrder(g.Key))
                .Select(g => new AppGroupViewModel(g.Key, g.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)));

            foreach (var group in catGroups)
            {
                ActiveGroups.Add(group);
            }
        }
    }

    public void LaunchApp(LauncherItemViewModel? item)
    {
        if (item == null) return;
        try
        {
            _launcherService.LaunchAsync(item.App);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to launch app {item.Name}: {ex.Message}");
        }
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
            var c = cat.ToUpperInvariant();
            if (c.Contains("DEVELOPMENT") || c.Contains("PROGRAMMING") || c.Contains("IDE")) return "Development";
            if (c.Contains("GRAPHICS") || c.Contains("PHOTOGRAPHY") || c.Contains("DESIGN")) return "Graphics & Design";
            if (c.Contains("OFFICE") || c.Contains("TEXTEDITOR") || c.Contains("DOCUMENT")) return "Office & Productivity";
            if (c.Contains("NETWORK") || c.Contains("WEB") || c.Contains("INTERNET")) return "Internet & Network";
            if (c.Contains("AUDIO") || c.Contains("VIDEO") || c.Contains("PLAYER") || c.Contains("MEDIA")) return "Sound & Video";
            if (c.Contains("GAME")) return "Games";
            if (c.Contains("SYSTEM") || c.Contains("TERMINAL") || c.Contains("FILEMANAGER") || c.Contains("CORE")) return "System Tools";
            if (c.Contains("UTILITY") || c.Contains("SETTINGS")) return "Utilities & Tools";
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

    private static string FormatUsername(string user)
    {
        if (string.IsNullOrWhiteSpace(user)) return "User";
        return char.ToUpper(user[0]) + user[1..];
    }

    private static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "U";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[1][0])}";
        }
        return name.Length >= 2 ? name[..2].ToUpperInvariant() : name[..1].ToUpperInvariant();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
