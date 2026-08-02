using DaisyOS.Core.Models;
using DaisyOS.Core.Services;

namespace DaisyOS.System.Storage;

public sealed class LinuxStorageStatusService : IStorageStatusService
{
    public Task<IReadOnlyList<MountedStorage>> GetMountedStorageAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var mounts = new List<MountedStorage>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.IsReady && IsUserVisibleMount(drive.Name)))
            {
                mounts.Add(new MountedStorage(
                    GetDisplayName(drive),
                    drive.Name,
                    drive.TotalSize,
                    drive.AvailableFreeSpace));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return Task.FromResult<IReadOnlyList<MountedStorage>>(mounts
            .GroupBy(mount => mount.MountPoint, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(mount => mount.MountPoint == "/" ? 0 : 1)
            .ThenBy(mount => mount.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray());
    }

    public Task<StorageStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var root = DriveInfo
                .GetDrives()
                .FirstOrDefault(drive => drive.IsReady && drive.Name == Path.GetPathRoot(Environment.SystemDirectory))
                ?? DriveInfo.GetDrives().FirstOrDefault(drive => drive.IsReady && drive.Name == "/")
                ?? DriveInfo.GetDrives().FirstOrDefault(drive => drive.IsReady);

            if (root is null)
            {
                return Task.FromResult(new StorageStatus(false, "Unavailable", "Unavailable", "No ready filesystem was found.", null, null));
            }

            var used = root.TotalSize - root.AvailableFreeSpace;
            var percent = root.TotalSize <= 0 ? 0 : used / (double)root.TotalSize * 100;
            var displayName = root.Name == "/" ? "System Drive" : GetDisplayName(root);
            var detail = $"Using {FormatBytes(used)} of {FormatBytes(root.TotalSize)} on {displayName}.";
            return Task.FromResult(new StorageStatus(true, displayName, $"{Math.Round(percent)}%", detail, used, root.TotalSize));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(new StorageStatus(false, "Unavailable", "Unavailable", "Storage status could not be read.", null, null));
        }
    }

    private static string FormatBytes(long bytes)
    {
        var gibibytes = bytes / 1024d / 1024d / 1024d;
        return $"{gibibytes:0.0} GB";
    }

    private static bool IsUserVisibleMount(string mountPoint) =>
        mountPoint == "/"
        || mountPoint.StartsWith("/media/", StringComparison.Ordinal)
        || mountPoint.StartsWith("/mnt/", StringComparison.Ordinal)
        || mountPoint.StartsWith("/run/media/", StringComparison.Ordinal);

    private static string GetDisplayName(DriveInfo drive)
    {
        if (drive.Name == "/")
        {
            return "System";
        }

        var trimmed = drive.Name.TrimEnd(Path.DirectorySeparatorChar);
        return Path.GetFileName(trimmed) is { Length: > 0 } name ? name : drive.Name;
    }
}
