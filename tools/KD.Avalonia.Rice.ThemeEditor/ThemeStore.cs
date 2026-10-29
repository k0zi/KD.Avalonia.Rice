using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Media;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.ThemeEditor;

/// <summary>
/// Saves themes as JSON files (<c>&lt;id&gt;.json</c>) to <c>&lt;ApplicationData&gt;/kd-rice-theme-editor/themes</c>.
/// Only the core colors and the overridden optional colors are written.
/// </summary>
public static class ThemeStore
{
    public const string AppId = "kd-rice-theme-editor";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppId, "themes");

    public static IReadOnlyList<RiceTheme> LoadAll()
    {
        if (!Directory.Exists(Folder))
            return [];

        var themes = new List<RiceTheme>();
        foreach (var path in Directory.EnumerateFiles(Folder, "*.json"))
        {
            try
            {
                themes.Add(Read(File.ReadAllText(path)));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException
                                          or FormatException or ArgumentException or InvalidOperationException)
            {
                // A broken file is skipped rather than blocking the editor.
            }
        }
        return themes.OrderBy(t => t.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static string Save(RiceTheme theme)
    {
        Directory.CreateDirectory(Folder);
        var path = PathOf(theme.Id);
        File.WriteAllText(path, Write(theme));
        return path;
    }

    public static void Delete(string id) => File.Delete(PathOf(id));

    public static string Write(RiceTheme theme)
    {
        var json = new JsonObject
        {
            ["id"] = theme.Id,
            ["displayName"] = theme.DisplayName,
            ["light"] = WritePalette(theme.Light),
            ["dark"] = WritePalette(theme.Dark),
        };
        return json.ToJsonString(WriteOptions);
    }

    public static RiceTheme Read(string text)
    {
        var json = JsonNode.Parse(text)?.AsObject() ?? throw new JsonException("Empty theme file.");
        return new RiceTheme(
            (string?)json["id"] ?? throw new JsonException("Missing id."),
            (string?)json["displayName"] ?? throw new JsonException("Missing displayName."),
            ReadPalette(json["light"]),
            ReadPalette(json["dark"]));
    }

    /// <summary>Theme id from a display name: lower case letters and digits separated by dashes.</summary>
    public static string ToId(string name)
    {
        var id = new StringBuilder();
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
                id.Append(c);
            else if (id.Length > 0 && id[^1] != '-')
                id.Append('-');
        }
        return id.ToString().TrimEnd('-');
    }

    private static string PathOf(string id) => Path.Combine(Folder, id + ".json");

    private static JsonObject WritePalette(RicePalette palette)
    {
        var json = new JsonObject();
        foreach (var field in PaletteFields.All)
        {
            if (PaletteFields.StoredValue(palette, field) is { } color)
                json[field.Key] = PaletteFields.ToHex(color);
        }
        return json;
    }

    private static RicePalette ReadPalette(JsonNode? node)
    {
        var json = node?.AsObject() ?? throw new JsonException("Missing palette.");
        return PaletteFields.Build(f => (string?)json[f.Key] is { } hex ? Color.Parse(hex) : null);
    }
}
