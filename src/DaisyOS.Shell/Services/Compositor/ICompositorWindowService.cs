namespace DaisyOS.Shell.Services.Compositor;

/// <summary>
/// Compositor-neutral window lifecycle contract used by shell controls.
/// KWin is the first implementation; the UI must not depend on KWin APIs.
/// </summary>
public interface ICompositorWindowService : IAsyncDisposable
{
    IReadOnlyCollection<CompositorWindowRecord> Windows { get; }

    event EventHandler? WindowsChanged;

    bool IsAvailable { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task ActivateOrMinimizeAsync(string windowId, CancellationToken cancellationToken = default);

    Task CloseAsync(string windowId, CancellationToken cancellationToken = default);
}
