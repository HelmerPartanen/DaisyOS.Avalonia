using DaisyOS.Core.Models;

namespace DaisyOS.Core.Services;

public interface IBluetoothService
{
    Task<BluetoothStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<bool> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    Task<bool> SetConnectedAsync(string address, bool connected, CancellationToken cancellationToken = default);
}
