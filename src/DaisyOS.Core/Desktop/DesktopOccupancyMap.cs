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

    /// <summary>
    /// Finds the nearest unoccupied cell using a Manhattan distance search, preferring lower columns and rows.
    /// </summary>
    private GridCell ResolveCollision(GridCell target)
    {
        if (!IsOccupied(target))
            return target;

        GridCell? bestCell = null;
        int bestDistance = int.MaxValue;

        // Perform a full scan to find the best available cell
        // A more optimized BFS could be used, but grid sizes are small enough (e.g., 30x20)
        for (int col = 0; col < _metrics.ColumnCount; col++)
        {
            for (int row = 0; row < _metrics.RowCount; row++)
            {
                var candidate = new GridCell(col, row);
                if (IsOccupied(candidate)) continue;

                int distance = Math.Abs(col - target.Column) + Math.Abs(row - target.Row);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCell = candidate;
                }
                else if (distance == bestDistance)
                {
                    // Tie-breaker: normal desktop order (lower column first, then lower row)
                    if (bestCell != null)
                    {
                        if (col < bestCell.Value.Column || (col == bestCell.Value.Column && row < bestCell.Value.Row))
                        {
                            bestCell = candidate;
                        }
                    }
                }
            }
        }

        if (bestCell == null)
            throw new InvalidOperationException("The desktop grid is entirely full.");

        return bestCell.Value;
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
