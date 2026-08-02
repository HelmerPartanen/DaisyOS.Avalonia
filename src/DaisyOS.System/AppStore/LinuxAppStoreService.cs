using System.Text.RegularExpressions;
using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.System.Processes;

namespace DaisyOS.System.AppStore;

public sealed partial class LinuxAppStoreService : IAppStoreService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(15);
    private readonly ICommandRunner _commandRunner;
    private static readonly List<AppStoreItem> DefaultCatalog =
    [
        new("libreoffice", "libreoffice-fresh", "LibreOffice", "Full-featured office productivity suite.", "LibreOffice is a powerful and free office suite, used by millions of people around the world. Its clean interface and feature-rich tools help you unleash your creativity and enhance your productivity.", AppCategory.Productivity, "libreoffice-startcenter", "description", "7.6.4", "The Document Foundation", "160 MB", 4.8, true, true),
        new("firefox", "firefox", "Mozilla Firefox", "Fast, private, and customizable web browser.", "Firefox is a free and open-source web browser created by Mozilla. It offers top-tier privacy protection, tracking protection, and customizability.", AppCategory.Productivity, "firefox", "public", "121.0", "Mozilla", "75 MB", 4.9, true, true),
        new("thunderbird", "thunderbird", "Thunderbird", "Email, calendar, and task management.", "Thunderbird is a free, open-source email application that is easy to set up and customize.", AppCategory.Productivity, "thunderbird", "mail", "115.6", "Mozilla", "65 MB", 4.6, false, false),
        new("vlc", "vlc", "VLC Media Player", "Versatile multimedia player and streamer.", "VLC is a free and open-source cross-platform multimedia player that plays most multimedia files as well as DVDs, Audio CDs, VCDs, and various streaming protocols.", AppCategory.Productivity, "vlc", "play_circle", "3.0.20", "VideoLAN", "42 MB", 4.9, true, false),
        
        new("gimp", "gimp", "GIMP", "GNU Image Manipulation Program.", "GIMP is a cross-platform image editor available for GNU/Linux, macOS, Windows and more operating systems. It is free software, you can change its source code and distribute your changes.", AppCategory.Creative, "gimp", "image", "2.10.36", "GIMP Team", "120 MB", 4.7, true, true),
        new("inkscape", "inkscape", "Inkscape", "Professional vector graphics editor.", "Inkscape is a free and open-source vector graphics editor used to create vector images, primarily in Scalable Vector Graphics format.", AppCategory.Creative, "org.inkscape.Inkscape", "draw", "1.3.2", "Inkscape Project", "95 MB", 4.8, true, false),
        new("krita", "krita", "Krita", "Digital painting and 2D animation.", "Krita is a professional free and open source painting program. It is made by artists that want to see affordable art tools for everyone.", AppCategory.Creative, "org.kde.krita", "brush", "5.2.2", "Krita Foundation", "180 MB", 4.9, false, true),
        new("blender", "blender", "Blender", "3D creation suite: modeling, animation, rendering.", "Blender is the free and open source 3D creation suite. It supports the entirety of the 3D pipeline—modeling, rigging, animation, simulation, rendering, compositing and motion tracking.", AppCategory.Creative, "blender", "view_in_ar", "4.0.2", "Blender Foundation", "310 MB", 5.0, true, true),
        new("kdenlive", "kdenlive", "Kdenlive", "Non-linear video editing program.", "Kdenlive is an open source video editor. The project was started around 2002. Kdenlive is built on MLT Framework and KDE Frameworks.", AppCategory.Creative, "org.kde.kdenlive", "movie", "23.08.4", "KDE Community", "140 MB", 4.6, false, false),
        new("audacity", "audacity", "Audacity", "Multi-track audio editor and recorder.", "Audacity is an easy-to-use, multi-track audio editor and recorder for Windows, macOS, GNU/Linux and other operating systems.", AppCategory.Creative, "audacity", "graphic_eq", "3.4.2", "Audacity Team", "35 MB", 4.7, false, false),
        new("obs-studio", "obs-studio", "OBS Studio", "Free and open source software for video recording and live streaming.", "OBS Studio is equipped with a powerful API, enabling plugins and scripts to provide further customization and functionality specific to your needs.", AppCategory.Creative, "com.obsproject.Studio", "videocam", "30.0.2", "OBS Project", "110 MB", 4.9, true, true),

        new("code", "code", "Visual Studio Code", "Code editing redefined.", "Visual Studio Code is a code editor redefined and optimized for building and debugging modern web and cloud applications.", AppCategory.Development, "code", "code", "1.85.1", "Microsoft / Open Source", "115 MB", 4.9, true, true),
        new("git", "git", "Git", "Fast, scalable, distributed revision control system.", "Git is a free and open source distributed version control system designed to handle everything from small to very large projects with speed and efficiency.", AppCategory.Development, "git", "terminal", "2.43.0", "Git Community", "28 MB", 5.0, false, false),
        new("docker", "docker", "Docker", "Pack, ship and run any application as a lightweight container.", "Docker is an open platform for developing, shipping, and running applications.", AppCategory.Development, "docker", "view_in_ar", "24.0.7", "Docker Inc.", "85 MB", 4.8, true, false),
        new("nodejs", "nodejs", "Node.js", "JavaScript runtime built on Chrome's V8 engine.", "Node.js is an open-source, cross-platform JavaScript runtime environment that executes JavaScript code outside a web browser.", AppCategory.Development, "nodejs", "javascript", "20.10.0", "OpenJS Foundation", "45 MB", 4.9, false, false),
        new("python", "python", "Python", "High-level programming language.", "Python is an interpreted, high-level and general-purpose programming language. Its design philosophy emphasizes code readability.", AppCategory.Development, "python", "code_blocks", "3.11.6", "Python Software Foundation", "52 MB", 5.0, false, false),
        new("neovim", "neovim", "Neovim", "Vim-fork focused on extensibility and usability.", "Neovim is a refactor, and sometimes redraft, of Vim in the pursuit of enhancing user experience and extensibility.", AppCategory.Development, "nvim", "terminal", "0.9.4", "Neovim Core", "18 MB", 4.8, false, true),

        new("steam", "steam", "Steam", "Ultimate entertainment platform. Play, connect, create.", "Steam is a digital distribution platform for video games and software. It offers digital rights management, matchmaking, video streaming, and social networking services.", AppCategory.Gaming, "steam", "sports_esports", "1.0.0.79", "Valve", "70 MB", 4.9, true, true),
        new("lutris", "lutris", "Lutris", "Open Gaming Platform for GNU/Linux.", "Lutris helps you install and manage your games in a unified interface. It supports native Linux games, Windows games via Wine/Proton, and emulators.", AppCategory.Gaming, "net.lutris.Lutris", "gamepad", "0.5.14", "Lutris Team", "32 MB", 4.8, true, false),
        new("heroic", "heroic-games-launcher-bin", "Heroic Games Launcher", "Native GOG, Epic Games, and Amazon Games launcher.", "Heroic is an open source games launcher for Linux, Windows and macOS. It supports Epic Games, GOG, and Amazon Prime Games.", AppCategory.Gaming, "com.heroicgameslauncher.hgl", "rocket_launch", "2.12.0", "Heroic Team", "105 MB", 4.7, false, true),
        new("bottles", "bottles", "Bottles", "Easily manage Windows environments on Linux.", "Run Windows software and games on Linux easily using customized Wine environments called Bottles.", AppCategory.Gaming, "com.usebottles.bottles", "wine_bar", "51.10", "Bottles Devs", "88 MB", 4.8, false, false),
        new("mangohud", "mangohud", "MangoHud", "Vulkan and OpenGL display overlay for monitoring fps, temperatures, CPU/GPU load.", "A Vulkan and OpenGL display overlay for monitoring FPS, temperatures, CPU/GPU load and more in-game.", AppCategory.Gaming, "mangohud", "monitor_heart", "0.7.0", "FlightlessMango", "12 MB", 4.9, false, false),
        new("discord", "discord", "Discord", "All-in-one voice and text chat for gamers.", "Discord is the easiest way to talk over voice, video, and text. Talk, chat, hang out, and stay close with your friends and communities.", AppCategory.Gaming, "discord", "forum", "0.0.39", "Discord Inc.", "92 MB", 4.8, true, true),

        new("spotify", "spotify", "Spotify", "Digital music and podcast service.", "Spotify gives you instant access to millions of songs, podcasts, and video clips from creators all over the world.", AppCategory.Audio, "spotify", "music_note", "1.2.26", "Spotify AB", "110 MB", 4.8, true, true),
        new("ardour", "ardour", "Ardour", "Digital Audio Workstation.", "Ardour is a hard disk recorder and digital audio workstation application. It runs on GNU/Linux, macOS, FreeBSD and Windows.", AppCategory.Audio, "org.ardour.Ardour", "equalizer", "8.2.0", "Ardour Community", "125 MB", 4.7, false, false),
        new("lmms", "lmms", "LMMS", "Produce music with your computer.", "LMMS is a sound generation, synthesizer, sample playing and MIDI sequencing application for Linux, macOS and Windows.", AppCategory.Audio, "io.lmms.LMMS", "piano", "1.2.2", "LMMS Developers", "64 MB", 4.6, false, false)
    ];

    public LinuxAppStoreService(ICommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    public async Task<IReadOnlyList<AppStoreItem>> GetFeaturedAppsAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<AppStoreItem>();
        foreach (var app in DefaultCatalog.Where(a => a.IsFeatured))
        {
            var isInstalled = await IsPackageInstalledAsync(app.PackageName, cancellationToken);
            items.Add(app with { InstallState = isInstalled ? AppInstallState.Installed : AppInstallState.NotInstalled });
        }
        return items;
    }

    public async Task<IReadOnlyList<AppStoreItem>> GetAppsByCategoryAsync(AppCategory category, CancellationToken cancellationToken = default)
    {
        if (category == AppCategory.Discover)
        {
            return await GetFeaturedAppsAsync(cancellationToken);
        }
        if (category == AppCategory.Installed)
        {
            return await GetInstalledAppsAsync(cancellationToken);
        }

        var liveItems = await SearchAppStreamAsync(CategoryQuery(category), category, cancellationToken);
        if (liveItems.Count > 0)
        {
            return liveItems;
        }

        var items = new List<AppStoreItem>();
        foreach (var app in DefaultCatalog.Where(a => a.Category == category))
        {
            var isInstalled = await IsPackageInstalledAsync(app.PackageName, cancellationToken);
            items.Add(app with { InstallState = isInstalled ? AppInstallState.Installed : AppInstallState.NotInstalled });
        }
        return items;
    }

    public async Task<IReadOnlyList<AppStoreItem>> SearchAppsAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetFeaturedAppsAsync(cancellationToken);
        }

        var trimmed = query.Trim();
        var appStreamMatches = await SearchAppStreamAsync(trimmed, AppCategory.Productivity, cancellationToken);
        var catalogMatches = DefaultCatalog.Where(a =>
            a.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
            a.PackageName.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
            a.Summary.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
            a.Description.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
        ).ToList();

        // Also query pacman for live Arch Linux search
        var pacmanResult = await _commandRunner.RunAsync(
            "pacman",
            ["-Ss", trimmed],
            CommandTimeout,
            cancellationToken);

        if (pacmanResult.Succeeded && !string.IsNullOrWhiteSpace(pacmanResult.StandardOutput))
        {
            var lines = pacmanResult.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length - 1; i += 2)
            {
                var pkgHeader = lines[i];
                var pkgDesc = lines[i + 1].Trim();

                var match = PacmanHeaderRegex().Match(pkgHeader);
                if (match.Success)
                {
                    var pkgName = match.Groups["name"].Value;
                    var version = match.Groups["version"].Value;

                    if (!catalogMatches.Any(m => m.PackageName.Equals(pkgName, StringComparison.OrdinalIgnoreCase)))
                    {
                        var isInstalled = pkgHeader.Contains("[installed]", StringComparison.OrdinalIgnoreCase);
                        catalogMatches.Add(new AppStoreItem(
                            pkgName,
                            pkgName,
                            pkgName,
                            pkgDesc,
                            $"{pkgDesc} (Arch Linux Package {pkgName} v{version})",
                            AppCategory.Productivity,
                            "",
                            "extension",
                            version,
                            "Arch Linux Repository",
                            "Arch Package",
                            4.5,
                            false,
                            false,
                            isInstalled ? AppInstallState.Installed : AppInstallState.NotInstalled
                        ));
                    }
                }
            }
        }

        var results = new List<AppStoreItem>(appStreamMatches);
        foreach (var app in catalogMatches.Take(30))
        {
            if (results.Any(result => result.Id.Equals(app.Id, StringComparison.OrdinalIgnoreCase) || result.PackageName.Equals(app.PackageName, StringComparison.OrdinalIgnoreCase))) continue;
            var isInstalled = await IsPackageInstalledAsync(app.PackageName, cancellationToken);
            results.Add(app with { InstallState = isInstalled ? AppInstallState.Installed : AppInstallState.NotInstalled });
        }

        return results.Take(30).ToArray();
    }

    public async Task<IReadOnlyList<AppStoreItem>> GetInstalledAppsAsync(CancellationToken cancellationToken = default)
    {
        var installed = new List<AppStoreItem>();
        foreach (var app in DefaultCatalog)
        {
            if (await IsPackageInstalledAsync(app.PackageName, cancellationToken))
            {
                installed.Add(app with { InstallState = AppInstallState.Installed });
            }
        }
        return installed;
    }

    public async Task<bool> InstallAppAsync(string packageName, Action<double>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        progressCallback?.Invoke(0.2);
        var result = await _commandRunner.RunAsync(
            "pkexec",
            ["pacman", "-S", "--noconfirm", packageName],
            TimeSpan.FromMinutes(5),
            cancellationToken);

        progressCallback?.Invoke(1.0);
        return result.Succeeded;
    }

    public async Task<bool> UninstallAppAsync(string packageName, CancellationToken cancellationToken = default)
    {
        var result = await _commandRunner.RunAsync(
            "pkexec",
            ["pacman", "-R", "--noconfirm", packageName],
            TimeSpan.FromMinutes(3),
            cancellationToken);

        return result.Succeeded;
    }

    public async Task<bool> IsPackageInstalledAsync(string packageName, CancellationToken cancellationToken = default)
    {
        var result = await _commandRunner.RunAsync(
            "pacman",
            ["-Qq", packageName],
            TimeSpan.FromSeconds(3),
            cancellationToken);

        return result.Succeeded && !string.IsNullOrWhiteSpace(result.StandardOutput);
    }

    private async Task<IReadOnlyList<AppStoreItem>> SearchAppStreamAsync(string query, AppCategory category, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var result = await _commandRunner.RunAsync("appstreamcli", ["search", "--details", query], CommandTimeout, cancellationToken);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.StandardOutput)) return [];

        var items = new List<AppStoreItem>();
        foreach (var block in result.StandardOutput.Split("---", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = block.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split(':', 2, StringSplitOptions.TrimEntries))
                .Where(parts => parts.Length == 2)
                .GroupBy(parts => parts[0], StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First()[1], StringComparer.OrdinalIgnoreCase);
            if (!fields.TryGetValue("Identifier", out var id) || !fields.TryGetValue("Name", out var name)) continue;
            if (!fields.TryGetValue("Package", out var packageName)) packageName = id;
            if (items.Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase))) continue;

            fields.TryGetValue("Summary", out var summary);
            fields.TryGetValue("Homepage", out var homepage);
            fields.TryGetValue("Icon", out var icon);
            var installed = await IsPackageInstalledAsync(packageName, cancellationToken);
            items.Add(new AppStoreItem(id, packageName, name, summary ?? "No description available.", summary ?? "No description available.", category,
                icon ?? id, "apps", string.Empty, homepage ?? "System repository", string.Empty, 0, false, false,
                installed ? AppInstallState.Installed : AppInstallState.NotInstalled));
            if (items.Count == 30) break;
        }
        return items;
    }

    private static string CategoryQuery(AppCategory category) => category switch
    {
        AppCategory.Productivity => "office",
        AppCategory.Creative => "graphics",
        AppCategory.Development => "development",
        AppCategory.Gaming => "game",
        AppCategory.Audio => "audio",
        _ => "application"
    };

    [GeneratedRegex(@"^(?<repo>[^\/]+)\/(?<name>[^\s]+)\s+(?<version>[^\s]+)", RegexOptions.IgnoreCase)]
    private static partial Regex PacmanHeaderRegex();
}
