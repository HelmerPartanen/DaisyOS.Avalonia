using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DaisyOS.Shell.Apps.Notes;

public class NotesViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
