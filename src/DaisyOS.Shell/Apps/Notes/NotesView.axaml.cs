using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace DaisyOS.Shell.Apps.Notes;

public partial class NotesView : UserControl
{
    private NotesViewModel? ViewModel => DataContext as NotesViewModel;

    public NotesView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (ViewModel != null)
        {
            ViewModel.NoteCreated -= OnNoteCreated;
            ViewModel.NoteCreated += OnNoteCreated;
        }
    }

    private void OnNoteCreated(object? sender, EventArgs e)
    {
        // Focus the content text box when a new note is created, delaying to allow layout to complete
        Dispatcher.UIThread.Post(() => 
        {
            var contentTextBox = this.FindControl<TextBox>("ContentTextBox");
            contentTextBox?.Focus();
        });
    }
}
