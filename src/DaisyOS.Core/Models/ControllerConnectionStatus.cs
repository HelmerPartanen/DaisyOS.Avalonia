namespace DaisyOS.Core.Models;

/// <summary>Describes a game controller that is currently exposed to the Linux input stack.</summary>
public sealed record ControllerConnectionStatus(
    bool IsConnected,
    string? Name,
    string? DevicePath,
    string? JoystickPath = null)
{
    public static ControllerConnectionStatus Disconnected { get; } = new(false, null, null);
}
