using Avalonia.Media;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.ThemeEditor;

/// <summary>One editable color of a <see cref="RicePalette"/>. Core colors are required, the rest is derived unless set.</summary>
public sealed record PaletteField(string Name, string Label, Func<RicePalette, Color> Get, Func<RicePalette, Color, RicePalette>? Set)
{
    public bool IsCore => Set is null;

    /// <summary>Camel case name, used as the JSON key.</summary>
    public string Key => char.ToLowerInvariant(Name[0]) + Name[1..];
}

public static class PaletteFields
{
    public static IReadOnlyList<PaletteField> All { get; } =
    [
        new(nameof(RicePalette.Accent), "Accent", p => p.Accent, null),
        new(nameof(RicePalette.Background), "Background", p => p.Background, null),
        new(nameof(RicePalette.Surface), "Surface", p => p.Surface, null),
        new(nameof(RicePalette.Foreground), "Foreground", p => p.Foreground, null),
        new(nameof(RicePalette.AccentForeground), "Text on accent", p => p.AccentForeground, (p, c) => p with { AccentForeground = c }),
        new(nameof(RicePalette.SurfaceAlt), "Surface (alternate)", p => p.SurfaceAlt, (p, c) => p with { SurfaceAlt = c }),
        new(nameof(RicePalette.ForegroundMuted), "Muted text", p => p.ForegroundMuted, (p, c) => p with { ForegroundMuted = c }),
        new(nameof(RicePalette.Border), "Border", p => p.Border, (p, c) => p with { Border = c }),
        new(nameof(RicePalette.TitleBarBackground), "Title bar", p => p.TitleBarBackground, (p, c) => p with { TitleBarBackground = c }),
        new(nameof(RicePalette.TitleBarForeground), "Title bar text", p => p.TitleBarForeground, (p, c) => p with { TitleBarForeground = c }),
        new(nameof(RicePalette.Danger), "Danger", p => p.Danger, (p, c) => p with { Danger = c }),
        new(nameof(RicePalette.DangerForeground), "Text on danger", p => p.DangerForeground, (p, c) => p with { DangerForeground = c }),
    ];

    /// <summary>Builds a palette; <paramref name="valueOf"/> returns null for optional colors that stay derived.</summary>
    public static RicePalette Build(Func<PaletteField, Color?> valueOf)
    {
        Color Core(int index) => valueOf(All[index]) ?? throw new ArgumentException($"{All[index].Name} is required.");

        // Derived colors are computed when the record is created, so the core colors go into the constructor.
        var palette = new RicePalette(Core(0), Core(1), Core(2), Core(3));
        foreach (var field in All.Where(f => !f.IsCore))
        {
            if (valueOf(field) is { } color)
                palette = field.Set!(palette, color);
        }
        return palette;
    }

    /// <summary>The value worth storing: core colors always, optional ones only when they differ from the derived value.</summary>
    public static Color? StoredValue(RicePalette palette, PaletteField field)
    {
        var value = field.Get(palette);
        if (field.IsCore)
            return value;

        // Text on danger is derived from the danger color, which may be set itself.
        var plain = new RicePalette(palette.Accent, palette.Background, palette.Surface, palette.Foreground);
        var reference = field.Name == nameof(RicePalette.Danger) ? plain : plain with { Danger = palette.Danger };
        return value == field.Get(reference) ? null : value;
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
