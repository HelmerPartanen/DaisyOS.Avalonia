using System;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;

using System.Collections.Concurrent;

namespace DaisyOS.Shell.ViewModels;

public static class DesktopItemLoader
{
    private static readonly ConcurrentDictionary<string, Avalonia.Media.IImage?> _imageCache = new();
    public static string ResolveIconPath(string iconName)
    {
        if (string.IsNullOrWhiteSpace(iconName)) return string.Empty;
        if (File.Exists(iconName)) return iconName;

        var extensions = new[] { ".png", ".svg", ".xpm" };
        var searchPaths = new[]
        {
            "/usr/share/pixmaps/",
            "/var/lib/flatpak/exports/share/icons/hicolor/128x128/apps/",
            "/var/lib/flatpak/exports/share/icons/hicolor/64x64/apps/",
            "/var/lib/flatpak/exports/share/icons/hicolor/48x48/apps/",
            "/var/lib/flatpak/exports/share/icons/hicolor/256x256/apps/",
            "/var/lib/flatpak/exports/share/icons/hicolor/scalable/apps/",
            "/usr/share/icons/hicolor/128x128/apps/",
            "/usr/share/icons/hicolor/64x64/apps/",
            "/usr/share/icons/hicolor/48x48/apps/",
            "/usr/share/icons/hicolor/256x256/apps/",
            "/usr/share/icons/hicolor/scalable/apps/",
            "/usr/share/icons/breeze/apps/128/",
            "/usr/share/icons/breeze/apps/64/",
            "/usr/share/icons/breeze/apps/48/",
            "/usr/share/icons/breeze/apps/256/",
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.local/share/icons/hicolor/128x128/apps/",
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.local/share/icons/hicolor/64x64/apps/",
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.local/share/icons/hicolor/48x48/apps/"
        };

        foreach (var path in searchPaths)
        {
            foreach (var ext in extensions)
            {
                var fullPath = Path.Combine(path, iconName + ext);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }
        
        return string.Empty;
    }

    public static Avalonia.Media.IImage? LoadBitmapSafe(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        return _imageCache.GetOrAdd(path, LoadBitmapInternal);
    }

    private static Avalonia.Media.IImage? LoadBitmapInternal(string path)
    {
        try
        {
            if (path.StartsWith("avares://"))
            {
                var uri = new Uri(path);
                if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    var svg = Avalonia.Svg.Skia.SvgSource.Load(path, null);
                    return new Avalonia.Svg.Skia.SvgImage { Source = svg };
                }
                else
                {
                    using var stream = Avalonia.Platform.AssetLoader.Open(uri);
                    return new Bitmap(stream);
                }
            }
            else if (File.Exists(path))
            {
                if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    var svg = Avalonia.Svg.Skia.SvgSource.Load(path, null);
                    return new Avalonia.Svg.Skia.SvgImage { Source = svg };
                }
                else
                {
                    return new Bitmap(path);
                }
            }
        }
        catch 
        { 
            // Ignore format exceptions
        }
        return null;
    }

    public static (string name, string iconPath) ParseDesktopFile(string filePath)
    {
        string name = Path.GetFileNameWithoutExtension(filePath);
        string iconPath = string.Empty;

        try
        {
            bool inDesktopEntry = false;
            foreach (var line in File.ReadLines(filePath))
            {
                var trimmed = line.Trim();
                if (trimmed == "[Desktop Entry]")
                {
                    inDesktopEntry = true;
                    continue;
                }
                else if (trimmed.StartsWith("["))
                {
                    inDesktopEntry = false;
                }

                if (inDesktopEntry)
                {
                    if (trimmed.StartsWith("Name=", StringComparison.OrdinalIgnoreCase))
                    {
                        name = trimmed.Substring(5).Trim();
                    }
                    else if (trimmed.StartsWith("Icon=", StringComparison.OrdinalIgnoreCase))
                    {
                        var iconValue = trimmed.Substring(5).Trim();
                        iconPath = ResolveIconPath(iconValue);
                    }
                }
            }
        }
        catch { }

        return (name, iconPath);
    }
}
