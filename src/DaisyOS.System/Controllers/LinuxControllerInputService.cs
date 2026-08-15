using DaisyOS.Core.Models;
using DaisyOS.Core.Services;

namespace DaisyOS.System.Controllers;

/// <summary>
/// Reads the Linux joystick interface for the currently connected controller. It consumes only
/// the controller node; keyboard and mouse input remain entirely outside this service.
/// </summary>
public sealed class LinuxControllerInputService : IControllerInputService
{
    private const byte ButtonEvent = 0x01;
    private const byte AxisEvent = 0x02;
    private const byte InitialStateFlag = 0x80;
    private const short AxisThreshold = 16_000;
    private readonly object _sync = new();
    private readonly Dictionary<byte, ControllerNavigationAction> _activeAxes = [];
    private CancellationTokenSource? _readerCancellation;
    private Task? _readerTask;
    private string? _activeDevicePath;

    public event EventHandler<ControllerNavigationAction>? NavigationRequested;

    public void Start(ControllerConnectionStatus controller)
    {
        var devicePath = controller.JoystickPath;
        if (string.IsNullOrWhiteSpace(devicePath) || !File.Exists(devicePath))
        {
            Stop();
            return;
        }

        lock (_sync)
        {
            if (string.Equals(_activeDevicePath, devicePath, StringComparison.Ordinal) && _readerTask is { IsCompleted: false })
            {
                return;
            }

            StopReaderLocked();
            _activeDevicePath = devicePath;
            _readerCancellation = new CancellationTokenSource();
            _readerTask = Task.Run(() => ReadEventsAsync(devicePath, _readerCancellation.Token));
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            StopReaderLocked();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task? readerTask;
        lock (_sync)
        {
            readerTask = _readerTask;
            StopReaderLocked();
        }

        if (readerTask is not null)
        {
            try
            {
                await readerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shell shutdown.
            }
        }
    }

    private async Task ReadEventsAsync(string devicePath, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                devicePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 8,
                FileOptions.Asynchronous);
            var buffer = new byte[8];

            while (!cancellationToken.IsCancellationRequested)
            {
                var received = 0;
                while (received < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(received, buffer.Length - received), cancellationToken);
                    if (read == 0)
                    {
                        return;
                    }

                    received += read;
                }

                var action = DecodeNavigation(buffer[6], BitConverter.ToInt16(buffer, 4), buffer[7]);
                if (action is { } navigationAction)
                {
                    NavigationRequested?.Invoke(this, navigationAction);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when a controller disconnects or the shell unloads.
        }
        catch (IOException)
        {
            // Controller removal closes the device stream on Linux.
        }
        catch (UnauthorizedAccessException)
        {
            // Detection remains useful even where session permissions do not grant input reads.
        }
    }

    /// <summary>Maps a Linux js_event into a shell action. Kept public for deterministic tests.</summary>
    public ControllerNavigationAction? DecodeNavigation(byte rawType, short value, byte number)
    {
        if ((rawType & InitialStateFlag) != 0)
        {
            return null;
        }

        var eventType = (byte)(rawType & ~InitialStateFlag);
        if (eventType == ButtonEvent)
        {
            return value == 1
                ? number switch
                {
                    0 => ControllerNavigationAction.Confirm, // Cross / A
                    1 => ControllerNavigationAction.Back,    // Circle / B
                    _ => null
                }
                : null;
        }

        if (eventType != AxisEvent)
        {
            return null;
        }

        var action = MapAxis(value, number);
        if (action is null)
        {
            _activeAxes.Remove(number);
            return null;
        }

        if (_activeAxes.TryGetValue(number, out var activeAction) && activeAction == action)
        {
            return null;
        }

        _activeAxes[number] = action.Value;
        return action;
    }

    private static ControllerNavigationAction? MapAxis(short value, byte axis) => axis switch
    {
        0 or 6 or 16 when value <= -AxisThreshold => ControllerNavigationAction.Left,
        0 or 6 or 16 when value >= AxisThreshold => ControllerNavigationAction.Right,
        1 or 7 or 17 when value <= -AxisThreshold => ControllerNavigationAction.Up,
        1 or 7 or 17 when value >= AxisThreshold => ControllerNavigationAction.Down,
        _ => null
    };

    private void StopReaderLocked()
    {
        _readerCancellation?.Cancel();
        _readerCancellation?.Dispose();
        _readerCancellation = null;
        _readerTask = null;
        _activeDevicePath = null;
        _activeAxes.Clear();
    }
}
