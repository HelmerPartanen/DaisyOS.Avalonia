using System.Text.Json;

namespace DaisyOS.Shell.Services;

/// <summary>
/// Small, resilient persistence boundary for shell-owned preferences. Hardware state is never
/// mirrored here: a successful system operation is required before its corresponding preference
/// changes.
/// </summary>
public sealed class ShellSessionState
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _path;
    private PersistedShellState _state;
    private readonly object _sync = new();

    public event EventHandler? Changed;

    public ShellSessionState(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetEnvironmentVariable("XDG_STATE_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state"),
            "DaisyOS", "shell-state.json");
        _state = Load();
    }

    public bool DoNotDisturb { get => _state.DoNotDisturb; set => Update(s => s.DoNotDisturb = value); }
    public bool GamingMode { get => _state.GamingMode; set => Update(s => s.GamingMode = value); }
    public bool ShowDateInSystemBar { get => _state.ShowDateInSystemBar; set => Update(s => s.ShowDateInSystemBar = value); }
    public double? LastKnownVolume { get => _state.LastKnownVolume; set => Update(s => s.LastKnownVolume = value is null ? null : Math.Clamp(value.Value, 0, 100)); }
    public IReadOnlyList<string> DockOrder => _state.DockOrder;

    public void SetDockOrder(IEnumerable<string> order) => Update(s => s.DockOrder = order.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList());

    /// <summary>Returns application ids ordered by real launch frequency, then most recent use.</summary>
    public IReadOnlyList<string> GetMostUsedApplicationIds(int maximumCount)
    {
        if (maximumCount <= 0)
        {
            return [];
        }

        lock (_sync)
        {
            return _state.ApplicationUsage
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && entry.Value.LaunchCount > 0)
                .OrderByDescending(entry => entry.Value.LaunchCount)
                .ThenByDescending(entry => entry.Value.LastLaunchedUtc)
                .Take(maximumCount)
                .Select(entry => entry.Key)
                .ToArray();
        }
    }

    /// <summary>Records a launch only after the system launcher accepted it.</summary>
    public void RecordApplicationLaunch(string applicationId)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return;
        }

        Update(state =>
        {
            if (!state.ApplicationUsage.TryGetValue(applicationId, out var usage))
            {
                usage = new ApplicationUsageState();
                state.ApplicationUsage[applicationId] = usage;
            }

            usage.LaunchCount++;
            usage.LastLaunchedUtc = DateTimeOffset.UtcNow;
        });
    }

    private void Update(Action<PersistedShellState> update)
    {
        lock (_sync)
        {
            update(_state);
            Save(_state);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private PersistedShellState Load()
    {
        try
        {
            if (!File.Exists(_path)) return new PersistedShellState();
            return JsonSerializer.Deserialize<PersistedShellState>(File.ReadAllText(_path), SerializerOptions) ?? new PersistedShellState();
        }
        catch (JsonException)
        {
            return new PersistedShellState();
        }
        catch (IOException)
        {
            return new PersistedShellState();
        }
        catch (UnauthorizedAccessException)
        {
            return new PersistedShellState();
        }
    }

    private void Save(PersistedShellState state)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, SerializerOptions));
            File.Move(temporaryPath, _path, overwrite: true);
        }
        catch (IOException)
        {
            // A preference write must never take down an input interaction.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class PersistedShellState
    {
        public bool DoNotDisturb { get; set; }
        public bool GamingMode { get; set; }
        public bool ShowDateInSystemBar { get; set; }
        public double? LastKnownVolume { get; set; }
        public List<string> DockOrder { get; set; } = [];
        public Dictionary<string, ApplicationUsageState> ApplicationUsage { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class ApplicationUsageState
    {
        public int LaunchCount { get; set; }
        public DateTimeOffset LastLaunchedUtc { get; set; }
    }
}
