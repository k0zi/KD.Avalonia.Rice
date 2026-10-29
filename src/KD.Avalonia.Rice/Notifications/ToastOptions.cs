using Avalonia.Controls.Templates;

namespace KD.Avalonia.Rice.Notifications;

/// <summary>Everything a <see cref="RiceToastItem"/> shows.</summary>
public sealed class ToastOptions
{
    public ToastKind Kind { get; set; }

    public string? Title { get; set; }

    public string? Message { get; set; }

    /// <summary>Custom content below the message: a control, or a view model shown through <see cref="ContentTemplate"/> or the application's data templates.</summary>
    public object? Content { get; set; }

    public IDataTemplate? ContentTemplate { get; set; }

    /// <summary>
    /// Time until the toast closes itself, paused while the pointer is over the toasts.
    /// <c>null</c>: <see cref="DefaultDuration"/> of the kind; <see cref="TimeSpan.Zero"/>: stays until closed.
    /// </summary>
    public TimeSpan? Duration { get; set; }

    public bool ShowCloseButton { get; set; } = true;

    /// <summary>Shows the remaining time as a thin bar at the bottom.</summary>
    public bool ShowProgress { get; set; } = true;

    public ToastAction? Action { get; set; }

    /// <summary>Called when the toast itself (not one of its buttons) is clicked; the toast closes then.</summary>
    public Action? OnClick { get; set; }

    /// <summary>Called once the toast is closed, by the user, its timer or code.</summary>
    public Action? OnClosed { get; set; }

    /// <summary>5 seconds, errors 8 seconds: they are read more carefully.</summary>
    public static TimeSpan DefaultDuration(ToastKind kind) =>
        kind == ToastKind.Error ? TimeSpan.FromSeconds(8) : TimeSpan.FromSeconds(5);

    internal TimeSpan EffectiveDuration => Duration ?? DefaultDuration(Kind);
}
