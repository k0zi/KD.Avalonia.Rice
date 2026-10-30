using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using KD.Avalonia.Rice.Dialogs;

namespace KD.Avalonia.Rice.Notifications;

/// <summary>
/// Shows toasts: short, non-blocking notifications stacked in a corner of a window that close themselves.
/// Without an owner the active window of the application shows them (else its main window); in a single view
/// application (browser, mobile) the top level of its main view.
/// Can be called from any thread.
/// </summary>
public static class RiceToast
{
    public static RiceToastHandle ShowInfo(string message, string? title = null, ToastAction? action = null, TopLevel? owner = null) =>
        Show(new ToastOptions { Kind = ToastKind.Info, Message = message, Title = title, Action = action }, owner);

    public static RiceToastHandle ShowSuccess(string message, string? title = null, ToastAction? action = null, TopLevel? owner = null) =>
        Show(new ToastOptions { Kind = ToastKind.Success, Message = message, Title = title, Action = action }, owner);

    public static RiceToastHandle ShowWarning(string message, string? title = null, ToastAction? action = null, TopLevel? owner = null) =>
        Show(new ToastOptions { Kind = ToastKind.Warning, Message = message, Title = title, Action = action }, owner);

    public static RiceToastHandle ShowError(string message, string? title = null, ToastAction? action = null, TopLevel? owner = null) =>
        Show(new ToastOptions { Kind = ToastKind.Error, Message = message, Title = title, Action = action }, owner);

    public static RiceToastHandle Show(ToastOptions options, TopLevel? owner = null)
    {
        var handle = new RiceToastHandle();
        if (Dispatcher.UIThread.CheckAccess())
            ShowCore(options, owner, handle);
        else
            Dispatcher.UIThread.Post(() => ShowCore(options, owner, handle));
        return handle;
    }

    /// <summary>Closes every toast of the window (default: the active one).</summary>
    public static void CloseAll(TopLevel? owner = null) => Dispatcher.UIThread.Post(() =>
    {
        if ((owner ?? ActiveTopLevel()) is { } topLevel)
            RiceToastHost.For(topLevel)?.CloseAll();
    });

    // A single view application has no window, only the top level its main view is in.
    private static TopLevel? ActiveTopLevel() =>
        RiceDialog.ActiveWindow()
        ?? (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime { MainView: { } view }
            ? TopLevel.GetTopLevel(view)
            : null);

    private static void ShowCore(ToastOptions options, TopLevel? owner, RiceToastHandle handle)
    {
        if ((owner ?? ActiveTopLevel()) is not { } topLevel || RiceToastHost.For(topLevel) is not { } host)
        {
            handle.SetClosed();
            return;
        }
        var toast = new RiceToastItem(options);
        handle.Attach(toast);
        host.Show(toast);
    }
}
