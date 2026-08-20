using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using DaisyOS.Core.Models;
using DaisyOS.Shell.Services.Notifications;

namespace DaisyOS.Shell.Views.Components.Notifications;

public partial class NotificationCenterView : UserControl
{
    private App? _app;

    public NotificationCenterView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _app = Application.Current as App;
        if (_app is not null)
        {
            _app.Notifications.NotificationsChanged += OnNotificationsStateChanged;
            _app.Notifications.UnreadCountChanged += OnUnreadCountChanged;
            _app.Notifications.DoNotDisturbChanged += OnDndChanged;
            RefreshUI();
            _app.Notifications.MarkAllAsRead();
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (_app is not null)
        {
            _app.Notifications.NotificationsChanged -= OnNotificationsStateChanged;
            _app.Notifications.UnreadCountChanged -= OnUnreadCountChanged;
            _app.Notifications.DoNotDisturbChanged -= OnDndChanged;
        }
    }

    private void OnNotificationsStateChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(RefreshUI);
    }

    private void OnUnreadCountChanged(object? sender, int count)
    {
        Dispatcher.UIThread.Post(() =>
        {
            UnreadCountText.Text = count.ToString();
            UnreadBadge.IsVisible = count > 0;
        });
    }

    private void OnDndChanged(object? sender, bool isDnd)
    {
        Dispatcher.UIThread.Post(UpdateDndButton);
    }

    private void RefreshUI()
    {
        if (_app is null) return;

        var items = _app.Notifications.GetNotifications();
        NotificationList.ItemsSource = items;
        bool hasItems = items.Count > 0;

        NotificationList.IsVisible = hasItems;
        EmptyStateHost.IsVisible = !hasItems;
        ClearAllButton.IsEnabled = hasItems;

        int unread = _app.Notifications.UnreadCount;
        UnreadCountText.Text = unread.ToString();
        UnreadBadge.IsVisible = unread > 0;

        UpdateDndButton();
    }

    private void UpdateDndButton()
    {
        if (_app is null) return;

        bool isDnd = _app.Notifications.IsDoNotDisturb;
        DndIcon.Text = isDnd ? "do_not_disturb_on" : "notifications";
        DndText.Text = isDnd ? "DND On" : "DND Off";
    }

    private void OnClearAllClicked(object? sender, RoutedEventArgs e)
    {
        _app?.Notifications.Clear();
    }

    private void OnDndToggled(object? sender, RoutedEventArgs e)
    {
        if (_app is null) return;
        _app.Notifications.SetDoNotDisturb(!_app.Notifications.IsDoNotDisturb);
    }

    private void OnDismissItemClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            _app?.Notifications.DismissById(id);
        }
    }

    private void OnItemActionClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is NotificationItem item)
        {
            _app?.Notifications.TriggerAction(item);
        }
    }

    private int _testCount = 0;

    private void OnPostTestNotificationClicked(object? sender, RoutedEventArgs e)
    {
        _testCount++;
        var notification = new NotificationItem(
            Title: $"New Message Received",
            Body: "Hey, do you want to play a game later? I just got the new update.",
            CreatedAt: DateTimeOffset.Now,
            SourceAppId: "Steam",
            Icon: "avares://DaisyOS.Shell/Assets/AppIcons/SettingsIcon.png",
            Category: NotificationCategory.Application,
            Urgency: NotificationUrgency.Normal,
            ActionLabel: "Reply",
            ActionId: "reply_msg"
        );

        _app?.Notifications.Add(notification);
    }

    private void OnPostUrgentTestNotificationClicked(object? sender, RoutedEventArgs e)
    {
        _testCount++;
        var notification = new NotificationItem(
            Title: "Security Warning: Low Disk Space",
            Body: "System partition /dev/sda2 has less than 500 MB remaining. Clean up files immediately.",
            CreatedAt: DateTimeOffset.Now,
            SourceAppId: "Security & Storage",
            SystemIconGlyph: "warning",
            IsError: true,
            Category: NotificationCategory.Security,
            Urgency: NotificationUrgency.Critical,
            ActionLabel: "Open Disk Cleaner",
            ActionId: "disk_cleaner"
        );

        _app?.Notifications.Add(notification);
    }

    private void OnPostSystemTestNotificationClicked(object? sender, RoutedEventArgs e)
    {
        _testCount++;
        var notification = new NotificationItem(
            Title: "System Update Available",
            Body: "DaisyOS 2026.08 feature update is ready to download and install.",
            CreatedAt: DateTimeOffset.Now,
            SourceAppId: "Software Updater",
            SystemIconGlyph: "system_update",
            Category: NotificationCategory.Updates,
            Urgency: NotificationUrgency.Normal,
            ActionLabel: "Install Now",
            ActionId: "install_update"
        );

        _app?.Notifications.Add(notification);
    }
}
