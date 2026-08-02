namespace DaisyOS.Core.Models;

public sealed record SystemCapabilityStatus(
    bool HasNetworkManager,
    bool HasPipeWire,
    bool HasSystemctl,
    bool HasXrandr,
    bool IsRoot,
    bool HasSudoGroup,
    string Detail);
