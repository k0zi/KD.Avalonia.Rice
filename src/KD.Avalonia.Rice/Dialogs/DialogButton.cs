using System.Windows.Input;

namespace KD.Avalonia.Rice.Dialogs;

/// <summary>A button of a dialog: a standard one from <see cref="For"/> or a custom caption with an optional command.</summary>
public sealed class DialogButton
{
    public DialogButton(string text, object? result = null)
    {
        Text = text;
        Result = result;
    }

    public string Text { get; set; }

    /// <summary>Executed when the button is clicked, before the dialog closes; <c>CanExecute</c> enables the button.</summary>
    public ICommand? Command { get; set; }

    public object? CommandParameter { get; set; }

    /// <summary>Value the dialog returns when this button closes it.</summary>
    public object? Result { get; set; }

    /// <summary>Clicked by Enter.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Clicked by Esc; its <see cref="Result"/> is also returned when the dialog is closed otherwise.</summary>
    public bool IsCancel { get; set; }

    /// <summary>Shown with the accent color.</summary>
    public bool IsAccent { get; set; }

    /// <summary>Closes the dialog after the command ran. Defaults to true.</summary>
    public bool ClosesDialog { get; set; } = true;

    /// <summary>
    /// A standard button with its localized caption (<see cref="RiceStrings"/>) and the result as <see cref="Result"/>;
    /// Cancel also answers Esc.
    /// </summary>
    public static DialogButton For(DialogResult result) =>
        new(Caption(result), result) { IsCancel = result == DialogResult.Cancel };

    private static string Caption(DialogResult result) => result switch
    {
        DialogResult.Ok => RiceStrings.Ok,
        DialogResult.Cancel => RiceStrings.Cancel,
        DialogResult.Yes => RiceStrings.Yes,
        DialogResult.No => RiceStrings.No,
        DialogResult.Retry => RiceStrings.Retry,
        DialogResult.Abort => RiceStrings.Abort,
        DialogResult.Ignore => RiceStrings.Ignore,
        DialogResult.Close => RiceStrings.Close,
        _ => result.ToString(),
    };
}
