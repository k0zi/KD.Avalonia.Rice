namespace KD.Avalonia.Rice.Dialogs;

/// <summary>Result of the standard dialog buttons; <see cref="None"/> when the dialog was closed otherwise.</summary>
public enum DialogResult
{
    None,
    Ok,
    Cancel,
    Yes,
    No,
    Retry,
    Abort,
    Ignore,
    Close,
}
