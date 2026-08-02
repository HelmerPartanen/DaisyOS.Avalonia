using System;

namespace DaisyOS.Core.Desktop;

public record struct GridCell(int Column, int Row);

public record struct Point(double X, double Y);

public record struct Rect(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}

public class DesktopGridMetrics
{
    public string MonitorId { get; }
    public Rect WorkArea { get; }
    public double Dpi { get; }
    public double Scale { get; }
    public double CellWidth { get; }
    public double CellHeight { get; }
    
    public double GridOriginX { get; }
    public double GridOriginY { get; }
    
    public int ColumnCount { get; }
    public int RowCount { get; }
    
    public DesktopGridMetrics(
        string monitorId,
        Rect workArea,
        double dpi,
        double cellWidth,
        double cellHeight,
        double edgeInsetX = 0,
        double edgeInsetY = 0)
    {
        MonitorId = monitorId;
        WorkArea = workArea;
        Dpi = dpi;
        Scale = dpi / 96.0;
        
        // Requirements:
        // cellWidth  = GetSystemMetricsForDpi(SM_CXICONSPACING, monitorDpi)
        // cellHeight = GetSystemMetricsForDpi(SM_CYICONSPACING, monitorDpi)
        // Ensure they are strictly positive.
        if (cellWidth <= 0 || cellHeight <= 0)
            throw new ArgumentException("Cell dimensions must be strictly positive.");
            
        CellWidth = cellWidth;
        CellHeight = cellHeight;
        
        // Calculate the available grid region
        double availableWidth = Math.Max(0, WorkArea.Width - edgeInsetX * 2);
        double availableHeight = Math.Max(0, WorkArea.Height - edgeInsetY * 2);
        
        // Calculate column and row counts (minimum 1)
        ColumnCount = Math.Max(1, (int)Math.Floor(availableWidth / CellWidth));
        RowCount = Math.Max(1, (int)Math.Floor(availableHeight / CellHeight));
        
        GridOriginX = WorkArea.Left + edgeInsetX;
        GridOriginY = WorkArea.Top + edgeInsetY;
        
        // Invariants checking
        double occupiedWidth = ColumnCount * CellWidth;
        double occupiedHeight = RowCount * CellHeight;
        
        double remainderX = availableWidth - occupiedWidth;
        double remainderY = availableHeight - occupiedHeight;
        
        // In some extreme cases (work area smaller than 1 cell), the remainder could be negative
        // because we enforced a minimum of 1 column/row. But logically, if the work area is valid,
        // it holds 0 <= remainderX < cellWidth.
        if (availableWidth >= CellWidth)
        {
            if (remainderX < 0 || remainderX >= CellWidth)
                throw new InvalidOperationException($"Invariant failed: RemainderX {remainderX} is out of bounds [0, {CellWidth})");
        }
    }
    
    /// <summary>
    /// Snaps a screen coordinate to the nearest valid grid cell.
    /// </summary>
    public GridCell GetNearestCell(double x, double y)
    {
        int col = (int)Math.Round((x - GridOriginX - CellWidth / 2.0) / CellWidth);
        int row = (int)Math.Round((y - GridOriginY - CellHeight / 2.0) / CellHeight);
        
        col = Math.Clamp(col, 0, ColumnCount - 1);
        row = Math.Clamp(row, 0, RowCount - 1);
        
        return new GridCell(col, row);
    }
    
    /// <summary>
    /// Maps a grid cell coordinate to its physical pixel origin (top-left).
    /// </summary>
    public Point GetCellOrigin(GridCell cell)
    {
        // Enforce invariants on mapping
        if (cell.Column < 0 || cell.Column >= ColumnCount)
            throw new ArgumentOutOfRangeException(nameof(cell.Column), "Column is out of grid bounds.");
        if (cell.Row < 0 || cell.Row >= RowCount)
            throw new ArgumentOutOfRangeException(nameof(cell.Row), "Row is out of grid bounds.");
            
        double x = GridOriginX + cell.Column * CellWidth;
        double y = GridOriginY + cell.Row * CellHeight;
        
        return new Point(x, y);
    }
    
    /// <summary>
    /// Checks if a cell is within the valid grid bounds.
    /// </summary>
    public bool IsValid(GridCell cell)
    {
        return cell.Column >= 0 && cell.Column < ColumnCount &&
               cell.Row >= 0 && cell.Row < RowCount;
    }
}
