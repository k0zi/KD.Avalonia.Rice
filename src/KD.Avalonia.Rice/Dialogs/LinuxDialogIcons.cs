using System.Diagnostics;

namespace KD.Avalonia.Rice.Dialogs;

/// <summary>
/// Finds the PNG file of a dialog icon in the desktop's icon theme (freedesktop Icon Theme Specification).
/// SVG only themes yield nothing, the dialog then falls back to its Material icon.
/// </summary>
internal static class LinuxDialogIcons
{
    public static string? Find(DialogIcon icon, int size, int scale)
    {
        try
        {
            return FindIcon(IconNames(icon), CurrentTheme(), SearchRoots(), size, scale);
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static string[] IconNames(DialogIcon icon) => icon switch
    {
        DialogIcon.Question => ["dialog-question", "gtk-dialog-question"],
        DialogIcon.Information => ["dialog-information", "gtk-dialog-info"],
        DialogIcon.Warning => ["dialog-warning", "gtk-dialog-warning"],
        DialogIcon.Error => ["dialog-error", "gtk-dialog-error"],
        _ => [],
    };

    /// <summary>Looks the names up in the theme, the themes it inherits, hicolor, and at last unthemed in the roots.</summary>
    internal static string? FindIcon(IReadOnlyList<string> names, string theme, IReadOnlyList<string> roots, int size, int scale)
    {
        if (names.Count == 0)
            return null;
        // Breadth first over the inherited themes; hicolor is always the last one.
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new List<string> { theme };
        for (var i = 0; i < pending.Count; i++)
        {
            if (!visited.Add(pending[i]))
                continue;
            if (ReadIndex(pending[i], roots) is { } index)
            {
                foreach (var name in names)
                {
                    if (FindInTheme(name, index, size, scale) is { } path)
                        return path;
                }
                pending.AddRange(index.Inherits.Where(t => t != "hicolor"));
            }
            if (i == pending.Count - 1 && !visited.Contains("hicolor"))
                pending.Add("hicolor");
        }
        foreach (var root in roots)
        {
            foreach (var name in names)
            {
                var path = Path.Combine(root, name + ".png");
                if (File.Exists(path))
                    return path;
            }
        }
        return null;
    }

    private static string? FindInTheme(string name, ThemeIndex index, int size, int scale)
    {
        string? best = null;
        var bestScore = int.MaxValue;
        foreach (var (directory, dirSize, dirScale) in index.Directories)
        {
            var score = Score(dirSize, dirScale, size, scale);
            if (score >= bestScore)
                continue;
            foreach (var themeDir in index.ThemeDirs)
            {
                var path = Path.Combine(themeDir, directory, name + ".png");
                if (File.Exists(path))
                {
                    best = path;
                    bestScore = score;
                    break;
                }
            }
        }
        return best;
    }

    // The exact size and scale first, then by pixels the closest larger one (scaled down nicely), then the closest smaller one.
    private static int Score(int dirSize, int dirScale, int size, int scale)
    {
        if (dirSize == size && dirScale == scale)
            return 0;
        int pixels = dirSize * dirScale, wanted = size * scale;
        return pixels >= wanted ? 1 + pixels - wanted : 10_000 + wanted - pixels;
    }

    internal sealed record ThemeIndex(IReadOnlyList<string> ThemeDirs, IReadOnlyList<string> Inherits,
        IReadOnlyList<(string Directory, int Size, int Scale)> Directories);

    /// <summary>Reads index.theme of the theme; the theme's files may be spread over every root.</summary>
    internal static ThemeIndex? ReadIndex(string theme, IReadOnlyList<string> roots)
    {
        var themeDirs = roots.Select(root => Path.Combine(root, theme)).Where(Directory.Exists).ToList();
        var indexFile = themeDirs.Select(dir => Path.Combine(dir, "index.theme")).FirstOrDefault(File.Exists);
        if (indexFile is null)
            return null;

        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        Dictionary<string, string>? current = null;
        foreach (var raw in File.ReadLines(indexFile))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;
            if (line[0] == '[' && line[^1] == ']')
            {
                current = new Dictionary<string, string>(StringComparer.Ordinal);
                sections[line[1..^1]] = current;
            }
            else if (current is not null && line.IndexOf('=') is var eq and > 0)
            {
                current[line[..eq].Trim()] = line[(eq + 1)..].Trim();
            }
        }
        if (!sections.TryGetValue("Icon Theme", out var header))
            return null;

        var directories = new List<(string, int, int)>();
        foreach (var key in new[] { "Directories", "ScaledDirectories" })
        {
            foreach (var directory in List(header, key))
            {
                if (!sections.TryGetValue(directory, out var section) || !Int(section, "Size", out var size))
                    continue;
                var scale = Int(section, "Scale", out var s) ? s : 1;
                directories.Add((directory, size, scale));
            }
        }
        return new ThemeIndex(themeDirs, List(header, "Inherits"), directories);
    }

    private static string[] List(Dictionary<string, string> section, string key) =>
        section.TryGetValue(key, out var value)
            ? value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

    private static bool Int(Dictionary<string, string> section, string key, out int value)
    {
        value = 0;
        return section.TryGetValue(key, out var text) && int.TryParse(text, out value) && value > 0;
    }

    private static IReadOnlyList<string> SearchRoots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrEmpty(dataHome))
            dataHome = Path.Combine(home, ".local", "share");
        var dataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
        if (string.IsNullOrEmpty(dataDirs))
            dataDirs = "/usr/local/share:/usr/share";

        var roots = new List<string> { Path.Combine(home, ".icons"), Path.Combine(dataHome, "icons") };
        roots.AddRange(dataDirs.Split(':', StringSplitOptions.RemoveEmptyEntries).Select(dir => Path.Combine(dir, "icons")));
        roots.Add("/usr/share/pixmaps");
        return roots.Distinct().Where(Directory.Exists).ToList();
    }

    private static string CurrentTheme()
    {
        var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "";
        if (desktop.Contains("KDE", StringComparison.OrdinalIgnoreCase))
            return KdeTheme() ?? "breeze";
        return GSettingsTheme() ?? KdeTheme() ?? "hicolor";
    }

    private static string? KdeTheme()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrEmpty(configHome))
            configHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        var file = Path.Combine(configHome, "kdeglobals");
        if (!File.Exists(file))
            return null;
        var inIcons = false;
        foreach (var raw in File.ReadLines(file))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
                inIcons = line == "[Icons]";
            else if (inIcons && line.StartsWith("Theme=", StringComparison.Ordinal))
                return line["Theme=".Length..].Trim();
        }
        return null;
    }

    private static string? GSettingsTheme()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("gsettings", "get org.gnome.desktop.interface icon-theme")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
                return null;
            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(2000) || process.ExitCode != 0)
                return null;
            var theme = output.Trim().Trim('\'', '"');
            return theme.Length > 0 ? theme : null;
        }
        catch (Exception)
        {
            // gsettings is not installed.
            return null;
        }
    }
}
