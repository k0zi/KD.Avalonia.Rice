using System.Text;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.ThemeEditor;

/// <summary>Writes a theme as a C# property in the style of <see cref="BuiltInThemes"/>.</summary>
public static class ThemeCodeWriter
{
    public static string Write(RiceTheme theme)
    {
        var code = new StringBuilder();
        code.Append($"public static RiceTheme {ToPropertyName(theme.DisplayName)} {{ get; }} = new(\"{Escape(theme.Id)}\", \"{Escape(theme.DisplayName)}\",\n");
        WritePalette(code, "Light", theme.Light, ",");
        WritePalette(code, "Dark", theme.Dark, ");");
        return code.ToString();
    }

    private static void WritePalette(StringBuilder code, string name, RicePalette palette, string end)
    {
        var core = PaletteFields.All.Where(f => f.IsCore).Select(f => Color(f.Get(palette)));
        code.Append($"    {name}: new({string.Join(", ", core)})");

        var overrides = PaletteFields.All
            .Where(f => !f.IsCore)
            .Select(f => (Field: f, Value: PaletteFields.StoredValue(palette, f)))
            .Where(x => x.Value is not null)
            .ToList();
        if (overrides.Count == 0)
        {
            code.Append(end).Append('\n');
            return;
        }

        code.Append("\n    {\n");
        foreach (var (field, value) in overrides)
            code.Append($"        {field.Name} = {Color(value!.Value)},\n");
        code.Append("    }").Append(end).Append('\n');
    }

    private static string Color(global::Avalonia.Media.Color c) => $"Color.Parse(\"{PaletteFields.ToHex(c)}\")";

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string ToPropertyName(string displayName)
    {
        var name = new StringBuilder();
        var startWord = true;
        foreach (var c in displayName)
        {
            if (!char.IsAsciiLetterOrDigit(c))
            {
                startWord = true;
                continue;
            }
            name.Append(startWord ? char.ToUpperInvariant(c) : c);
            startWord = false;
        }
        if (name.Length == 0 || char.IsAsciiDigit(name[0]))
            name.Insert(0, "Theme");
        return name.ToString();
    }
}
