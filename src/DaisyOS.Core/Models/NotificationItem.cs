namespace DaisyOS.Core.Models;

public enum NotificationCategory { General, Application, System, Network, Battery, Updates, Security }
public enum NotificationUrgency { Low, Normal, High, Critical }

public sealed record NotificationItem(
    string Title,
    string Body,
    DateTimeOffset CreatedAt,
    string? SourceAppId = null,
    string? Icon = null,
    string? SystemIconGlyph = null,
    bool IsError = false,
    NotificationCategory Category = NotificationCategory.General,
    NotificationUrgency Urgency = NotificationUrgency.Normal);
