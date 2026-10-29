namespace KD.Avalonia.Rice.Notifications;

/// <summary>Kind of a toast; sets its color and icon.</summary>
public enum ToastKind
{
    /// <summary>The accent color of the theme.</summary>
    Info,

    /// <summary>Green.</summary>
    Success,

    /// <summary>Amber.</summary>
    Warning,

    /// <summary>The danger color of the theme (red).</summary>
    Error,
}
