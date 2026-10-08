using System.Text.Json;
using System.Text.Json.Serialization;

namespace KD.Avalonia.Rice.Theming;

/// <summary>Persists the selected theme id to <c>$XDG_CONFIG_HOME/&lt;appId&gt;/rice.json</c>.</summary>
internal sealed class ThemeSettingsStore(string appId)
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appId, "rice.json");

    public string? LoadThemeId()
    {
        try
        {
            if (!File.Exists(_path))
                return null;
            using var stream = File.OpenRead(_path);
            return JsonSerializer.Deserialize(stream, ThemeSettingsContext.Default.ThemeSettings)?.ThemeId;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void SaveThemeId(string themeId)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            using var stream = File.Create(_path);
            JsonSerializer.Serialize(stream, new ThemeSettings(themeId), ThemeSettingsContext.Default.ThemeSettings);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Losing the theme choice is not worth crashing the app.
        }
    }
}

internal sealed record ThemeSettings(string ThemeId);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ThemeSettings))]
internal sealed partial class ThemeSettingsContext : JsonSerializerContext;
