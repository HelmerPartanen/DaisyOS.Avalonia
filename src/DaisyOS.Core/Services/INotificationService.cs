using DaisyOS.Core.Models;

namespace DaisyOS.Core.Services;

public interface INotificationService
{
    event EventHandler? NotificationsChanged;

    IReadOnlyList<NotificationItem> GetNotifications();

    void Add(NotificationItem notification);

    void Dismiss(NotificationItem notification);

    void Clear();

}
