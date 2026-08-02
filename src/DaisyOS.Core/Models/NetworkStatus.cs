namespace DaisyOS.Core.Models;

public enum NetworkConnectionKind
{
    Unknown,
    WiFi,
    Ethernet
}

public sealed record NetworkStatus(
    bool IsConnected,
    string DisplayName,
    string Detail,
    NetworkConnectionKind ConnectionKind = NetworkConnectionKind.Unknown,
    bool IsAvailable = true);
