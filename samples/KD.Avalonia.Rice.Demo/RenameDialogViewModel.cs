using System.ComponentModel;
using KD.Avalonia.Rice.Dialogs;

namespace KD.Avalonia.Rice.Demo;

/// <summary>View model of the custom dialog sample: validates the name and closes the dialog with it.</summary>
public sealed class RenameDialogViewModel : INotifyPropertyChanged, IDialogContent
{
    private string _name;

    public RenameDialogViewModel(string name)
    {
        _name = name;
        RenameCommand = new RelayCommand(() => CloseRequested?.Invoke(this, Name.Trim()), () => IsValid);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<object?>? CloseRequested;

    public RelayCommand RenameCommand { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
                return;
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Error)));
            RenameCommand.RaiseCanExecuteChanged();
        }
    }

    public string? Error => IsValid ? null : "The name cannot be empty or contain / characters.";

    private bool IsValid => !string.IsNullOrWhiteSpace(Name) && !Name.Contains('/');
}
