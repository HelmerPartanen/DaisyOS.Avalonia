using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DaisyOS.Core.Desktop;

namespace DaisyOS.Shell.ViewModels;

public class DesktopItemViewModel : INotifyPropertyChanged
{
    public DesktopItemState State { get; }
    
    private string _label = string.Empty;
    public string Label
    {
        get => _label;
        set => SetField(ref _label, value);
    }
    
    private string _iconPath = string.Empty;
    public string IconPath
    {
        get => _iconPath;
        set
        {
            if (SetField(ref _iconPath, value))
            {
                IconBitmap = DesktopItemLoader.LoadBitmapSafe(value);
            }
        }
    }
    
    private Avalonia.Media.IImage? _iconBitmap;
    public Avalonia.Media.IImage? IconBitmap
    {
        get => _iconBitmap;
        set => SetField(ref _iconBitmap, value);
    }

    private double _x;
    public double X
    {
        get => _x;
        set => SetField(ref _x, value);
    }

    private double _y;
    public double Y
    {
        get => _y;
        set => SetField(ref _y, value);
    }

    public DesktopItemViewModel(DesktopItemState state)
    {
        State = state;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public class DesktopViewModel : INotifyPropertyChanged
{
    public ObservableCollection<DesktopItemViewModel> Items { get; } = new();

    public DesktopViewModel()
    {
        LoadDesktopItems();
    }

    private void LoadDesktopItems()
    {
        var desktopPath = global::System.Environment.GetFolderPath(global::System.Environment.SpecialFolder.Desktop);
        if (!global::System.IO.Directory.Exists(desktopPath)) return;

        int index = 0;
        foreach (var file in global::System.IO.Directory.GetFiles(desktopPath, "*.desktop"))
        {
            var parsed = DesktopItemLoader.ParseDesktopFile(file);
            Items.Add(new DesktopItemViewModel(new DesktopItemState(file, "primary", 0, 0, index++))
            {
                Label = parsed.name,
                IconPath = string.IsNullOrWhiteSpace(parsed.iconPath) ? "avares://DaisyOS.Shell/Assets/AppIcons/FilesIcon.png" : parsed.iconPath
            });
        }
    }

    public void CalculateLayout(DesktopGridMetrics metrics)
    {
        var map = new DesktopOccupancyMap(metrics);
        
        // Auto-arrange enabled by default for this example.
        // We extract the states, run AutoArrange, and update positions.
        var states = new List<DesktopItemState>();
        foreach (var item in Items)
        {
            states.Add(item.State);
        }
        
        var newStates = map.AutoArrange(states);
        var stateDict = new Dictionary<string, DesktopItemState>();
        foreach(var state in newStates)
        {
            stateDict[state.Id] = state;
        }

        foreach (var item in Items)
        {
            if (stateDict.TryGetValue(item.State.Id, out var newState))
            {
                item.State.Column = newState.Column;
                item.State.Row = newState.Row;
                item.State.MonitorId = newState.MonitorId;
                
                var cellOrigin = metrics.GetCellOrigin(new GridCell(item.State.Column, item.State.Row));
                item.X = cellOrigin.X;
                item.Y = cellOrigin.Y;
            }
        }
    }
    
    public void CommitItemMove(DesktopItemViewModel item, GridCell nearestCell, DesktopGridMetrics metrics)
    {
        var map = new DesktopOccupancyMap(metrics);
        
        // Populate current occupancy for ALL items EXCEPT the one being dragged
        foreach (var other in Items)
        {
            if (other == item) continue;
            // Assuming all other items are in valid positions for now
            map.PlaceItem(other.State.Id, new GridCell(other.State.Column, other.State.Row));
        }
        
        // Now resolve the dragged item's placement using the map
        var resolvedCell = map.PlaceItem(item.State.Id, nearestCell);
        
        // Update state
        item.State.Column = resolvedCell.Column;
        item.State.Row = resolvedCell.Row;
        
        // Update pixel position
        var cellOrigin = metrics.GetCellOrigin(resolvedCell);
        item.X = cellOrigin.X;
        item.Y = cellOrigin.Y;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
