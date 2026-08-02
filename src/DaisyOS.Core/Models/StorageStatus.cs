namespace DaisyOS.Core.Models;

public sealed record StorageStatus(
    bool IsAvailable,
    string MountPoint,
    string UsageLabel,
    string Detail,
    long? UsedBytes,
    long? TotalBytes);

public sealed record MountedStorage(
    string Name,
    string MountPoint,
    long? TotalBytes,
    long? AvailableBytes);
