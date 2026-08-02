using DaisyOS.Core.Models;

namespace DaisyOS.Core.Services;

public interface IBluetoothService
{
    Task<BluetoothStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<bool> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
}
