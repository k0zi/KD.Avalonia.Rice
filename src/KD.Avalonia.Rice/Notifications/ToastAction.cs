using System.Windows.Input;

namespace KD.Avalonia.Rice.Notifications;

/// <summary>Button of a toast, e.g. "Undo" or "Open"; clicking it runs the action and closes the toast.</summary>
public sealed class ToastAction
{
    public ToastAction(string text, ICommand? command = null, object? commandParameter = null)
    {
        Text = text;
        Command = command;
        CommandParameter = commandParameter;
    }

    public ToastAction(string text, Action execute) : this(text) => Execute = execute;

    public string Text { get; }

    public ICommand? Command { get; }

    public object? CommandParameter { get; }

    /// <summary>Runs after <see cref="Command"/>.</summary>
    public Action? Execute { get; }
}
