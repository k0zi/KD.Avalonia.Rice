using Avalonia;
using KD.Avalonia.Rice.Controls;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.Options;

public sealed class RiceOptions
{
    public RiceAppInfo AppInfo { get; set; } = RiceAppInfo.Default;

    /// <summary>Themes the application offers. Defaults to every built-in theme.</summary>
    public IReadOnlyList<RiceTheme> SupportedThemes { get; set; } = BuiltInThemes.All;

    /// <summary>Theme used when nothing has been saved yet. Defaults to the first supported theme.</summary>
    public string? DefaultThemeId { get; set; }

    /// <summary>
    /// Corner radius of every <c>RiceWindow</c> (0 = square corners). Defaults to 15.
    /// A single window can override it with its <c>CornerRadius</c> property.
    /// </summary>
    public CornerRadius WindowCornerRadius { get; set; } = new(15);

    /// <summary>
    /// Background material of every <c>RiceWindow</c>. Defaults to <see cref="RiceWindowMaterial.Gradient"/>.
    /// A single window can override it with its <c>Material</c> property.
    /// </summary>
    public RiceWindowMaterial WindowMaterial { get; set; } = RiceWindowMaterial.Gradient;

    /// <summary>
    /// Lets the color blobs of <see cref="RiceWindowMaterial.Gradient"/> windows drift slowly. Defaults to false.
    /// A single window can override it with its <c>IsBackgroundAnimated</c> property.
    /// </summary>
    public bool AnimateWindowBackground { get; set; }
}
