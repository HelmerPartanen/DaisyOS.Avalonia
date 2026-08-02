using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.System.Processes;

namespace DaisyOS.System.Power;

public sealed class LinuxPowerService : IPowerService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);
    private readonly ICommandRunner _commandRunner;

    public LinuxPowerService(ICommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    public Task<PowerActionResult> ShutdownAsync(CancellationToken cancellationToken = default)
    {
        return RunSystemctlAsync("poweroff", cancellationToken);
    }

    public Task<PowerActionResult> RebootAsync(CancellationToken cancellationToken = default)
    {
        return RunSystemctlAsync("reboot", cancellationToken);
    }

    public Task<PowerActionResult> SuspendAsync(CancellationToken cancellationToken = default)
    {
        return RunSystemctlAsync("suspend", cancellationToken);
    }

    private async Task<PowerActionResult> RunSystemctlAsync(string action, CancellationToken cancellationToken)
    {
        var result = await _commandRunner.RunAsync("systemctl", [action], CommandTimeout, cancellationToken);
        var message = result.Succeeded
            ? $"Requested system {action}."
            : $"Could not request system {action}: {result.StandardError}";

        return new PowerActionResult(result.Succeeded, message.Trim());
    }
}
