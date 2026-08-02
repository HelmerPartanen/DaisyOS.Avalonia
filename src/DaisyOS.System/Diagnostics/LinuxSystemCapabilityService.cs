using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.System.Processes;

namespace DaisyOS.System.Diagnostics;

public sealed class LinuxSystemCapabilityService : ISystemCapabilityService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(3);
    private readonly ICommandRunner _commandRunner;

    public LinuxSystemCapabilityService(ICommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    public async Task<SystemCapabilityStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var hasNetworkManager = await HasCommandAsync("nmcli", cancellationToken);
        var hasPipeWire = await HasCommandAsync("wpctl", cancellationToken);
        var hasSystemctl = await HasCommandAsync("systemctl", cancellationToken);
        var hasXrandr = await HasCommandAsync("xrandr", cancellationToken);
        var id = await _commandRunner.RunAsync("id", ["-Gn"], CommandTimeout, cancellationToken);
        var groups = id.Succeeded ? id.StandardOutput.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [];
        var isRoot = string.Equals(Environment.UserName, "root", StringComparison.OrdinalIgnoreCase);
        var hasSudoGroup = groups.Contains("wheel", StringComparer.OrdinalIgnoreCase) || groups.Contains("sudo", StringComparer.OrdinalIgnoreCase);

        var available = new[]
        {
            hasNetworkManager ? "NetworkManager" : null,
            hasPipeWire ? "PipeWire" : null,
            hasSystemctl ? "systemd" : null,
            hasXrandr ? "xrandr" : null
        }.Where(item => item is not null);

        return new SystemCapabilityStatus(
            hasNetworkManager,
            hasPipeWire,
            hasSystemctl,
            hasXrandr,
            isRoot,
            hasSudoGroup,
            $"Available: {string.Join(", ", available)}.");
    }

    private static Task<bool> HasCommandAsync(string command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Task.FromResult(paths.Any(path => File.Exists(Path.Combine(path, command))));
    }
}
