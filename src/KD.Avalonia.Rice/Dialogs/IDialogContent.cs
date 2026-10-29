namespace KD.Avalonia.Rice.Dialogs;

/// <summary>Implemented by a dialog's view model to close the dialog itself, e.g. after validating its input.</summary>
public interface IDialogContent
{
    /// <summary>Closes the dialog; the argument is the dialog's result.</summary>
    event EventHandler<object?>? CloseRequested;
}
