using DaisyOS.Core.Models;
using DaisyOS.Shell.Services;
using Xunit;

namespace DaisyOS.Tests;

public sealed class JsonSettingsServiceTests
{
    [Fact]
    public void MissingAndMalformedFilesUseDefaults()
    {
        using var directory = new TemporaryDirectory();
        var missingPath = Path.Combine(directory.Path, "missing", "settings.json");
        var missing = new JsonSettingsService(missingPath);

        Assert.Equal(ThemeMode.Dark, missing.ThemeMode);
        Assert.Equal(72, missing.Volume);
        Assert.True(missing.NotificationToastsEnabled);
        Assert.Equal(AccentColor.Blue, missing.AccentColor);
        Assert.False(missing.HighContrast);
        Assert.False(missing.ReduceMotion);
        Assert.Equal(SystemBarItemDefaults.All, missing.VisibleSystemBarItems);
        Assert.Equal(MediaWidgetSize.Basic, missing.MediaWidgetSize);
        Assert.False(missing.UseAdaptiveMediaTint);
        Assert.True(missing.AutomaticUpdateChecks);
        Assert.Empty(missing.UpdateCheckHistory);
        Assert.Equal(ShellSettings.DefaultWallpaperUri, missing.WallpaperUri);
        Assert.Equal(ShellSettings.DefaultProfilePictureUri, missing.ProfilePictureUri);

        var malformedPath = Path.Combine(directory.Path, "malformed.json");
        File.WriteAllText(malformedPath, "{not-json");
        var malformed = new JsonSettingsService(malformedPath);

        Assert.Equal(ThemeMode.Dark, malformed.ThemeMode);
        Assert.Equal(72, malformed.Volume);
    }

    [Fact]
    public void PackagedDefaultsSeedAUserWithoutOverridingTheirSettings()
    {
        using var directory = new TemporaryDirectory();
        var userPath = Path.Combine(directory.Path, "user", "settings.json");
        var administratorPath = Path.Combine(directory.Path, "etc", "settings.json");
        var vendorPath = Path.Combine(directory.Path, "vendor", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(administratorPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(vendorPath)!);
        File.WriteAllText(administratorPath, """{"Volume": 31, "ThemeMode": "Light"}""");
        File.WriteAllText(vendorPath, """{"Volume": 44, "ThemeMode": "Dark"}""");

        var seeded = new JsonSettingsService(userPath, [administratorPath, vendorPath]);
        Assert.Equal(31, seeded.Volume);
        Assert.Equal(ThemeMode.Light, seeded.ThemeMode);

        seeded.Volume = 57;
        var reloaded = new JsonSettingsService(userPath, [administratorPath, vendorPath]);
        Assert.Equal(57, reloaded.Volume);
        Assert.Equal(ThemeMode.Light, reloaded.ThemeMode);
    }

    [Fact]
    public void InvalidPackagedDefaultFallsBackToNextCandidate()
    {
        using var directory = new TemporaryDirectory();
        var invalidPath = Path.Combine(directory.Path, "invalid.json");
        var validPath = Path.Combine(directory.Path, "valid.json");
        File.WriteAllText(invalidPath, "{not-json");
        File.WriteAllText(validPath, """{"Volume": 46}""");

        var settings = new JsonSettingsService(
            Path.Combine(directory.Path, "user", "settings.json"),
            [invalidPath, validPath]);

        Assert.Equal(46, settings.Volume);
    }

    [Fact]
    public void SettingsPersistAndReloadFromExplicitPath()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "nested", "settings.json");
        var settings = new JsonSettingsService(path)
        {
            ThemeMode = ThemeMode.Light,
            AccentColor = AccentColor.Purple,
            HighContrast = true,
            ReduceMotion = true,
            Volume = 35,
            DockPosition = DockPosition.Bottom,
            VisibleSystemBarItems = [SystemBarItem.Media, SystemBarItem.Clock],
            MediaWidgetSize = MediaWidgetSize.Compact,
            UseAdaptiveMediaTint = true,
            ShowDateInSystemBar = true,
            NotificationToastsEnabled = false,
            DoNotDisturb = true,
            GamingMode = true,
            AutomaticUpdateChecks = false,
            UpdateCheckHistory =
            [
                new UpdateCheckRecord(DateTimeOffset.Parse("2026-07-13T08:00:00Z"), 3, true, false, "3 updates were available.")
            ],
            WallpaperUri = "  file:///wallpaper.jpg  ",
            ProfilePictureUri = "  /tmp/profile.png  ",
            PinnedDockApps = [CreateApp("files", true)],
            DockItemOrder = ["files", "launcher", "settings", "FILES", ""],
            RecentApps = [CreateApp("terminal", true)]
            ,
            FavoriteAppIds = ["files", "terminal"],
            PinnedFileManagerLocations =
            [
                new FileManagerLocation("Projects", "/tmp/projects"),
                new FileManagerLocation("Duplicate", "/tmp/projects")
            ]
        };

        var loaded = new JsonSettingsService(path);

        Assert.Equal(ThemeMode.Light, loaded.ThemeMode);
        Assert.Equal(AccentColor.Purple, loaded.AccentColor);
        Assert.True(loaded.HighContrast);
        Assert.True(loaded.ReduceMotion);
        Assert.Equal(35, loaded.Volume);
        Assert.Equal(DockPosition.Bottom, loaded.DockPosition);
        Assert.Equal([SystemBarItem.Media, SystemBarItem.Clock], loaded.VisibleSystemBarItems);
        Assert.Equal(MediaWidgetSize.Compact, loaded.MediaWidgetSize);
        Assert.True(loaded.UseAdaptiveMediaTint);
        Assert.True(loaded.ShowDateInSystemBar);
        Assert.False(loaded.NotificationToastsEnabled);
        Assert.True(loaded.DoNotDisturb);
        Assert.True(loaded.GamingMode);
        Assert.False(loaded.AutomaticUpdateChecks);
        Assert.Equal(3, Assert.Single(loaded.UpdateCheckHistory).PendingCount);
        Assert.Equal("file:///wallpaper.jpg", loaded.WallpaperUri);
        Assert.Equal("/tmp/profile.png", loaded.ProfilePictureUri);
        Assert.True(Assert.Single(loaded.PinnedDockApps).IsPinned);
        Assert.Equal(["files", "settings"], loaded.DockItemOrder);
        Assert.False(Assert.Single(loaded.RecentApps).IsPinned);
        Assert.Equal(["files", "terminal"], loaded.FavoriteAppIds);
        var location = Assert.Single(loaded.PinnedFileManagerLocations!);
        Assert.Equal("Projects", location.Name);
        Assert.Equal("/tmp/projects", location.Path);
    }

    [Fact]
    public void ValuesAndAppCollectionsAreNormalized()
    {
        using var directory = new TemporaryDirectory();
        var settings = new JsonSettingsService(Path.Combine(directory.Path, "settings.json"));
        var recent = Enumerable.Range(0, 15).Select(index => CreateApp($"app-{index}", true)).ToList();
        settings.Volume = 150;
        settings.PinnedDockApps = [CreateApp("files", false), CreateApp("FILES", false)];
        settings.RecentApps = recent;
        settings.FavoriteAppIds = ["files", "FILES", "", "terminal"];
        settings.WallpaperUri = " ";
        settings.PinnedFileManagerLocations =
        [
            new FileManagerLocation("Invalid", "bad\0path"),
            new FileManagerLocation("Valid", "/tmp/valid")
        ];

        Assert.Equal(100, settings.Volume);
        Assert.Single(settings.PinnedDockApps);
        Assert.True(settings.PinnedDockApps[0].IsPinned);
        Assert.Equal(12, settings.RecentApps.Count);
        Assert.All(settings.RecentApps, app => Assert.False(app.IsPinned));
        Assert.Equal(["files", "terminal"], settings.FavoriteAppIds);
        Assert.Equal(ShellSettings.DefaultWallpaperUri, settings.WallpaperUri);
        Assert.Equal("Valid", Assert.Single(settings.PinnedFileManagerLocations!).Name);
    }

    [Fact]
    public void UpdateHistoryIsNewestFirstAndBounded()
    {
        using var directory = new TemporaryDirectory();
        var settings = new JsonSettingsService(Path.Combine(directory.Path, "settings.json"));
        var start = DateTimeOffset.Parse("2026-07-01T00:00:00Z");

        settings.UpdateCheckHistory = Enumerable.Range(0, 25)
            .Select(index => new UpdateCheckRecord(start.AddDays(index), index, true, false, "Checked"))
            .Append(new UpdateCheckRecord(default, 99, false, false, "Invalid"))
            .ToArray();

        Assert.Equal(20, settings.UpdateCheckHistory.Count);
        Assert.Equal(24, settings.UpdateCheckHistory[0].PendingCount);
        Assert.Equal(5, settings.UpdateCheckHistory[^1].PendingCount);
    }

    [Fact]
    public void SettingsChangedOnlyFiresForEffectiveChanges()
    {
        using var directory = new TemporaryDirectory();
        var settings = new JsonSettingsService(Path.Combine(directory.Path, "settings.json"));
        var changeCount = 0;
        settings.SettingsChanged += (_, _) => changeCount++;

        settings.ThemeMode = ThemeMode.Dark;
        settings.AccentColor = AccentColor.Blue;
        settings.HighContrast = false;
        settings.ReduceMotion = false;
        settings.Volume = 72;
        settings.DockPosition = DockPosition.Bottom;
        Assert.Equal(0, changeCount);

        settings.ThemeMode = ThemeMode.Light;
        settings.AccentColor = AccentColor.Green;
        settings.HighContrast = true;
        settings.ReduceMotion = true;
        settings.Volume = -10;
        Assert.Equal(5, changeCount);
    }

    private static AppEntry CreateApp(string id, bool pinned) =>
        new(id, id, "Test app", "icon", pinned, AppLaunchKind.DirectCommand, "/bin/true", ["--test"]);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), "daisyos-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
