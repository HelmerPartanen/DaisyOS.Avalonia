using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Threading;
using DaisyOS.Core.Helpers;

namespace DaisyOS.Shell.Apps.Notes;

public class NotesViewModel : INotifyPropertyChanged
{
    private NoteItem? _selectedNote;
    private string _searchQuery = string.Empty;
    private readonly string _notesDir;
    private DispatcherTimer _autosaveTimer;

    public ObservableCollection<NoteItem> Notes { get; } = new();
    
    public ObservableCollection<NoteItem> FilteredNotes { get; } = new();

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                OnPropertyChanged();
                FilterNotes();
            }
        }
    }

    public NoteItem? SelectedNote
    {
        get => _selectedNote;
        set
        {
            if (_selectedNote != value)
            {
                if (_selectedNote != null) _selectedNote.IsSelected = false;
                _selectedNote = value;
                if (_selectedNote != null) _selectedNote.IsSelected = true;

                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedNote));
                FilterNotes(); // Resort list based on new selection
            }
        }
    }

    public bool HasSelectedNote => _selectedNote != null;

    public ICommand AddNoteCommand { get; }
    public ICommand DeleteNoteCommand { get; }
    public ICommand SelectNoteCommand { get; }
    public ICommand PinNoteCommand { get; }
    public ICommand DuplicateNoteCommand { get; }
    public ICommand SaveNoteCommand { get; }

    public NotesViewModel()
    {
        _notesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Notes");
        Directory.CreateDirectory(_notesDir);

        AddNoteCommand = new RelayCommand(_ => AddNote());
        DeleteNoteCommand = new RelayCommand(param =>
        {
            if (param is NoteItem note) DeleteNote(note);
            else if (SelectedNote != null) DeleteNote(SelectedNote);
        });
        SelectNoteCommand = new RelayCommand(param =>
        {
            if (param is NoteItem note) SelectNote(note);
        });
        PinNoteCommand = new RelayCommand(param =>
        {
            if (param is NoteItem note) 
            {
                note.IsPinned = !note.IsPinned;
                FilterNotes();
            }
        });
        DuplicateNoteCommand = new RelayCommand(param =>
        {
            if (param is NoteItem note) 
            {
                var copy = new NoteItem
                {
                    Content = note.Content,
                    Icon = note.Icon,
                    IsDirty = true
                };
                Notes.Add(copy);
                SelectedNote = copy;
                FilterNotes();
            }
        });
        SaveNoteCommand = new RelayCommand(param =>
        {
            if (param is NoteItem note) SaveNote(note);
            else if (SelectedNote != null) SaveNote(SelectedNote);
        });

        _autosaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _autosaveTimer.Tick += OnAutosaveTick;
        _autosaveTimer.Start();

        LoadNotes();
    }

    private void OnAutosaveTick(object? sender, EventArgs e)
    {
        foreach (var note in Notes.Where(n => n.IsDirty))
        {
            SaveNote(note);
        }
    }

    private void LoadNotes()
    {
        Notes.Clear();
        if (Directory.Exists(_notesDir))
        {
            var files = Directory.GetFiles(_notesDir, "*.txt");
            foreach (var file in files)
            {
                var content = File.ReadAllText(file);
                var fileInfo = new FileInfo(file);
                var note = new NoteItem
                {
                    Content = content,
                    FilePath = file,
                    LastModified = fileInfo.LastWriteTime,
                    Icon = "description"
                };
                note.IsDirty = false;
                Notes.Add(note);
            }
        }
        FilterNotes();
    }

    public void SaveNote(NoteItem note)
    {
        if (string.IsNullOrWhiteSpace(note.Content)) return;

        if (string.IsNullOrEmpty(note.FilePath))
        {
            string title = note.Title;
            if (string.IsNullOrWhiteSpace(title)) title = "Untitled";
            
            // Clean invalid chars
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                title = title.Replace(c, '_');
            }
            
            string fileName = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{title}.txt";
            note.FilePath = Path.Combine(_notesDir, fileName);
        }

        File.WriteAllText(note.FilePath, note.Content);
        note.IsDirty = false;
    }

    private void SelectNote(NoteItem? note)
    {
        SelectedNote = note;
    }

    private void FilterNotes()
    {
        var sorted = Notes.Where(n => string.IsNullOrEmpty(SearchQuery) || 
                                     n.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                     n.Content.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                          .OrderByDescending(n => n.IsPinned)
                          .ThenByDescending(n => n.LastModified)
                          .ToList();

        // Check if order is identical to prevent UI thrashing
        bool identical = FilteredNotes.Count == sorted.Count;
        if (identical)
        {
            for (int i = 0; i < sorted.Count; i++)
            {
                if (FilteredNotes[i] != sorted[i])
                {
                    identical = false;
                    break;
                }
            }
        }

        if (!identical)
        {
            FilteredNotes.Clear();
            for (int i = 0; i < sorted.Count; i++)
            {
                var note = sorted[i];
                note.ShowDivider = i < sorted.Count - 1;
                FilteredNotes.Add(note);
            }
        }
        else
        {
            for (int i = 0; i < FilteredNotes.Count; i++)
            {
                FilteredNotes[i].ShowDivider = i < FilteredNotes.Count - 1;
            }
        }
    }

    public event EventHandler? NoteCreated;

    public void AddNote()
    {
        var note = new NoteItem
        {
            Content = string.Empty,
            Icon = "description"
        };
        Notes.Add(note);
        SelectedNote = note;
        
        NoteCreated?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteNote(NoteItem note)
    {
        if (note != null)
        {
            if (!string.IsNullOrEmpty(note.FilePath) && File.Exists(note.FilePath))
            {
                File.Delete(note.FilePath);
            }
            
            Notes.Remove(note);
            FilterNotes(); // Refresh filter
            if (SelectedNote == note)
            {
                SelectedNote = null;
            }
        }
    }

    public void InsertExternalNote(string filePath)
    {
        if (File.Exists(filePath))
        {
            var content = File.ReadAllText(filePath);
            var fileInfo = new FileInfo(filePath);
            var note = new NoteItem
            {
                Content = content,
                FilePath = filePath,
                LastModified = fileInfo.LastWriteTime,
                Icon = "description"
            };
            note.IsDirty = false;
            Notes.Add(note);
            SelectedNote = note;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
