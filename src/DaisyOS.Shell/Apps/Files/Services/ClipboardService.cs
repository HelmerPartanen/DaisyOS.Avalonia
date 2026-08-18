using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DaisyOS.Shell.Apps.Files.Services;

public enum ClipboardOperation
{
    None,
    Copy,
    Cut
}

public static class ClipboardService
{
    public static List<string> CurrentFiles { get; private set; } = new();
    public static ClipboardOperation CurrentOperation { get; private set; } = ClipboardOperation.None;

    public static async Task SetFilesAsync(IEnumerable<string> paths, ClipboardOperation operation)
    {
        CurrentFiles = paths.ToList();
        CurrentOperation = operation;

        var clipboard = GetClipboard();
        if (clipboard != null)
        {
            var uris = string.Join(Environment.NewLine, CurrentFiles);
            await clipboard.SetTextAsync(uris);
        }
    }

    public static async Task<(List<string> files, ClipboardOperation operation)> GetFilesAsync()
    {
        await Task.CompletedTask;
        return (CurrentFiles, CurrentOperation);
    }

    public static void ClearInternal()
    {
        CurrentFiles.Clear();
        CurrentOperation = ClipboardOperation.None;
    }

    private static IClipboard? GetClipboard()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow?.Clipboard;
        }
        return null;
    }
}
