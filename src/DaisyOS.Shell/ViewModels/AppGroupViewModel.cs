using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DaisyOS.Shell.ViewModels;

public class AppGroupViewModel
{
    public string Header { get; }
    public ObservableCollection<LauncherItemViewModel> Items { get; }

    public AppGroupViewModel(string header, IEnumerable<LauncherItemViewModel> items)
    {
        Header = header;
        Items = new ObservableCollection<LauncherItemViewModel>(items);
    }
}
