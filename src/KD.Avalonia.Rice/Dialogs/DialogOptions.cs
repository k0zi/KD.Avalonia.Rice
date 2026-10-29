using Avalonia.Controls.Templates;

namespace KD.Avalonia.Rice.Dialogs;

/// <summary>Everything a <see cref="RiceDialogWindow"/> shows.</summary>
public sealed class DialogOptions
{
    /// <summary>Window title; not displayed (the dialog has no title bar), used by the task switcher and screen readers.</summary>
    public string? Title { get; set; }

    public string? Message { get; set; }

    public DialogIcon Icon { get; set; }

    /// <summary>Custom content below the message: a control, or a view model shown through <see cref="ContentTemplate"/> or the application's data templates.</summary>
    public object? Content { get; set; }

    public IDataTemplate? ContentTemplate { get; set; }

    public IReadOnlyList<DialogButton> Buttons { get; set; } = DialogButtons.Ok.ToButtons();

    public double Width { get; set; } = 420;

    public double MaxWidth { get; set; } = double.PositiveInfinity;
}
