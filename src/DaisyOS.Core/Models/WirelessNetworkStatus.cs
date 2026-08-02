namespace DaisyOS.Core.Models;

public sealed record WirelessNetworkStatus(
    bool IsAvailable,
    IReadOnlyList<WirelessNetworkInfo> Networks,
    string Detail);

public sealed record WirelessNetworkInfo(
    string Ssid,
    int SignalPercent,
    string Security,
    bool IsActive);
