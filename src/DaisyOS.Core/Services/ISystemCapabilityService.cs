using DaisyOS.Core.Models;

namespace DaisyOS.Core.Services;

public interface ISystemCapabilityService
{
    Task<SystemCapabilityStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
