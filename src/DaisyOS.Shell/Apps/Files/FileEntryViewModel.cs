using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Directory = global::System.IO.Directory;
using File = global::System.IO.File;
using FileInfo = global::System.IO.FileInfo;
using Path = global::System.IO.Path;
using FileAttributes = global::System.IO.FileAttributes;
using Avalonia.Media;

namespace DaisyOS.Shell.Apps.Files;

public sealed class FileEntryViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isRenaming;
    private string _name;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Path { get; private set; }
    
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsRenaming
    {
        get => _isRenaming;
        set => SetProperty(ref _isRenaming, value);
    }

    public string Extension { get; }
    public bool IsDirectory { get; }
    public DateTime LastWriteTime { get; }
    public long Length { get; }

    public string LastWriteTimeDisplay => LastWriteTime == DateTime.MinValue ? string.Empty : LastWriteTime.ToString("yyyy-MM-dd HH:mm");
    public string DisplaySize => IsDirectory ? string.Empty : FormatFileSize(Length);
    public string DisplayType { get; }
    public string MaterialIcon { get; }
    public string IconColor { get; }
    public bool IsHidden => Name.StartsWith(".");

    public FileEntryViewModel(string path)
    {
        Path = path;
        _name = global::System.IO.Path.GetFileName(path);
        if (string.IsNullOrEmpty(_name))
        {
            _name = path; // Root paths like "/"
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
            IconColor = "#F59E0B"; // Folder warm yellow accent
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
            (MaterialIcon, IconColor) = GetIconAndColor(Extension);
        }
    }

    public void UpdatePathAfterRename(string newPath)
    {
        Path = newPath;
        Name = global::System.IO.Path.GetFileName(newPath);
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

    private static (string icon, string color) GetIconAndColor(string ext) => ext switch
    {
        ".txt" or ".md" => ("description", "#9CA3AF"),
        ".cs" or ".json" or ".xml" or ".html" or ".css" or ".js" or ".ts" => ("code", "#10B981"),
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".svg" => ("image", "#3B82F6"),
        ".mp3" or ".wav" or ".flac" or ".ogg" or ".aac" => ("music_note", "#EC4899"),
        ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" => ("movie", "#8B5CF6"),
        ".zip" or ".tar" or ".gz" or ".7z" or ".rar" => ("folder_zip", "#F59E0B"),
        ".pdf" => ("picture_as_pdf", "#EF4444"),
        ".sh" or ".bash" => ("terminal", "#10B981"),
        _ => ("insert_drive_file", "#9CA3AF")
    };

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
