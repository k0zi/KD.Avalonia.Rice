using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace KD.Avalonia.Rice.Theming;

/// <summary>
/// Colors of one theme variant (light or dark). Only the four core colors are required,
/// the rest is derived from them but can be overridden with object initializers.
/// </summary>
public sealed record RicePalette(Color Accent, Color Background, Color Surface, Color Foreground)
{
    public Color AccentForeground { get; init; } = ColorMath.ContrastingForeground(Accent);
    public Color SurfaceAlt { get; init; } = ColorMath.Blend(Surface, Foreground, 0.08);
    public Color ForegroundMuted { get; init; } = ColorMath.Blend(Foreground, Background, 0.35);
    public Color Border { get; init; } = ColorMath.Blend(Background, Foreground, 0.16);
    public Color TitleBarBackground { get; init; } = Surface;
    public Color TitleBarForeground { get; init; } = Foreground;
    public Color Danger { get; init; } = Color.Parse("#E81123");

    public bool IsDark => ColorMath.Luminance(Background) < 0.5;

    /// <summary>Background, surface, accent and foreground brushes for previews.</summary>
    public IReadOnlyList<IBrush> Swatches =>
    [
        new ImmutableSolidColorBrush(Background),
        new ImmutableSolidColorBrush(Surface),
        new ImmutableSolidColorBrush(Accent),
        new ImmutableSolidColorBrush(Foreground),
    ];
}

internal static class ColorMath
{
    public static Color Blend(Color from, Color to, double amount) => Color.FromRgb(
        Lerp(from.R, to.R, amount),
        Lerp(from.G, to.G, amount),
        Lerp(from.B, to.B, amount));

    public static double Luminance(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;

    public static Color ContrastingForeground(Color background) =>
        Luminance(background) > 0.6 ? Color.Parse("#1A1A1A") : Colors.White;

    private static byte Lerp(byte a, byte b, double t) => (byte)Math.Round(a + (b - a) * t);
}
