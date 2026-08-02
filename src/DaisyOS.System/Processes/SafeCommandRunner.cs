using System.Diagnostics;
using DaisyOS.Core.Models;
using DaisyOS.Core.Services;

namespace DaisyOS.System.Processes;

public sealed class SafeCommandRunner : ICommandRunner
{
    private readonly ILogService _log;

    public SafeCommandRunner(ILogService? log = null)
    {
        _log = log ?? NullLogService.Instance;
    }

    public async Task<CommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is InvalidOperationException or global::System.ComponentModel.Win32Exception)
        {
            _log.Log(LogLevel.Warning, $"Could not start command: {FormatCommand(fileName, arguments)}", ex);
            return new CommandResult(127, string.Empty, ex.Message, TimedOut: false);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);
            var result = new CommandResult(process.ExitCode, await stdoutTask, await stderrTask, TimedOut: false);
            if (!result.Succeeded)
            {
                _log.Log(
                    LogLevel.Warning,
                    $"Command failed with exit code {result.ExitCode}: {FormatCommand(fileName, arguments)}{FormatError(result.StandardError)}");
            }

            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            KillProcess(process);

            _log.Log(LogLevel.Warning, $"Command timed out after {timeout}: {FormatCommand(fileName, arguments)}");
            return new CommandResult(124, string.Empty, "Command timed out.", TimedOut: true);
        }
        catch (OperationCanceledException)
        {
            KillProcess(process);
            throw;
        }
    }

    private static void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string FormatCommand(string fileName, IReadOnlyList<string> arguments)
    {
        return arguments.Count == 0 ? fileName : $"{fileName} ({arguments.Count} args)";
    }

    private static string FormatError(string standardError)
    {
        var error = standardError.Trim();
        return string.IsNullOrEmpty(error) ? string.Empty : $" Error: {error}";
    }
}
