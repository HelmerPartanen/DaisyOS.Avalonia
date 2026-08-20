using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DaisyOS.Core.Models;
using DaisyOS.Shell.Services.Notifications;

namespace DaisyOS.Shell.Views.Components.Notifications;

public partial class NotificationToastHost : UserControl
{
    private readonly ObservableCollection<NotificationItem> _activeToasts = new();
    private readonly ConcurrentDictionary<string, DispatcherTimer> _dismissTimers = new();
    private App? _app;

    public NotificationToastHost()
    {
        InitializeComponent();
        ToastList.ItemsSource = _activeToasts;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _app = Application.Current as App;
        if (_app is not null)
        {
            _app.Notifications.NotificationPosted += OnNotificationPosted;
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (_app is not null)
        {
            _app.Notifications.NotificationPosted -= OnNotificationPosted;
        }

        foreach (var timer in _dismissTimers.Values)
        {
            timer.Stop();
        }
        _dismissTimers.Clear();
        _activeToasts.Clear();
    }

    private void OnNotificationPosted(object? sender, NotificationItem notification)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Limit to 3 toasts at once
            while (_activeToasts.Count >= 3)
            {
                var oldest = _activeToasts.Last();
                RemoveToast(oldest.Id);
            }

            _activeToasts.Insert(0, notification);

            // Auto-dismiss after 5 seconds unless Critical/High urgency
            if (notification.Urgency is NotificationUrgency.Low or NotificationUrgency.Normal)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    RemoveToast(notification.Id);
                };
                _dismissTimers[notification.Id] = timer;
                timer.Start();
            }
        });
    }

    private void RemoveToast(string id)
    {
        if (_dismissTimers.TryRemove(id, out var timer))
        {
            timer.Stop();
        }

        var item = _activeToasts.FirstOrDefault(t => t.Id == id);
        if (item is not null)
        {
            _activeToasts.Remove(item);
        }
    }

    private void OnDismissToastClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            RemoveToast(id);
        }
    }

    private void OnToastActionClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is NotificationItem item && _app is not null)
        {
            RemoveToast(item.Id);
            _app.Notifications.TriggerAction(item);
        }
    }
}
