using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DaisyOS.Shell.Apps.Notes;

public class NoteItem : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _content = string.Empty;
    private bool _isSelected;
    private string? _filePath;
    private bool _isDirty;
    private bool _isPinned;
    private DateTime _lastModified = DateTime.Now;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath != value)
            {
                _filePath = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _showDivider;
    public bool ShowDivider
    {
        get => _showDivider;
        set
        {
            if (_showDivider != value)
            {
                _showDivider = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty != value)
            {
                _isDirty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FontStyle));
            }
        }
    }

    public string FontStyle => IsDirty ? "Italic" : "Normal";

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (_isPinned != value)
            {
                _isPinned = value;
                OnPropertyChanged();
            }
        }
    }

    public DateTime LastModified
    {
        get => _lastModified;
        set
        {
            if (_lastModified != value)
            {
                _lastModified = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastModifiedFormatted));
            }
        }
    }

    public string LastModifiedFormatted => LastModified.ToString("MMM d, yyyy  HH:mm");

    private string? _cachedTitle;

    public string Content
    {
        get => _content;
        set
        {
            if (_content != value)
            {
                _content = value;
                _cachedTitle = null; // Invalidate cached title
                IsDirty = true;
                LastModified = DateTime.Now;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    public string Title
    {
        get
        {
            if (_cachedTitle != null) return _cachedTitle;

            if (string.IsNullOrWhiteSpace(_content))
            {
                _cachedTitle = string.Empty;
                return _cachedTitle;
            }

            var lines = _content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                _cachedTitle = string.Empty;
                return _cachedTitle;
            }

            var firstLine = lines[0].Trim();
            _cachedTitle = firstLine.Length > 40 ? firstLine.Substring(0, 40) + "..." : firstLine;
            return _cachedTitle;
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public string Icon { get; set; } = "description";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
