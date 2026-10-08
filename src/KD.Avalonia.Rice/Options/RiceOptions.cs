using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.Options;

public sealed class RiceOptions
{
    public RiceAppInfo AppInfo { get; set; } = RiceAppInfo.Default;

    /// <summary>Themes the application offers. Defaults to every built-in theme.</summary>
    public IReadOnlyList<RiceTheme> SupportedThemes { get; set; } = BuiltInThemes.All;

    /// <summary>Theme used when nothing has been saved yet. Defaults to the first supported theme.</summary>
    public string? DefaultThemeId { get; set; }
}
