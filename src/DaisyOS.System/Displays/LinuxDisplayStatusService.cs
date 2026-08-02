using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.System.Processes;
using System.Text.RegularExpressions;

namespace DaisyOS.System.Displays;

public sealed partial class LinuxDisplayStatusService : IDisplayStatusService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(5);
    private readonly ICommandRunner _commandRunner;

    public LinuxDisplayStatusService(ICommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    public async Task<DisplayStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var result = await _commandRunner.RunAsync("xrandr", ["--query"], CommandTimeout, cancellationToken);
        if (!result.Succeeded)
        {
            var detail = string.IsNullOrWhiteSpace(result.StandardError)
                ? "Display status is unavailable."
                : result.StandardError.Trim();
            return new DisplayStatus(false, "Unavailable", "Unavailable", 0, detail);
        }

        var displays = result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseDisplayLine)
            .Where(display => display is not null)
            .Cast<DisplayInfo>()
            .ToArray();

        var connected = displays.Where(display => display.IsConnected).ToArray();
        if (connected.Length == 0)
        {
            return new DisplayStatus(false, "No display", "Unavailable", 0, result.StandardOutput.Trim());
        }

        var primary = connected.FirstOrDefault(display => display.IsPrimary) ?? connected[0];
        return new DisplayStatus(
            true,
            primary.Name,
            primary.Resolution ?? "Unknown",
            connected.Length,
            BuildDetail(connected));
    }

    private static DisplayInfo? ParseDisplayLine(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts[1] is not ("connected" or "disconnected"))
        {
            return null;
        }

        var isConnected = parts[1] == "connected";
        var isPrimary = parts.Contains("primary", StringComparer.OrdinalIgnoreCase);
        var resolution = isConnected
            ? parts.Select(part => ResolutionRegex().Match(part))
                .FirstOrDefault(match => match.Success)
                ?.Value
            : null;

        return new DisplayInfo(parts[0], isConnected, isPrimary, resolution);
    }

    private static string BuildDetail(IReadOnlyList<DisplayInfo> displays)
    {
        var label = displays.Count == 1 ? "display" : "displays";
        var summaries = displays.Select(display =>
            string.IsNullOrWhiteSpace(display.Resolution)
                ? display.Name
                : $"{display.Name} at {display.Resolution}");
        return $"{displays.Count} connected {label}: {string.Join(", ", summaries)}.";
    }

    [GeneratedRegex(@"^\d+x\d+")]
    private static partial Regex ResolutionRegex();

    private sealed record DisplayInfo(string Name, bool IsConnected, bool IsPrimary, string? Resolution);
}
