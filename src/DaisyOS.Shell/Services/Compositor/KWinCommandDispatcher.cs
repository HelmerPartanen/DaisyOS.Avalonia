using System.Diagnostics;
using System.Text.Json;

namespace DaisyOS.Shell.Services.Compositor;

/// <summary>
/// Sends a single, validated window command to KWin through its scripting D-Bus
/// endpoint. Commands are short-lived scripts so the shell never assumes an
/// application process can be manipulated directly.
/// </summary>
internal sealed class KWinCommandDispatcher
{
    private const string KWinService = "org.kde.KWin";
    private const string ScriptingPath = "/Scripting";
    private const string ScriptingInterface = "org.kde.kwin.Scripting";

    public async Task DispatchAsync(string windowId, KWinWindowCommand command, CancellationToken cancellationToken, int workspace = 0)
    {
        if (string.IsNullOrWhiteSpace(windowId)
            || windowId.Length > 256
            || windowId.Any(char.IsControl))
        {
            return;
        }

        var runtimeDirectory = Path.Combine(
            Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? Path.GetTempPath(),
            "daisyos", "kwin-commands");
        Directory.CreateDirectory(runtimeDirectory);

        var pluginName = $"daisyos-command-{Guid.NewGuid():N}";
        var scriptPath = Path.Combine(runtimeDirectory, $"{pluginName}.js");
        await File.WriteAllTextAsync(scriptPath, CreateScript(windowId, command, workspace), cancellationToken);

        try
        {
            var scriptId = await RunQDbusAsync([KWinService, ScriptingPath, $"{ScriptingInterface}.loadScript", scriptPath, pluginName], cancellationToken);
            if (!string.IsNullOrWhiteSpace(scriptId))
            {
                await RunQDbusAsync([KWinService, ScriptingPath, $"{ScriptingInterface}.start"], cancellationToken);
            }
        }
        finally
        {
            try { File.Delete(scriptPath); } catch (IOException) { }
        }
    }

    private static string CreateScript(string windowId, KWinWindowCommand command, int workspace)
    {
        var literalId = JsonSerializer.Serialize(windowId);
        var body = command switch
        {
            KWinWindowCommand.ActivateOrMinimize => @"
if (workspace.activeWindow === target && !target.minimized) {
    target.minimized = true;
} else {
    target.minimized = false;
    workspace.activeWindow = target;
}",
            KWinWindowCommand.Close => "target.closeWindow();",
            KWinWindowCommand.Maximize => "target.maximized = true;",
            KWinWindowCommand.Restore => "target.maximized = false; target.minimized = false; workspace.activeWindow = target;",
            KWinWindowCommand.MoveToWorkspace => $@"
var targetWorkspace = Number({workspace});
if (targetWorkspace > 0 && workspace.desktops.length >= targetWorkspace) {{
    target.desktops = [workspace.desktops[targetWorkspace - 1]];
}}
",
            _ => string.Empty
        };

        return $@"
var targetId = {literalId};
var windows = workspace.windowList();
for (var index = 0; index < windows.length; ++index) {{
    var candidate = windows[index];
    if (String(candidate.internalId) !== targetId) continue;
    var target = candidate;
    {body}
    break;
}}
";
    }

    private static async Task<string> RunQDbusAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux()) return string.Empty;
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "qdbus6",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);

        try
        {
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0 ? (await output).Trim() : string.Empty;
        }
        catch (Exception) when (OperatingSystem.IsLinux())
        {
            return string.Empty;
        }
    }
}

internal enum KWinWindowCommand
{
    ActivateOrMinimize,
    Close,
    Maximize,
    Restore,
    MoveToWorkspace
}
