using System;
using Directory = global::System.IO.Directory;
using File = global::System.IO.File;
using FileInfo = global::System.IO.FileInfo;
using Path = global::System.IO.Path;
using FileAttributes = global::System.IO.FileAttributes;
using Avalonia.Media;

namespace DaisyOS.Shell.Apps.Files;

public sealed class FileEntryViewModel
{
    public string Path { get; }
    public string Name { get; }
    public string Extension { get; }
    public bool IsDirectory { get; }
    public DateTime LastWriteTime { get; }
    public long Length { get; }

    public string LastWriteTimeDisplay => LastWriteTime == DateTime.MinValue ? string.Empty : LastWriteTime.ToString("yyyy-MM-dd HH:mm");
    public string DisplaySize => IsDirectory ? string.Empty : FormatFileSize(Length);
    public string DisplayType { get; }
    public string MaterialIcon { get; }
    public IBrush IconBrush { get; }

    public FileEntryViewModel(string path)
    {
        Path = path;
        Name = global::System.IO.Path.GetFileName(path);
        if (string.IsNullOrEmpty(Name))
        {
            Name = path; // Root paths like "/"
        }

        try
        {
            var attr = File.GetAttributes(path);
            IsDirectory = attr.HasFlag(FileAttributes.Directory);
        }
        catch
        {
            IsDirectory = Directory.Exists(path);
        }

        if (IsDirectory)
        {
            Extension = string.Empty;
            Length = 0;
            DisplayType = "File folder";
            MaterialIcon = "folder";
            IconBrush = SolidColorBrush.Parse("#F59E0B"); // Folder warm yellow accent
            try
            {
                LastWriteTime = Directory.GetLastWriteTime(path);
            }
            catch
            {
                LastWriteTime = DateTime.MinValue;
            }
        }
        else
        {
            Extension = global::System.IO.Path.GetExtension(path).ToLowerInvariant();
            try
            {
                var info = new FileInfo(path);
                Length = info.Length;
                LastWriteTime = info.LastWriteTime;
            }
            catch
            {
                Length = 0;
                LastWriteTime = DateTime.MinValue;
            }

            DisplayType = GetDisplayType(Extension);
            (MaterialIcon, IconBrush) = GetIconAndBrush(Extension);
        }
    }

    private static string GetDisplayType(string ext) => ext switch
    {
        ".txt" => "Text Document",
        ".md" => "Markdown Document",
        ".cs" => "C# Source File",
        ".json" => "JSON Document",
        ".xml" => "XML Document",
        ".html" or ".htm" => "HTML Web Page",
        ".css" => "Cascading Style Sheet",
        ".js" or ".ts" => "JavaScript / TypeScript File",
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".svg" => "Image File",
        ".mp3" or ".wav" or ".flac" or ".ogg" or ".aac" => "Audio File",
        ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" => "Video File",
        ".zip" or ".tar" or ".gz" or ".7z" or ".rar" => "Archive File",
        ".pdf" => "PDF Document",
        ".sh" or ".bash" => "Shell Script",
        ".exe" or ".dll" or ".so" => "Executable / Library",
        _ when string.IsNullOrEmpty(ext) => "File",
        _ => $"{ext.TrimStart('.').ToUpperInvariant()} File"
    };

    private static (string icon, IBrush brush) GetIconAndBrush(string ext) => ext switch
    {
        ".txt" or ".md" => ("description", SolidColorBrush.Parse("#9CA3AF")),
        ".cs" or ".json" or ".xml" or ".html" or ".css" or ".js" or ".ts" => ("code", SolidColorBrush.Parse("#10B981")),
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".svg" => ("image", SolidColorBrush.Parse("#3B82F6")),
        ".mp3" or ".wav" or ".flac" or ".ogg" or ".aac" => ("music_note", SolidColorBrush.Parse("#EC4899")),
        ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" => ("movie", SolidColorBrush.Parse("#8B5CF6")),
        ".zip" or ".tar" or ".gz" or ".7z" or ".rar" => ("folder_zip", SolidColorBrush.Parse("#F59E0B")),
        ".pdf" => ("picture_as_pdf", SolidColorBrush.Parse("#EF4444")),
        ".sh" or ".bash" => ("terminal", SolidColorBrush.Parse("#10B981")),
        _ => ("insert_drive_file", SolidColorBrush.Parse("#9CA3AF"))
    };

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }
}
