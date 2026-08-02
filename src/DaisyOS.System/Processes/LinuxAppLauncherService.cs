using DaisyOS.Core.Models;
using DaisyOS.Core.Services;

namespace DaisyOS.System.Processes;

public sealed class LinuxAppLauncherService : IAppLauncherService
{
    private const int MaxApps = 240;
    private readonly ILogService _log;
    private readonly SafeProcessLauncher _launcher;
    private readonly Lazy<IReadOnlyList<AppEntry>> _apps;

    public LinuxAppLauncherService(ILogService? log = null)
    {
        _log = log ?? NullLogService.Instance;
        _launcher = new SafeProcessLauncher(_log);
        _apps = new Lazy<IReadOnlyList<AppEntry>>(DiscoverApps);
    }

    public IReadOnlyList<AppEntry> GetAvailableApps() => _apps.Value;

    public Task<AppLaunchResult> LaunchAsync(AppEntry app, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<AppLaunchResult>(cancellationToken);
        }

        var result = app.LaunchKind switch
        {
            AppLaunchKind.Uri => LaunchUri(app),
            AppLaunchKind.DesktopEntry or AppLaunchKind.DirectCommand => LaunchCommand(app),
            _ => new AppLaunchResult(false, $"{app.Name} cannot be launched by the system service.")
        };

        return Task.FromResult(result);
    }

    private AppLaunchResult LaunchUri(AppEntry app)
    {
        if (string.IsNullOrWhiteSpace(app.LaunchTarget))
        {
            return new AppLaunchResult(false, $"{app.Name} has no launch target.");
        }

        var result = _launcher.Launch("xdg-open", [app.LaunchTarget]);
        return result.Succeeded
            ? new AppLaunchResult(true, $"{app.Name} launch requested.", result.ProcessId)
            : result;
    }

    private AppLaunchResult LaunchCommand(AppEntry app)
    {
        if (string.IsNullOrWhiteSpace(app.LaunchTarget))
        {
            return new AppLaunchResult(false, $"{app.Name} has no launch command.");
        }

        var result = _launcher.Launch(app.LaunchTarget, app.LaunchArguments ?? []);
        return result.Succeeded
            ? new AppLaunchResult(true, $"{app.Name} launch requested.", result.ProcessId)
            : result;
    }

    private IReadOnlyList<AppEntry> DiscoverApps()
    {
        var apps = new List<AppEntry>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in GetApplicationDirectories())
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.desktop", SearchOption.AllDirectories))
            {
                if (apps.Count >= MaxApps)
                {
                    return apps
                        .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToArray();
                }

                var app = TryReadDesktopEntry(file);
                if (app is null || !seenIds.Add(app.Id))
                {
                    continue;
                }

                apps.Add(app);
            }
        }

        return apps
            .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private AppEntry? TryReadDesktopEntry(string file)
    {
        Dictionary<string, string>? entry = null;
        var inDesktopEntry = false;

        try
        {
            foreach (var rawLine in File.ReadLines(file))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    inDesktopEntry = string.Equals(line, "[Desktop Entry]", StringComparison.Ordinal);
                    if (inDesktopEntry)
                    {
                        entry ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }

                    continue;
                }

                if (!inDesktopEntry || entry is null)
                {
                    continue;
                }

                var separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var key = line[..separator];
                if (!IsSupportedKey(key))
                {
                    continue;
                }

                entry[key] = line[(separator + 1)..].Trim();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Log(LogLevel.Warning, $"Could not read desktop entry: {file}", ex);
            return null;
        }

        if (entry is null
            || !IsDesktopApplication(entry)
            || !TryGet(entry, "Name", out var name)
            || !TryGet(entry, "Exec", out var exec))
        {
            return null;
        }

        var command = DesktopExecParser.Parse(exec);
        if (command is null || command.Count == 0 || !IsSafeCommand(command[0]))
        {
            return null;
        }

        var id = Path.GetFileName(file);
        var description = TryGet(entry, "Comment", out var comment) ? comment : "Desktop application";
        var icon = TryGet(entry, "Icon", out var iconValue) ? iconValue : string.Empty;
        var categories = TryGet(entry, "Categories", out var catValue)
            ? catValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => c.Trim())
                .Where(c => c.Length > 0)
                .ToArray()
            : [];

        return new AppEntry(
            id,
            name,
            description,
            icon,
            IsPinned: false,
            AppLaunchKind.DesktopEntry,
            command[0],
            command.Skip(1).ToArray(),
            categories);
    }

    private static IEnumerable<string> GetApplicationDirectories()
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(dataHome))
        {
            dataHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        yield return Path.Combine(dataHome, "applications");
        yield return "/usr/local/share/applications";
        yield return "/usr/share/applications";
    }

    private static bool IsDesktopApplication(Dictionary<string, string> entry)
    {
        return TryGet(entry, "Type", out var type)
            && string.Equals(type, "Application", StringComparison.OrdinalIgnoreCase)
            && !IsTruthy(entry, "NoDisplay")
            && !IsTruthy(entry, "Hidden")
            && !IsTruthy(entry, "Terminal");
    }

    private static bool IsSupportedKey(string key)
    {
        return key is "Name" or "Comment" or "Icon" or "Exec" or "NoDisplay" or "Hidden" or "Terminal" or "Type" or "Categories";
    }

    private static bool IsTruthy(Dictionary<string, string> entry, string key)
    {
        return TryGet(entry, key, out var value)
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGet(Dictionary<string, string> entry, string key, out string value)
    {
        return entry.TryGetValue(key, out value!) && !string.IsNullOrWhiteSpace(value);
    }

    private static bool IsSafeCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command) || command.Contains('\0', StringComparison.Ordinal))
        {
            return false;
        }

        return Path.IsPathFullyQualified(command) || !command.Contains('/', StringComparison.Ordinal);
    }
}
