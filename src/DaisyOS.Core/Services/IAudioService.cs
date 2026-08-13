using DaisyOS.Core.Models;

namespace DaisyOS.Core.Services;

public interface IAudioService
{
    Task<double?> GetVolumeAsync(CancellationToken cancellationToken = default);

    Task SetVolumeAsync(double volume, CancellationToken cancellationToken = default);

    Task<string> GetDefaultDeviceNameAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AudioDeviceInfo>> GetAudioDevicesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns false when PipeWire rejects the requested sink.</summary>
    Task<bool> SetDefaultAudioDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
}
