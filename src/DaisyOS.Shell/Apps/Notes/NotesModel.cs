using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DaisyOS.Shell.Apps.Notes;

public class NoteItem : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _content = string.Empty;
    private bool _isSelected;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Content
    {
        get => _content;
        set
        {
            if (_content != value)
            {
                _content = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    public string Title
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_content)) return string.Empty;
            
            // Get the first non-empty line
            var lines = _content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return string.Empty;
            
            var firstLine = lines[0].Trim();
            // Truncate to a reasonable length for the sidebar
            return firstLine.Length > 40 ? firstLine.Substring(0, 40) + "..." : firstLine;
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
