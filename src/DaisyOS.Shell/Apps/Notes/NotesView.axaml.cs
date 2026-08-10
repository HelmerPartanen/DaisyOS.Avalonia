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

    private async void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null || ViewModel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Open Note File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } },
                Avalonia.Platform.Storage.FilePickerFileTypes.All
            }
        });

        if (files.Count >= 1)
        {
            var file = files[0];
            var path = file.Path.LocalPath;
            if (!string.IsNullOrEmpty(path))
            {
                ViewModel.InsertExternalNote(path);
            }
        }
    }
}
