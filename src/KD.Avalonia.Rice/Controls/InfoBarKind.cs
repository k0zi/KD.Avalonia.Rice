namespace KD.Avalonia.Rice.Controls;

/// <summary>Kind of a <see cref="RiceInfoBar"/>; sets its color and icon, the same as the toasts of that kind.</summary>
public enum InfoBarKind
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
