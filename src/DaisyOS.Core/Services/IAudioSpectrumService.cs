namespace DaisyOS.Core.Services;

public interface IAudioSpectrumService : IDisposable
{
    Task<IReadOnlyList<double>> GetSpectrumAsync(CancellationToken cancellationToken = default);
}
