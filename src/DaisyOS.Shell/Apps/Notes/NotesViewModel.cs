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

    public ObservableCollection<NoteItem> Notes { get; } = new();

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
            if (param is NoteItem note) SelectedNote = note;
        });
    }

    public event EventHandler? NoteCreated;

    public void AddNote(string? title = null, string? content = null)
    {
        var note = new NoteItem
        {
            Content = content ?? string.Empty,
            Icon = "description"
        };
        Notes.Add(note);
        SelectedNote = note;
        
        NoteCreated?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteNote(NoteItem note)
    {
        Notes.Remove(note);
        if (SelectedNote == note)
        {
            SelectedNote = Notes.FirstOrDefault();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
