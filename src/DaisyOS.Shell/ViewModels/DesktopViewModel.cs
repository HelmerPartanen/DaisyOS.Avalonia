using System;
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

    /// <summary>
    /// True only for the item currently being dragged by the pointer.
    /// Used purely for view styling - it opts this item out of the Canvas.Left/Top
    /// transition so it can track the pointer directly instead of easing toward it.
    /// </summary>
    private bool _isDragging;
    public bool IsDragging
    {
        get => _isDragging;
        set => SetField(ref _isDragging, value);
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
    
    private DesktopItemViewModel?[]? _dragCache;
    private DesktopGridMetrics? _dragMetrics;

    public void BeginDrag(DesktopItemViewModel draggedItem, DesktopGridMetrics metrics)
    {
        _dragMetrics = metrics;
        int maxSlots = metrics.ColumnCount * metrics.RowCount;
        _dragCache = new DesktopItemViewModel?[maxSlots];
        
        foreach (var item in Items)
        {
            if (item != draggedItem)
            {
                int slot = metrics.GetSlotIndex(new GridCell(item.State.Column, item.State.Row));
                if (slot >= 0 && slot < maxSlots)
                {
                    _dragCache[slot] = item;
                }
            }
        }
    }

    public void EndDrag()
    {
        _dragCache = null;
        _dragMetrics = null;
    }

    private static void PreviewPlaceAt(DesktopItemViewModel item, int slot, DesktopGridMetrics metrics)
    {
        var cell = metrics.GetCellForSlot(slot);
        var origin = metrics.GetCellOrigin(cell);
        item.X = origin.X;
        item.Y = origin.Y;
    }

    public void PreviewReflow(DesktopItemViewModel draggedItem, int targetSlot, DesktopGridMetrics metrics)
    {
        if (_dragCache == null) return;
        
        int maxSlots = _dragCache.Length;
        int currentSlot = Math.Max(0, Math.Min(targetSlot, maxSlots - 1));
        
        int chainEnd = currentSlot;
        while (chainEnd < maxSlots && _dragCache[chainEnd] != null)
        {
            chainEnd++;
        }
        
        for (int i = 0; i < maxSlots; i++)
        {
            var item = _dragCache[i];
            if (item == null) continue;
            
            int newSlot = (i >= currentSlot && i < chainEnd) ? i + 1 : i;
            if (newSlot < maxSlots)
            {
                PreviewPlaceAt(item, newSlot, metrics);
            }
        }
    }

    public void CommitItemMove(DesktopItemViewModel item, int targetSlot, DesktopGridMetrics metrics)
    {
        if (_dragCache == null) return;
        
        int maxSlots = _dragCache.Length;
        int currentSlot = Math.Max(0, Math.Min(targetSlot, maxSlots - 1));
        
        int chainEnd = currentSlot;
        while (chainEnd < maxSlots && _dragCache[chainEnd] != null)
        {
            chainEnd++;
        }
        
        for (int i = 0; i < maxSlots; i++)
        {
            var other = _dragCache[i];
            if (other == null) continue;
            
            int newSlot = (i >= currentSlot && i < chainEnd) ? i + 1 : i;
            if (newSlot < maxSlots)
            {
                PlaceAt(other, newSlot, metrics);
            }
        }
        
        PlaceAt(item, currentSlot, metrics);
        EndDrag();
    }

    private static void PlaceAt(DesktopItemViewModel item, int slot, DesktopGridMetrics metrics)
    {
        var cell = metrics.GetCellForSlot(slot);
        item.State.Column = cell.Column;
        item.State.Row = cell.Row;

        var origin = metrics.GetCellOrigin(cell);
        item.X = origin.X;
        item.Y = origin.Y;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}