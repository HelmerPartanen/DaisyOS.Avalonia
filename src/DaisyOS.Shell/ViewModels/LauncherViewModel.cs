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

public class LauncherViewModel : INotifyPropertyChanged
{
    private readonly LinuxAppLauncherService _launcherService;
    private readonly ShellSessionState _sessionState;
    private string _searchText = string.Empty;

    public ObservableCollection<LauncherItemViewModel> AllApps { get; } = new();
    public ObservableCollection<LauncherItemViewModel> FilteredApps { get; } = new();

    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchText);
    public bool ShowNoResults => HasSearchQuery && FilteredApps.Count == 0;

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
            UpdateFilteredApps();
        }
    }

    public LauncherViewModel(ShellSessionState? sessionState = null)
    {
        _launcherService = new LinuxAppLauncherService();
        _sessionState = sessionState ?? new ShellSessionState();

        _ = LoadApplicationsAsync();
    }

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
            var iconPath = DesktopItemLoader.ResolveIconPath(app.Icon);
            var iconBitmap = string.IsNullOrEmpty(iconPath) ? null : DesktopItemLoader.LoadBitmapSafe(iconPath);
            AllApps.Add(new LauncherItemViewModel(app, iconBitmap));
        }

        UpdateFilteredApps();
    }

    private void UpdateFilteredApps()
    {
        FilteredApps.Clear();

        var sorted = AllApps.Where(MatchesSearch).OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase);
        foreach (var app in sorted)
        {
            FilteredApps.Add(app);
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
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
