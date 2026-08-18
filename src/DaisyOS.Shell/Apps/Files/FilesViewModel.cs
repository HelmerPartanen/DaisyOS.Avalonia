using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DaisyOS.Shell.Apps.Files;

public sealed class FilesViewModel : INotifyPropertyChanged
{
    private FilesTabViewModel? _activeTab;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<FilesTabViewModel> Tabs { get; } = [];

    public FilesTabViewModel? ActiveTab
    {
        get => _activeTab;
        set
        {
            if (SetProperty(ref _activeTab, value))
            {
                foreach (var tab in Tabs)
                {
                    tab.IsActive = (tab == value);
                }
            }
        }
    }

    public FilesViewModel(string initialPath)
    {
        NewTab(initialPath);
    }

    public FilesTabViewModel NewTab(string? path = null)
    {
        path ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var tab = new FilesTabViewModel(path);
        Tabs.Add(tab);
        ActiveTab = tab;
        return tab;
    }

    public void CloseTab(FilesTabViewModel tab)
    {
        if (Tabs.Contains(tab))
        {
            int index = Tabs.IndexOf(tab);
            Tabs.Remove(tab);

            if (Tabs.Count == 0)
            {
                NewTab();
            }
            else
            {
                ActiveTab = Tabs[Math.Min(index, Tabs.Count - 1)];
            }
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
