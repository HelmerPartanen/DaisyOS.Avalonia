using System.ComponentModel;
using System.Diagnostics;
using DaisyOS.Core.Models;
using DaisyOS.Core.Services;

namespace DaisyOS.System.Processes;

public sealed class SafeProcessLauncher
{
    private readonly ILogService _log;

    public SafeProcessLauncher(ILogService? log = null)
    {
        _log = log ?? NullLogService.Instance;
    }

    public AppLaunchResult Launch(string fileName, IReadOnlyList<string>? arguments = null)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains('\0', StringComparison.Ordinal))
        {
            return new AppLaunchResult(false, "Launch target was invalid.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (arguments is not null)
        {
            foreach (var argument in arguments)
            {
                if (argument.Contains('\0', StringComparison.Ordinal))
                {
                    return new AppLaunchResult(false, "Launch argument was invalid.");
                }

                startInfo.ArgumentList.Add(argument);
            }
        }

        try
        {
            var process = Process.Start(startInfo);
            return new AppLaunchResult(true, "Launch requested.", process?.Id);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            _log.Log(LogLevel.Warning, $"Could not launch process: {fileName}", ex);
            return new AppLaunchResult(false, $"Could not start {fileName}.");
        }
    }
}
