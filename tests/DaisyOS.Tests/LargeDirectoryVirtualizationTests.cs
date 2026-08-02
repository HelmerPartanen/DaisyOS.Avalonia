using System.Diagnostics;
using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.Shell.Services;
using DaisyOS.Shell.ViewModels;
using Xunit;

namespace DaisyOS.Tests;

public sealed class LargeDirectoryVirtualizationTests
{
    [Fact]
    public void LargeDirectory_50kFiles_SortsAndRespondsFast()
    {
        var notifications = new DummyNotifications();
        var associations = new DummyFileAssociations();
        var vm = new FileManagerViewModel(
            notifications,
            associations,
            settings: null,
            initialPath: "/tmp",
            trashFilesPath: "/tmp/trash_files",
            trashInfoPath: "/tmp/trash_info");

        // Generate 50,000 synthetic file items
        var items = new List<FileItemViewModel>(50000);
        for (var i = 0; i < 50000; i++)
        {
            items.Add(new FileItemViewModel(
                $"/tmp/synthetic_file_{i:D5}.txt",
                $"synthetic_file_{i:D5}.txt",
                false,
                (i * 128) % 1048576,
                DateTime.UtcNow.AddMinutes(-i)));
        }

        var sw = Stopwatch.StartNew();

        // Populate Items collection
        vm.Items.Clear();
        foreach (var item in items)
        {
            vm.Items.Add(item);
        }

        sw.Stop();
        Assert.Equal(50000, vm.Items.Count);
        Assert.True(sw.ElapsedMilliseconds < 2500, $"Populating 50k items took {sw.ElapsedMilliseconds} ms");

        // Verify sort field changes complete efficiently
        sw.Restart();
        vm.SetSortFieldCommand.Execute("Size");
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Sorting 50k items took {sw.ElapsedMilliseconds} ms");
    }

    private sealed class DummyFileAssociations : IFileAssociationService
    {
        public Task<FileHandlerQueryResult> GetHandlersAsync(string filePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FileHandlerQueryResult(true, "text/plain", [], string.Empty));

        public Task<AppLaunchResult> OpenDefaultAsync(string filePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppLaunchResult(false, "Not used."));

        public Task<AppLaunchResult> OpenWithAsync(FileHandler handler, string filePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppLaunchResult(false, "Not used."));
    }

    private sealed class DummyNotifications : INotificationService
    {
        public event EventHandler? NotificationsChanged;
        public IReadOnlyList<NotificationItem> GetNotifications() => [];
        public void Add(NotificationItem notification) => NotificationsChanged?.Invoke(this, EventArgs.Empty);
        public void Dismiss(NotificationItem notification) { }
        public void Clear() { }
    }
}
