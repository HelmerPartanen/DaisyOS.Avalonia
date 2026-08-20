using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DaisyOS.Shell.Apps.Calendar;

internal sealed class CalendarEventStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _path;

    public CalendarEventStore()
    {
        var configRoot = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(configRoot))
        {
            configRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        _path = Path.Combine(configRoot, "DaisyOS", "calendar-events.json");
    }

    public IReadOnlyList<CalendarEventRecord> Load()
    {
        try
        {
            if (!File.Exists(_path)) return Array.Empty<CalendarEventRecord>();
            return JsonSerializer.Deserialize<List<CalendarEventRecord>>(File.ReadAllText(_path), SerializerOptions)
                   ?? new List<CalendarEventRecord>();
        }
        catch (JsonException) { return Array.Empty<CalendarEventRecord>(); }
        catch (IOException) { return Array.Empty<CalendarEventRecord>(); }
    }

    public void Save(IReadOnlyCollection<CalendarEventRecord> events)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(events, SerializerOptions));
        File.Move(temporaryPath, _path, true);
    }
}

internal sealed record CalendarEventRecord(Guid Id, string Title, DateTime StartTime, DateTime EndTime);
