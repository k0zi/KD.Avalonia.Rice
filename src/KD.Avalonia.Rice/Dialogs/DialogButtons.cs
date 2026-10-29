namespace KD.Avalonia.Rice.Dialogs;

/// <summary>Standard button sets of a dialog.</summary>
public enum DialogButtons
{
    Ok,
    OkCancel,
    YesNo,
    YesNoCancel,
    RetryCancel,
    AbortRetryIgnore,
    Close,
}

public static class DialogButtonsExtensions
{
    /// <summary>The buttons of the set in display order; the first one is the default and accented.</summary>
    public static IReadOnlyList<DialogButton> ToButtons(this DialogButtons buttons)
    {
        DialogResult[] results = buttons switch
        {
            DialogButtons.OkCancel => [DialogResult.Ok, DialogResult.Cancel],
            DialogButtons.YesNo => [DialogResult.Yes, DialogResult.No],
            DialogButtons.YesNoCancel => [DialogResult.Yes, DialogResult.No, DialogResult.Cancel],
            DialogButtons.RetryCancel => [DialogResult.Retry, DialogResult.Cancel],
            DialogButtons.AbortRetryIgnore => [DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore],
            DialogButtons.Close => [DialogResult.Close],
            _ => [DialogResult.Ok],
        };
        var list = new DialogButton[results.Length];
        for (var i = 0; i < results.Length; i++)
        {
            list[i] = DialogButton.For(results[i]);
            list[i].IsDefault = list[i].IsAccent = i == 0;
        }
        // Esc answers Cancel, else No; a single button (OK / Close) answers it too. Abort / Retry / Ignore has none.
        var cancel = Array.IndexOf(results, DialogResult.Cancel);
        if (cancel < 0)
            cancel = results.Length == 1 ? 0 : Array.IndexOf(results, DialogResult.No);
        if (cancel >= 0)
            list[cancel].IsCancel = true;
        return list;
    }
}
