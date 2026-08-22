using System.Diagnostics;
using System.Text.Json;

namespace DaisyOS.Shell.Services.Compositor;

/// <summary>
/// Receives KWin window snapshots from the session bus. The KWin script sends
/// snapshots to a private bus destination; dbus-monitor observes those calls
/// without granting DaisyOS any compositor privileges.
/// </summary>
public sealed class KWinWindowBridge : ICompositorWindowService
{
    internal const string BusName = "org.daisyos.Shell.WindowBridge";
    internal const string ObjectPath = "/org/daisyos/Shell/WindowBridge";
    internal const string InterfaceName = "org.daisyos.Shell.WindowBridge";
    internal const string SnapshotMember = "PublishSnapshot";

    private readonly Dictionary<string, CompositorWindowRecord> _windows = new(StringComparer.Ordinal);
    private readonly KWinCommandDispatcher _dispatcher = new();
    private Process? _monitor;
    private CancellationTokenSource? _monitorCancellation;

    public IReadOnlyCollection<CompositorWindowRecord> Windows => _windows.Values.ToArray();
    public bool IsAvailable { get; private set; }
    public event EventHandler? WindowsChanged;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux() || _monitor is not null)
        {
            return Task.CompletedTask;
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dbus-monitor",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            },
            EnableRaisingEvents = true
        };
        process.StartInfo.ArgumentList.Add("--session");
        process.StartInfo.ArgumentList.Add($"type='method_call',destination='{BusName}',interface='{InterfaceName}',member='{SnapshotMember}'");

        try
        {
            process.Start();
        }
        catch (Exception)
        {
            process.Dispose();
            return Task.CompletedTask;
        }

        _monitor = process;
        _monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsAvailable = true;
        _ = ReadSnapshotsAsync(process, _monitorCancellation.Token);
        return Task.CompletedTask;
    }

    public Task ActivateOrMinimizeAsync(string windowId, CancellationToken cancellationToken = default) =>
        _dispatcher.DispatchAsync(windowId, KWinWindowCommand.ActivateOrMinimize, cancellationToken);

    public Task CloseAsync(string windowId, CancellationToken cancellationToken = default) =>
        _dispatcher.DispatchAsync(windowId, KWinWindowCommand.Close, cancellationToken);

    private async Task ReadSnapshotsAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
            {
                var marker = line.IndexOf("string ", StringComparison.Ordinal);
                if (marker < 0) continue;
                var serializedString = line[(marker + "string ".Length)..].Trim();
                string? payload;
                try { payload = JsonSerializer.Deserialize<string>(serializedString); }
                catch (JsonException) { continue; }

                if (KWinWindowSnapshotParser.TryParse(payload, out var snapshot))
                {
                    ApplySnapshot(snapshot);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsAvailable = false;
        }
    }

    internal void ApplySnapshot(IReadOnlyList<CompositorWindowRecord> snapshot)
    {
        var changed = _windows.Count != snapshot.Count;
        var incoming = snapshot.ToDictionary(window => window.Id, StringComparer.Ordinal);
        foreach (var window in snapshot)
        {
            if (!_windows.TryGetValue(window.Id, out var current) || current != window)
            {
                _windows[window.Id] = window;
                changed = true;
            }
        }
        foreach (var staleId in _windows.Keys.Where(id => !incoming.ContainsKey(id)).ToArray())
        {
            _windows.Remove(staleId);
            changed = true;
        }
        if (changed) WindowsChanged?.Invoke(this, EventArgs.Empty);
    }

    public ValueTask DisposeAsync()
    {
        _monitorCancellation?.Cancel();
        if (_monitor is { HasExited: false })
        {
            try { _monitor.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        }
        _monitor?.Dispose();
        _monitorCancellation?.Dispose();
        _monitor = null;
        _monitorCancellation = null;
        IsAvailable = false;
        return ValueTask.CompletedTask;
    }
}
