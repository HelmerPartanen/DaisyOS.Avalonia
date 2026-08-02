using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.System.Processes;
using System.Text.RegularExpressions;

namespace DaisyOS.System.Bluetooth;

public sealed class LinuxBluetoothService : IBluetoothService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(5);
    private readonly ICommandRunner _commandRunner;

    public LinuxBluetoothService(ICommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    public async Task<BluetoothStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var resultShow = await _commandRunner.RunAsync(
            "bluetoothctl",
            ["show"],
            CommandTimeout,
            cancellationToken);

        if (!resultShow.Succeeded)
        {
            return new BluetoothStatus(
                false,
                false,
                false,
                Array.Empty<BluetoothDevice>(),
                BuildFailureDetail(resultShow, "Bluetooth status query"));
        }

        var isEnabled = ParseShowOutput(resultShow.StandardOutput);
        var isDiscovering = resultShow.StandardOutput.Contains("Discovering: yes", StringComparison.OrdinalIgnoreCase);

        var devices = new List<BluetoothDevice>();
        var deviceQueryFailed = false;
        if (isEnabled)
        {
            var resultDevices = await _commandRunner.RunAsync(
                "bluetoothctl",
                ["devices"],
                CommandTimeout,
                cancellationToken);

            if (resultDevices.Succeeded)
            {
                var lines = resultDevices.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var seenAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in lines)
                {
                    var match = Regex.Match(line, @"^Device\s+([0-9A-Fa-f:]{17})\s+(.+)$");
                    if (match.Success && seenAddresses.Add(match.Groups[1].Value))
                    {
                        var address = match.Groups[1].Value;
                        var name = match.Groups[2].Value;

                        var isConnected = false;
                        var isPaired = false;
                        var type = "Unknown";

                        var resultInfo = await _commandRunner.RunAsync(
                            "bluetoothctl",
                            ["info", address],
                            TimeSpan.FromSeconds(2),
                            cancellationToken);

                        if (resultInfo.Succeeded)
                        {
                            isConnected = resultInfo.StandardOutput.Contains("Connected: yes", StringComparison.OrdinalIgnoreCase);
                            isPaired = resultInfo.StandardOutput.Contains("Paired: yes", StringComparison.OrdinalIgnoreCase);
                            if (resultInfo.StandardOutput.Contains("Icon: audio", StringComparison.OrdinalIgnoreCase) ||
                                resultInfo.StandardOutput.Contains("UUID: Audio", StringComparison.OrdinalIgnoreCase))
                            {
                                type = "Audio";
                            }
                            else if (resultInfo.StandardOutput.Contains("Icon: input", StringComparison.OrdinalIgnoreCase))
                            {
                                type = "Input";
                            }
                        }

                        devices.Add(new BluetoothDevice(address, name, isConnected, isPaired, type));
                    }
                }
            }
            else
            {
                deviceQueryFailed = true;
            }
        }

        var detailText = !isEnabled
            ? "Bluetooth is disabled."
            : deviceQueryFailed
                ? "Bluetooth is enabled, but devices could not be queried."
                : devices.Count == 0
                    ? "No Bluetooth devices found."
                    : $"{devices.Count} Bluetooth {(devices.Count == 1 ? "device" : "devices")} found.";

        return new BluetoothStatus(true, isEnabled, isDiscovering, devices, detailText);
    }

    public async Task<bool> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var action = enabled ? "on" : "off";
        var result = await _commandRunner.RunAsync(
            "bluetoothctl",
            ["power", action],
            CommandTimeout,
            cancellationToken);

        if (!result.Succeeded)
        {
            return false;
        }

        return true;
    }

    private static bool ParseShowOutput(string output)
    {
        return output.Contains("Powered: yes", StringComparison.OrdinalIgnoreCase);
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
}
