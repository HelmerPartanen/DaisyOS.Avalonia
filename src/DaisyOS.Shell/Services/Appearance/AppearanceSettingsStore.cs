using System.Text.Json;

namespace DaisyOS.Shell.Services.Appearance;

/// <summary>Small, shell-owned store for appearance preferences.</summary>
public sealed class AppearanceSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;

    public AppearanceSettingsStore(string? path = null)
    {
        _path = path ?? GetDefaultPath();
    }

    public AppearanceSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new AppearanceSettings();
            }

            return JsonSerializer.Deserialize<AppearanceSettings>(File.ReadAllText(_path), SerializerOptions)
                   ?? new AppearanceSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Failed to load appearance preferences: {ex.Message}");
            return new AppearanceSettings();
        }
    }

    public async Task SaveAsync(AppearanceSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        var temporaryPath = _path + ".tmp";
        var json = JsonSerializer.Serialize(settings, SerializerOptions);

        await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
        File.Move(temporaryPath, _path, true);
    }

    private static string GetDefaultPath()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(configHome))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            configHome = Path.Combine(home, ".config");
        }

        return Path.Combine(configHome, "DaisyOS", "appearance.json");
    }
}
