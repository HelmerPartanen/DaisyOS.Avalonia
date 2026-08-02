namespace DaisyOS.Core.Desktop;

/// <summary>
/// Represents the persistent state and grid identity of a desktop item.
/// </summary>
public class DesktopItemState
{
    /// <summary>
    /// Stable identifier for the item (e.g., path or UUID).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The monitor this item is assigned to.
    /// </summary>
    public string MonitorId { get; set; } = string.Empty;

    /// <summary>
    /// The grid column position.
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// The grid row position.
    /// </summary>
    public int Row { get; set; }

    /// <summary>
    /// The sorting order for auto-arrange.
    /// </summary>
    public int SortOrder { get; set; }

    public DesktopItemState() { }

    public DesktopItemState(string id, string monitorId, int column, int row, int sortOrder = 0)
    {
        Id = id;
        MonitorId = monitorId;
        Column = column;
        Row = row;
        SortOrder = sortOrder;
    }
}
