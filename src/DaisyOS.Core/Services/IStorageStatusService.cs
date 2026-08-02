using DaisyOS.Core.Models;

namespace DaisyOS.Core.Services;

public interface IStorageStatusService
{
    Task<StorageStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MountedStorage>> GetMountedStorageAsync(CancellationToken cancellationToken = default);
}
