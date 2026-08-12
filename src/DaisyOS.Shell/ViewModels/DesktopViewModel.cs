using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DaisyOS.Core.Desktop;

namespace DaisyOS.Shell.ViewModels;

public class DesktopItemViewModel : INotifyPropertyChanged
{
    public DesktopItemId Id { get; }
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

    private bool _isDragging;
    public bool IsDragging
    {
        get => _isDragging;
        set => SetField(ref _isDragging, value);
    }

    private bool _isHiddenPlaceholder;
    public bool IsHiddenPlaceholder
    {
        get => _isHiddenPlaceholder;
        set => SetField(ref _isHiddenPlaceholder, value);
    }

    public DesktopItemViewModel(DesktopItemState state, DesktopItemId? id = null)
    {
        State = state;
        Id = id ?? (string.IsNullOrEmpty(state.Id) ? DesktopItemId.NewId() : new DesktopItemId(state.Id));
        state.Id = Id.Value;
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

    private readonly Dictionary<DesktopItemId, DesktopItemViewModel> _itemMap = new();
    private readonly Dictionary<DesktopItemId, GridCell> _committedCells = new();

    public DesktopViewModel()
    {
        LoadDesktopItems();
    }

    private void LoadDesktopItems()
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (!Directory.Exists(desktopPath)) return;

        int index = 0;
        foreach (var file in Directory.GetFiles(desktopPath, "*.desktop"))
        {
            var parsed = DesktopItemLoader.ParseDesktopFile(file);
            var item = new DesktopItemViewModel(new DesktopItemState(file, "primary", 0, 0, index++))
            {
                Label = parsed.name,
                IconPath = string.IsNullOrWhiteSpace(parsed.iconPath) ? "avares://DaisyOS.Shell/Assets/AppIcons/FilesIcon.png" : parsed.iconPath
            };
            AddItem(item);
        }
    }

    public void AddItem(DesktopItemViewModel item)
    {
        if (_itemMap.ContainsKey(item.Id)) return;
        _itemMap[item.Id] = item;
        Items.Add(item);
        var cell = new GridCell(item.State.Column, item.State.Row);
        _committedCells[item.Id] = cell;
    }

    public DesktopItemViewModel? GetItem(DesktopItemId id)
    {
        return _itemMap.TryGetValue(id, out var item) ? item : null;
    }

    public GridCell GetCommittedCell(DesktopItemId id)
    {
        return _committedCells.TryGetValue(id, out var cell) ? cell : new GridCell(0, 0);
    }

    public void ClearItems()
    {
        Items.Clear();
        _itemMap.Clear();
        _committedCells.Clear();
    }

    public int GetCommittedIndex(DesktopItemId id)
    {
        var keys = _committedCells.Keys.ToList();
        return keys.IndexOf(id);
    }

    public void CalculateLayout(DesktopGridMetrics metrics)
    {
        var map = new DesktopOccupancyMap(metrics);
        var states = Items.Select(i => i.State).ToList();
        var newStates = map.AutoArrange(states);

        _committedCells.Clear();

        foreach (var state in newStates)
        {
            var item = Items.FirstOrDefault(i => i.State.Id == state.Id);
            if (item != null)
            {
                item.State.Column = state.Column;
                item.State.Row = state.Row;
                item.State.MonitorId = state.MonitorId;

                var cell = new GridCell(state.Column, state.Row);
                _committedCells[item.Id] = cell;

                var cellOrigin = metrics.GetCellOrigin(cell);
                item.X = cellOrigin.X;
                item.Y = cellOrigin.Y;
            }
        }
    }

    public static void MoveItem<T>(IList<T> list, int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex || fromIndex < 0 || toIndex < 0 || fromIndex >= list.Count || toIndex >= list.Count)
            return;
        var item = list[fromIndex];
        list.RemoveAt(fromIndex);
        list.Insert(toIndex, item);
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