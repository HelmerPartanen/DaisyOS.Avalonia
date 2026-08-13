using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.System.Processes;
using System.Globalization;

namespace DaisyOS.System.Networking;

public sealed class LinuxWirelessNetworkService : IWirelessNetworkService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(8);
    private readonly ICommandRunner _commandRunner;

    public LinuxWirelessNetworkService(ICommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    public async Task<WirelessNetworkStatus> GetNetworksAsync(CancellationToken cancellationToken = default)
    {
        var deviceResult = await _commandRunner.RunAsync(
            "nmcli",
            ["-t", "-f", "DEVICE,TYPE", "device", "status"],
            CommandTimeout,
            cancellationToken);

        if (!deviceResult.Succeeded)
        {
            return new WirelessNetworkStatus(
                false,
                Array.Empty<WirelessNetworkInfo>(),
                BuildFailureDetail(deviceResult, "Wi-Fi status"));
        }

        var hasWirelessAdapter = deviceResult.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(line =>
            {
                var fields = SplitEscapedFields(line);
                return fields.Count > 1 && fields[1].Equals("wifi", StringComparison.OrdinalIgnoreCase);
            });
        if (!hasWirelessAdapter)
        {
            return new WirelessNetworkStatus(
                false,
                Array.Empty<WirelessNetworkInfo>(),
                "Wi-Fi isn’t available on this device.");
        }

        var result = await _commandRunner.RunAsync(
            "nmcli",
            ["-t", "-f", "IN-USE,SSID,SECURITY,SIGNAL", "device", "wifi", "list", "--rescan", "no"],
            CommandTimeout,
            cancellationToken);

        if (!result.Succeeded)
        {
            return new WirelessNetworkStatus(
                false,
                Array.Empty<WirelessNetworkInfo>(),
                BuildFailureDetail(result, "Wi-Fi network scan"));
        }

        var networks = result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseNetworkLine)
            .Where(network => network is not null)
            .Cast<WirelessNetworkInfo>()
            .GroupBy(network => string.IsNullOrWhiteSpace(network.Ssid) ? "<hidden>" : network.Ssid, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(network => network.IsActive).ThenByDescending(network => network.SignalPercent).First())
            .OrderByDescending(network => network.IsActive)
            .ThenByDescending(network => network.SignalPercent)
            .ThenBy(network => network.Ssid, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        var detailText = networks.Length == 0
            ? "No Wi-Fi networks found."
            : $"{networks.Length} Wi-Fi {(networks.Length == 1 ? "network" : "networks")} found.";
        return new WirelessNetworkStatus(true, networks, detailText);
    }

    private static WirelessNetworkInfo? ParseNetworkLine(string line)
    {
        var fields = SplitEscapedFields(line);
        if (fields.Count < 4)
        {
            return null;
        }

        var isActive = fields[0] == "*";
        var ssid = string.IsNullOrWhiteSpace(fields[1]) ? "Hidden network" : fields[1];
        var security = string.IsNullOrWhiteSpace(fields[2]) ? "Open" : fields[2];
        var signal = int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSignal)
            ? Math.Clamp(parsedSignal, 0, 100)
            : 0;

        return new WirelessNetworkInfo(ssid, signal, security, isActive);
    }

    private static IReadOnlyList<string> SplitEscapedFields(string line)
    {
        var fields = new List<string>();
        var current = new List<char>();
        var isEscaped = false;

        foreach (var character in line)
        {
            if (isEscaped)
            {
                current.Add(character);
                isEscaped = false;
                continue;
            }

            if (character == '\\')
            {
                isEscaped = true;
                continue;
            }

            if (character == ':')
            {
                fields.Add(new string(current.ToArray()));
                current.Clear();
                continue;
            }

            current.Add(character);
        }

        if (isEscaped)
        {
            current.Add('\\');
        }

        fields.Add(new string(current.ToArray()));
        return fields;
    }

    private static string BuildFailureDetail(CommandResult result, string operation)
    {
        if (result.TimedOut)
        {
            return $"{operation} timed out.";
        }

        var error = result.StandardError.Trim();
        return string.IsNullOrWhiteSpace(error)
            ? $"{operation} failed."
            : $"{operation} failed: {error}";
    }

    public async Task<bool> ConnectToNetworkAsync(string ssid, string? password = null, CancellationToken cancellationToken = default)
    {
        var arguments = new List<string> { "device", "wifi", "connect", ssid };
        if (!string.IsNullOrEmpty(password))
        {
            arguments.Add("password");
            arguments.Add(password);
        }

        var result = await _commandRunner.RunAsync(
            "nmcli",
            arguments,
            CommandTimeout,
            cancellationToken);

        return result.Succeeded;
    }

    public async Task<bool> DisconnectFromNetworkAsync(CancellationToken cancellationToken = default)
    {
        var resultDevice = await _commandRunner.RunAsync(
            "nmcli",
            ["-t", "-f", "DEVICE,TYPE,STATE", "device"],
            CommandTimeout,
            cancellationToken);

        if (!resultDevice.Succeeded)
        {
            return false;
        }

        var wifiDevices = resultDevice.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(SplitEscapedFields)
            .Where(parts => parts.Count >= 3
                && parts[1].Equals("wifi", StringComparison.OrdinalIgnoreCase)
                && parts[2].Equals("connected", StringComparison.OrdinalIgnoreCase))
            .Select(parts => parts[0])
            .ToArray();

        if (wifiDevices.Length == 0)
        {
            return true;
        }

        var success = true;
        foreach (var device in wifiDevices)
        {
            var disconnectResult = await _commandRunner.RunAsync(
                "nmcli",
                ["device", "disconnect", device],
                CommandTimeout,
                cancellationToken);
            if (!disconnectResult.Succeeded)
            {
                success = false;
            }
        }

        return success;
    }

    public async Task<bool> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var result = await _commandRunner.RunAsync(
            "nmcli",
            ["radio", "wifi", enabled ? "on" : "off"],
            CommandTimeout,
            cancellationToken);
        return result.Succeeded;
    }
}
