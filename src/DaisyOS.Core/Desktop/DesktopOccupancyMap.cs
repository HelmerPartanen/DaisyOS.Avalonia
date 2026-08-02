using System;
using System.Collections.Generic;
using System.Linq;

namespace DaisyOS.Core.Desktop;

/// <summary>
/// Maintains an occupancy map for a specific monitor's grid and provides collision resolution and auto-arrange logic.
/// </summary>
public class DesktopOccupancyMap
{
    private readonly DesktopGridMetrics _metrics;
    private readonly string?[,] _occupied; // stores item IDs

    public DesktopOccupancyMap(DesktopGridMetrics metrics)
    {
        _metrics = metrics;
        _occupied = new string?[metrics.ColumnCount, metrics.RowCount];
    }

    /// <summary>
    /// Checks if a given cell is occupied.
    /// </summary>
    public bool IsOccupied(GridCell cell)
    {
        if (!_metrics.IsValid(cell)) return true; // Treat out of bounds as occupied
        return _occupied[cell.Column, cell.Row] != null;
    }

    /// <summary>
    /// Attempts to place an item at the target cell. If it is occupied, it resolves the collision using Manhattan distance.
    /// </summary>
    /// <returns>The final resolved cell.</returns>
    public GridCell PlaceItem(string itemId, GridCell targetCell)
    {
        var resolved = ResolveCollision(targetCell);
        _occupied[resolved.Column, resolved.Row] = itemId;
        return resolved;
    }

    /// <summary>
    /// Unregisters an item from its current cell.
    /// </summary>
    public void RemoveItem(GridCell cell)
    {
        if (_metrics.IsValid(cell))
        {
            _occupied[cell.Column, cell.Row] = null;
        }
    }

    private GridCell ResolveCollision(GridCell target)
    {
        if (!IsOccupied(target))
            return target;

        var queue = new Queue<GridCell>();
        var visited = new HashSet<GridCell>();

        queue.Enqueue(target);
        visited.Add(target);

        // Prioritize down and right to prefer filling naturally
        int[] dx = { 0, 1, 0, -1 };
        int[] dy = { 1, 0, -1, 0 };

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();

            if (!IsOccupied(cell))
            {
                return cell;
            }

            for (int i = 0; i < 4; i++)
            {
                var next = new GridCell(cell.Column + dx[i], cell.Row + dy[i]);
                if (_metrics.IsValid(next) && visited.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        throw new InvalidOperationException("The desktop grid is entirely full.");
    }

    /// <summary>
    /// Calculates the auto-arrange sequence.
    /// </summary>
    /// <param name="items">The list of items to arrange, sorted in the desired order.</param>
    /// <returns>A list of new states for the items.</returns>
    public IEnumerable<DesktopItemState> AutoArrange(IEnumerable<DesktopItemState> items)
    {
        var result = new List<DesktopItemState>();
        var sortedItems = items.OrderBy(x => x.SortOrder).ToList();
        
        int currentColumn = 0;
        int currentRow = 0;

        foreach (var item in sortedItems)
        {
            if (currentColumn >= _metrics.ColumnCount)
            {
                // We've run out of grid space on this monitor. The excess items might need migrating
                // or just piling up on the last cell, but typically we just drop or migrate them.
                break;
            }

            var cell = new GridCell(currentColumn, currentRow);
            _occupied[currentColumn, currentRow] = item.Id;

            result.Add(new DesktopItemState(item.Id, _metrics.MonitorId, currentColumn, currentRow, item.SortOrder));

            currentRow++;
            if (currentRow >= _metrics.RowCount)
            {
                currentRow = 0;
                currentColumn++;
            }
        }

        return result;
    }
}
