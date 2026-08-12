using System.Globalization;
using System.Diagnostics;
using System.Text.RegularExpressions;
using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.System.Processes;

namespace DaisyOS.System.Media;

public sealed class LinuxMediaSessionService : IMediaSessionService, IDisposable
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(1);
    private const string MprisObjectPath = "/org/mpris/MediaPlayer2";
    private const string MprisPlayerInterface = "org.mpris.MediaPlayer2.Player";
    private readonly ICommandRunner _commandRunner;
    private readonly CancellationTokenSource _watcherCancellation = new();
    private readonly Task _watcherTask;
    private string? _activePlayerName;
    private string? _activeMprisPlayerName;

    public LinuxMediaSessionService(ICommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
        _watcherTask = WatchMediaChangesAsync(_watcherCancellation.Token);
    }

    public event EventHandler? MediaChanged;

    public async Task<MediaSession?> GetCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        var playerName = await GetPreferredPlayerctlPlayerAsync(_activePlayerName, cancellationToken);
        _activePlayerName = playerName;
        var metadata = await RunPlayerctlQuietAsync(
            WithPlayer(playerName,
                "metadata", "--format", "{{title}}\u001f{{artist}}\u001f{{mpris:length}}\u001f{{mpris:artUrl}}\u001f{{playerName}}\u001f{{xesam:url}}\u001f{{mpris:trackid}}"),
            cancellationToken);

        if (metadata is null)
        {
            return await GetCurrentMprisSessionAsync(cancellationToken);
        }

        var parts = metadata.Trim().Split('\u001f');
        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]))
        {
            return null;
        }

        var statusTask = RunPlayerCommandAsync(["status"], cancellationToken);
        var positionTask = RunPlayerCommandAsync(["position"], cancellationToken);
        await Task.WhenAll(statusTask, positionTask);
        var status = await statusTask;
        var position = await positionTask;

        var durationSeconds = parts.Length > 2 ? ParseMicroseconds(parts[2]) : 0;
        var positionSeconds = ParseSeconds(position);

        return new MediaSession(
            parts[0].Trim(),
            parts[1].Trim(),
            parts.Length > 3 ? NormalizeArtPath(parts[3]) : null,
            Math.Clamp(positionSeconds, 0, Math.Max(0, durationSeconds)),
            Math.Max(0, durationSeconds),
            status.Trim().Equals("Playing", StringComparison.OrdinalIgnoreCase),
            parts.Length > 4 ? NullIfWhiteSpace(parts[4]) : null,
            FirstNonEmpty(
                parts.Length > 5 ? parts[5] : null,
                parts.Length > 6 ? parts[6] : null));
    }

    public Task PreviousAsync(CancellationToken cancellationToken = default)
    {
        return RunControlCommandAsync("previous", "Previous", cancellationToken);
    }

    public Task PlayPauseAsync(CancellationToken cancellationToken = default)
    {
        return RunControlCommandAsync("play-pause", "PlayPause", cancellationToken);
    }

    public Task NextAsync(CancellationToken cancellationToken = default)
    {
        return RunControlCommandAsync("next", "Next", cancellationToken);
    }

    private async Task<string> RunPlayerCommandAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        return await RunPlayerctlQuietAsync(WithPlayer(_activePlayerName, arguments), cancellationToken) ?? string.Empty;
    }

    private async Task RunControlCommandAsync(string playerctlCommand, string mprisMethod, CancellationToken cancellationToken)
    {
        var result = await _commandRunner.RunAsync(
            "playerctl",
            WithPlayer(_activePlayerName, playerctlCommand),
            CommandTimeout,
            cancellationToken);
        if (result.Succeeded)
        {
            return;
        }

        var playerName = await GetPreferredMprisPlayerNameAsync(_activeMprisPlayerName, cancellationToken);
        if (playerName is null)
        {
            return;
        }

        _activeMprisPlayerName = playerName;

        await RunGdbusQuietAsync(
            [
                "call",
                "--session",
                "--dest",
                playerName,
                "--object-path",
                MprisObjectPath,
                "--method",
                $"{MprisPlayerInterface}.{mprisMethod}"
            ],
            cancellationToken);
    }

    private async Task<MediaSession?> GetCurrentMprisSessionAsync(CancellationToken cancellationToken)
    {
        var playerName = await GetPreferredMprisPlayerNameAsync(_activeMprisPlayerName, cancellationToken);
        if (playerName is null)
        {
            return null;
        }

        _activeMprisPlayerName = playerName;

        var metadata = await GetMprisPropertyAsync(playerName, "Metadata", cancellationToken);
        if (metadata is null)
        {
            return null;
        }

        var title = ExtractQuotedValue(metadata, "xesam:title");
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var artist = ExtractArrayValue(metadata, "xesam:artist");
        var sourceUrl = FirstNonEmpty(
            ExtractQuotedValue(metadata, "xesam:url"),
            ExtractObjectPathValue(metadata, "mpris:trackid"),
            ExtractQuotedValue(metadata, "mpris:trackid"));
        var artPath = NormalizeArtPath(
            ExtractQuotedValue(metadata, "mpris:artUrl")
            ?? ExtractQuotedValue(metadata, "xesam:artUrl")
            ?? string.Empty);
        var durationSeconds = ParseMicroseconds(ExtractIntegerValue(metadata, "mpris:length"));
        var status = await GetMprisPropertyAsync(playerName, "PlaybackStatus", cancellationToken) ?? string.Empty;
        var position = await GetMprisPropertyAsync(playerName, "Position", cancellationToken) ?? string.Empty;
        var positionSeconds = ParseMicroseconds(ExtractVariantInteger(position));

        return new MediaSession(
            title,
            artist ?? string.Empty,
            artPath,
            Math.Clamp(positionSeconds, 0, Math.Max(0, durationSeconds)),
            Math.Max(0, durationSeconds),
            status.Contains("Playing", StringComparison.OrdinalIgnoreCase),
            playerName,
            sourceUrl);
    }

    private static async Task<IReadOnlyList<string>> GetMprisPlayerNamesAsync(CancellationToken cancellationToken)
    {
        var namesOutput = await RunGdbusQuietAsync(
            [
                "call",
                "--session",
                "--dest",
                "org.freedesktop.DBus",
                "--object-path",
                "/org/freedesktop/DBus",
                "--method",
                "org.freedesktop.DBus.ListNames"
            ],
            cancellationToken);

        if (namesOutput is null)
        {
            return [];
        }

        return Regex.Matches(namesOutput, @"'(?<name>org\.mpris\.MediaPlayer2\.[^']+)'")
            .Select(match => match.Groups["name"].Value)
            .ToArray();
    }

    private static async Task<string?> GetPreferredMprisPlayerNameAsync(
        string? currentPlayer,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(currentPlayer)
            && await IsMprisPlayerPlayingAsync(currentPlayer, cancellationToken))
        {
            return currentPlayer;
        }

        var players = await GetMprisPlayerNamesAsync(cancellationToken);
        foreach (var player in players)
        {
            if (await IsMprisPlayerPlayingAsync(player, cancellationToken))
            {
                return player;
            }
        }

        return players.FirstOrDefault();
    }

    private static async Task<bool> IsMprisPlayerPlayingAsync(string playerName, CancellationToken cancellationToken)
    {
        var status = await GetMprisPropertyAsync(playerName, "PlaybackStatus", cancellationToken);
        return status?.Contains("Playing", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static Task<string?> GetMprisPropertyAsync(
        string playerName,
        string propertyName,
        CancellationToken cancellationToken)
    {
        return RunGdbusQuietAsync(
            [
                "call",
                "--session",
                "--dest",
                playerName,
                "--object-path",
                MprisObjectPath,
                "--method",
                "org.freedesktop.DBus.Properties.Get",
                MprisPlayerInterface,
                propertyName
            ],
            cancellationToken);
    }

    private static Task<string?> RunPlayerctlQuietAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        return RunCommandQuietAsync("playerctl", arguments, cancellationToken);
    }

    private static async Task<string?> GetPreferredPlayerctlPlayerAsync(
        string? currentPlayer,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(currentPlayer))
        {
            var currentStatus = await RunPlayerctlQuietAsync(WithPlayer(currentPlayer, "status"), cancellationToken);
            if (string.Equals(currentStatus?.Trim(), "Playing", StringComparison.OrdinalIgnoreCase))
            {
                return currentPlayer;
            }
        }

        var output = await RunPlayerctlQuietAsync(["--list-all"], cancellationToken);
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var players = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? firstPlaying = null;
        string? spotify = null;

        foreach (var player in players)
        {
            if (player.Contains("spotify", StringComparison.OrdinalIgnoreCase))
            {
                spotify ??= player;
            }

            var status = await RunPlayerctlQuietAsync(WithPlayer(player, "status"), cancellationToken);
            if (!string.Equals(status?.Trim(), "Playing", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (player.Contains("spotify", StringComparison.OrdinalIgnoreCase))
            {
                return player;
            }

            firstPlaying ??= player;
        }

        return firstPlaying ?? spotify ?? players[0];
    }

    private static IReadOnlyList<string> WithPlayer(string? playerName, params string[] arguments) =>
        WithPlayer(playerName, (IReadOnlyList<string>)arguments);

    private static IReadOnlyList<string> WithPlayer(string? playerName, IReadOnlyList<string> arguments)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return arguments;
        }

        var result = new string[arguments.Count + 2];
        result[0] = "--player";
        result[1] = playerName;
        for (var i = 0; i < arguments.Count; i++)
        {
            result[i + 2] = arguments[i];
        }

        return result;
    }

    private static Task<string?> RunGdbusQuietAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        return RunCommandQuietAsync("gdbus", arguments, cancellationToken);
    }

    private static Task<string?> RunCommandQuietAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        return RunCommandQuietCoreAsync(fileName, arguments, cancellationToken);
    }

    private static async Task<string?> RunCommandQuietCoreAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
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
            return null;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(CommandTimeout);

        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);
            return process.ExitCode == 0 ? await stdoutTask : null;
        }
        catch (OperationCanceledException)
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

            return null;
        }
    }

    private async Task WatchMediaChangesAsync(CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "playerctl",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in new[]
        {
            "--all-players",
            "--follow",
            "metadata",
            "--format",
            "{{playerInstance}}\u001f{{title}}\u001f{{artist}}\u001f{{mpris:artUrl}}"
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                MediaChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or global::System.ComponentModel.Win32Exception
            or OperationCanceledException)
        {
        }
        finally
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

        // playerctl is optional. When it is unavailable, listen to MPRIS directly
        // so metadata and cover changes arrive immediately instead of waiting for
        // the periodic refresh.
        if (!cancellationToken.IsCancellationRequested)
        {
            await WatchMprisMediaChangesAsync(cancellationToken);
        }
    }

    private async Task WatchMprisMediaChangesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var playerName = await GetPreferredMprisPlayerNameAsync(_activeMprisPlayerName, cancellationToken);
            if (playerName is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                continue;
            }

            _activeMprisPlayerName = playerName;
            var startInfo = new ProcessStartInfo
            {
                FileName = "gdbus",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("monitor");
            startInfo.ArgumentList.Add("--session");
            startInfo.ArgumentList.Add("--dest");
            startInfo.ArgumentList.Add(playerName);
            startInfo.ArgumentList.Add("--object-path");
            startInfo.ArgumentList.Add(MprisObjectPath);

            using var process = new Process { StartInfo = startInfo };
            try
            {
                process.Start();
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
                    if (line is null)
                    {
                        break;
                    }

                    if (line.Contains("PropertiesChanged", StringComparison.Ordinal)
                        || line.Contains("Metadata", StringComparison.Ordinal))
                    {
                        MediaChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException
                or global::System.ComponentModel.Win32Exception
                or OperationCanceledException)
            {
                return;
            }
            finally
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

            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1500), cancellationToken);
            }
        }
    }

    public void Dispose()
    {
        _watcherCancellation.Cancel();
        try
        {
            _watcherTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
        }
        _watcherCancellation.Dispose();
    }

    private static double ParseMicroseconds(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var microseconds)
            ? microseconds / 1_000_000d
            : 0;
    }

    private static string? ExtractQuotedValue(string metadata, string key)
    {
        var match = Regex.Match(metadata, $@"'{Regex.Escape(key)}':\s+<'(?<value>(?:\\'|[^'])*)'>");
        return match.Success ? UnescapeGdbusString(match.Groups["value"].Value) : null;
    }

    private static string? ExtractArrayValue(string metadata, string key)
    {
        var match = Regex.Match(metadata, $@"'{Regex.Escape(key)}':\s+<\[(?<values>[^\]]*)\]>");
        if (!match.Success)
        {
            return null;
        }

        var values = Regex.Matches(match.Groups["values"].Value, @"'(?<value>(?:\\'|[^'])*)'")
            .Select(valueMatch => UnescapeGdbusString(valueMatch.Groups["value"].Value))
            .Where(value => !string.IsNullOrWhiteSpace(value));

        return string.Join(", ", values);
    }

    private static string? ExtractObjectPathValue(string metadata, string key)
    {
        var match = Regex.Match(
            metadata,
            $@"'{Regex.Escape(key)}':\s+<objectpath\s+'(?<value>(?:\\'|[^'])*)'>");
        return match.Success ? UnescapeGdbusString(match.Groups["value"].Value) : null;
    }

    private static string ExtractIntegerValue(string metadata, string key)
    {
        var match = Regex.Match(metadata, $@"'{Regex.Escape(key)}':\s+<int64\s+(?<value>-?\d+)>");
        return match.Success ? match.Groups["value"].Value : string.Empty;
    }

    private static string ExtractVariantInteger(string value)
    {
        var match = Regex.Match(value, @"<int64\s+(?<value>-?\d+)>");
        return match.Success ? match.Groups["value"].Value : string.Empty;
    }

    private static string UnescapeGdbusString(string value)
    {
        return value.Replace("\\'", "'", StringComparison.Ordinal);
    }

    private static double ParseSeconds(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? seconds
            : 0;
    }

    private static string? NormalizeArtPath(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            return Uri.UnescapeDataString(uri.LocalPath);
        }

        return trimmed;
    }

    private static string? NullIfWhiteSpace(string value)
    {
        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(value => value is null ? null : NullIfWhiteSpace(value))
            .FirstOrDefault(value => value is not null);
}
