using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DaisyOS.Core.Helpers;

namespace DaisyOS.Shell.Apps.Notes;

public class NotesViewModel : INotifyPropertyChanged
{
    private NoteItem? _selectedNote;
    private string _searchQuery = string.Empty;

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
            }
        }
    }

    public bool HasSelectedNote => _selectedNote != null;

    public ICommand AddNoteCommand { get; }
    public ICommand DeleteNoteCommand { get; }
    public ICommand SelectNoteCommand { get; }

    public NotesViewModel()
    {
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
    }

    private void SelectNote(NoteItem? note)
    {
        SelectedNote = note;
    }

    private void FilterNotes()
    {
        FilteredNotes.Clear();
        foreach (var note in Notes)
        {
            if (string.IsNullOrWhiteSpace(SearchQuery) || 
                note.Content.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) || 
                note.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
            {
                FilteredNotes.Add(note);
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
        FilterNotes(); // Refresh filter to include new note
        SelectedNote = note;
        
        NoteCreated?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteNote(NoteItem note)
    {
        if (note != null)
        {
            Notes.Remove(note);
            FilterNotes(); // Refresh filter
            if (SelectedNote == note)
            {
                SelectedNote = null;
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
