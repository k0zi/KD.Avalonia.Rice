using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace KD.Avalonia.Rice.Dialogs;

/// <summary>
/// Shows <see cref="RiceDialogWindow"/>s: messages and questions with the system icons, standard or custom buttons,
/// or a custom view with its view model. Without an owner the active window of the application owns the dialog.
/// </summary>
public static class RiceDialog
{
    public static async Task<DialogResult> ShowAsync(string message, DialogIcon icon = DialogIcon.None,
        DialogButtons buttons = DialogButtons.Ok, string? title = null, Window? owner = null) =>
        await ShowAsync(new DialogOptions { Message = message, Icon = icon, Buttons = buttons.ToButtons(), Title = title }, owner)
            is DialogResult result ? result : DialogResult.None;

    public static Task ShowInfoAsync(string message, string? title = null, Window? owner = null) =>
        ShowAsync(message, DialogIcon.Information, DialogButtons.Ok, title, owner);

    public static Task ShowWarningAsync(string message, string? title = null, Window? owner = null) =>
        ShowAsync(message, DialogIcon.Warning, DialogButtons.Ok, title, owner);

    public static Task ShowErrorAsync(string message, string? title = null, Window? owner = null) =>
        ShowAsync(message, DialogIcon.Error, DialogButtons.Ok, title, owner);

    /// <summary>A Yes / No question; true when the answer is Yes.</summary>
    public static async Task<bool> AskAsync(string question, string? title = null, Window? owner = null) =>
        await ShowAsync(question, DialogIcon.Question, DialogButtons.YesNo, title, owner) == DialogResult.Yes;

    /// <summary>Shows a dialog; returns the <see cref="DialogButton.Result"/> of the button that closed it.</summary>
    public static async Task<object?> ShowAsync(DialogOptions options, Window? owner = null)
    {
        owner ??= ActiveWindow();
        var dialog = new RiceDialogWindow(options, owner?.RenderScaling ?? 1);
        if (owner is not null)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            var closed = new TaskCompletionSource();
            dialog.Closed += (_, _) => closed.TrySetResult();
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.Show();
            await closed.Task;
        }
        return dialog.Result;
    }

    /// <summary>
    /// Shows a new <typeparamref name="TView"/> with <paramref name="viewModel"/> as its data context.
    /// A view model implementing <see cref="IDialogContent"/> can close the dialog itself.
    /// </summary>
    public static Task<object?> ShowAsync<TView>(object viewModel, IEnumerable<DialogButton> buttons,
        DialogIcon icon = DialogIcon.None, string? message = null, string? title = null, Window? owner = null)
        where TView : Control, new() =>
        ShowAsync(new DialogOptions
        {
            Content = new TView { DataContext = viewModel },
            Buttons = buttons.ToList(),
            Icon = icon,
            Message = message,
            Title = title,
        }, owner);

    private static Window? ActiveWindow() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow
            : null;
}
