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
    }

    private void OnClearAllClicked(object? sender, RoutedEventArgs e)
    {
        _app?.Notifications.Clear();
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
}
