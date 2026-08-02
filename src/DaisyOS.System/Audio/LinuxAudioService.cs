using System.Globalization;
using System.Text.RegularExpressions;
using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.System.Processes;

namespace DaisyOS.System.Audio;

public sealed partial class LinuxAudioService : IAudioService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(3);
    private readonly ICommandRunner _commandRunner;

    public LinuxAudioService(ICommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    public async Task<double?> GetVolumeAsync(CancellationToken cancellationToken = default)
    {
        var result = await _commandRunner.RunAsync(
            "wpctl",
            ["get-volume", "@DEFAULT_AUDIO_SINK@"],
            CommandTimeout,
            cancellationToken);

        if (!result.Succeeded)
        {
            return null;
        }

        var match = VolumeRegex().Match(result.StandardOutput);
        if (!match.Success ||
            !double.TryParse(match.Groups["volume"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rawVolume))
        {
            return null;
        }

        return Math.Clamp(rawVolume * 100, 0, 100);
    }

    public Task SetVolumeAsync(double volume, CancellationToken cancellationToken = default)
    {
        var normalized = (int)Math.Round(Math.Clamp(volume, 0, 100));
        return _commandRunner.RunAsync(
            "wpctl",
            ["set-volume", "@DEFAULT_AUDIO_SINK@", $"{normalized}%"],
            CommandTimeout,
            cancellationToken);
    }

    public async Task<string> GetDefaultDeviceNameAsync(CancellationToken cancellationToken = default)
    {
        var result = await _commandRunner.RunAsync(
            "wpctl",
            ["status"],
            CommandTimeout,
            cancellationToken);

        if (!result.Succeeded)
        {
            return "System Output";
        }

        var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool inSinks = false;
        foreach (var line in lines)
        {
            if (line.Contains("Sinks:", StringComparison.OrdinalIgnoreCase))
            {
                inSinks = true;
                continue;
            }
            if (inSinks)
            {
                if (line.Contains("Sources:", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Video", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                if (line.StartsWith("*"))
                {
                    var clean = line.TrimStart('*', ' ', '\t');
                    var firstDot = clean.IndexOf('.');
                    if (firstDot > 0 && int.TryParse(clean[..firstDot], out _))
                    {
                        clean = clean[(firstDot + 1)..].Trim();
                    }
                    var volStart = clean.IndexOf('[');
                    if (volStart > 0)
                    {
                        clean = clean[..volStart].Trim();
                    }
                    if (!string.IsNullOrWhiteSpace(clean))
                    {
                        return clean;
                    }
                }
            }
        }

        return "System Output";
    }

    public async Task<IReadOnlyList<AudioDeviceInfo>> GetAudioDevicesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _commandRunner.RunAsync(
            "wpctl",
            ["status"],
            CommandTimeout,
            cancellationToken);

        var list = new List<AudioDeviceInfo>();

        if (result.Succeeded)
        {
            var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            bool inSinks = false;
            foreach (var line in lines)
            {
                if (line.Contains("Sinks:", StringComparison.OrdinalIgnoreCase))
                {
                    inSinks = true;
                    continue;
                }
                if (inSinks)
                {
                    if (line.Contains("Sources:", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("Video", StringComparison.OrdinalIgnoreCase))
                    {
                        if (list.Count > 0) break;
                    }
                    var isDefault = line.StartsWith("*");
                    var clean = line.TrimStart('*', ' ', '\t');
                    var firstDot = clean.IndexOf('.');
                    if (firstDot > 0 && int.TryParse(clean[..firstDot], out var idVal))
                    {
                        var deviceId = idVal.ToString();
                        var name = clean[(firstDot + 1)..].Trim();
                        var volStart = name.IndexOf('[');
                        if (volStart > 0) name = name[..volStart].Trim();

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            var isBt = name.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) || name.Contains("Headphones", StringComparison.OrdinalIgnoreCase);
                            var icon = isBt ? "headphones" : name.Contains("HDMI", StringComparison.OrdinalIgnoreCase) ? "tv" : "speaker";
                            list.Add(new AudioDeviceInfo(deviceId, name, icon, isDefault, isBt));
                        }
                    }
                }
            }
        }

        if (list.Count == 0)
        {
            list.Add(new AudioDeviceInfo("default", "Internal Speakers", "speaker", true));
            list.Add(new AudioDeviceInfo("hdmi", "HDMI / DisplayPort Output", "tv", false));
            list.Add(new AudioDeviceInfo("bt", "Wireless Headphones", "headphones", false, true));
        }

        return list;
    }

    public async Task SetDefaultAudioDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        await _commandRunner.RunAsync(
            "wpctl",
            ["set-default", deviceId],
            CommandTimeout,
            cancellationToken);
    }

    [GeneratedRegex(@"Volume:\s+(?<volume>[0-9]+(?:\.[0-9]+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex VolumeRegex();
}
