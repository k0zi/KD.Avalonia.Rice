using System.Text;
using System.Text.RegularExpressions;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>A trimmed, non-empty, non-comment source line with its 1-based number.</summary>
internal readonly record struct PlantUmlLine(int Number, string Text)
{
    public override string ToString() => $"{Number}: {Text}";
}

/// <summary>Lines of one <c>@startuml</c> … <c>@enduml</c> block.</summary>
internal sealed class PlantUmlBlock(int startLine, string? name)
{
    public int StartLine { get; } = startLine;

    public string? Name { get; } = name;

    /// <summary>Diagram kind from the <c>' rice:kind=…</c> comment, which PlantUML itself ignores.</summary>
    public UmlDiagramKind? KindHint { get; set; }

    public List<PlantUmlLine> Lines { get; } = [];
}

/// <summary>Reads the lines of a block one after the other; multi-line constructs pull their continuation lines.</summary>
internal sealed class PlantUmlCursor(IReadOnlyList<PlantUmlLine> lines)
{
    private int _index;

    public bool HasMore => _index < lines.Count;

    public PlantUmlLine Current => lines[_index];

    /// <summary>Line number for diagnostics at the end of the block.</summary>
    public int LastNumber => lines.Count > 0 ? lines[^1].Number : 0;

    public PlantUmlLine Next() => lines[_index++];

    public bool TryPeek(out PlantUmlLine line)
    {
        line = HasMore ? lines[_index] : default;
        return HasMore;
    }

    /// <summary>Collects lines until one matches <paramref name="end"/> (consumed, not returned).</summary>
    public List<string> ReadUntil(Regex end, out bool found)
    {
        var result = new List<string>();
        while (HasMore)
        {
            var line = Next();
            if (end.IsMatch(line.Text))
            {
                found = true;
                return result;
            }
            result.Add(line.Text);
        }
        found = false;
        return result;
    }
}

internal static partial class PlantUmlText
{
    /// <summary>Removes surrounding quotes and turns <c>\n</c> into line breaks.</summary>
    public static string Unescape(string text)
    {
        text = text.Trim();
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
            text = text[1..^1];
        if (!text.Contains('\\'))
            return text;

        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                var next = text[i + 1];
                if (next is 'n' or 'r' or 'l')
                {
                    sb.Append('\n');
                    i++;
                    continue;
                }
                if (next == 't')
                {
                    sb.Append('\t');
                    i++;
                    continue;
                }
                if (next == '\\')
                {
                    sb.Append('\\');
                    i++;
                    continue;
                }
            }
            sb.Append(text[i]);
        }
        return sb.ToString();
    }

    /// <summary>Line breaks as <c>\n</c>, so the text fits on one PlantUML line.</summary>
    public static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\r\n", "\n").Replace("\n", "\\n");

    public static bool IsSimpleId(string text) => SimpleIdRegex().IsMatch(text);

    /// <summary>The text as it can stand in a reference: as is when it is a simple identifier, quoted otherwise.</summary>
    public static string Ref(string text) => IsSimpleId(text) ? text : Quote(text);

    public static string Quote(string text) => "\"" + Escape(text).Replace("\"", "'") + "\"";

    /// <summary>Removes <c>#color</c>, <c>$tag</c> and <c>##[style]</c> decorations PlantUML allows after declarations.</summary>
    public static string StripDecorations(string text) => DecorationRegex().Replace(text, "").Trim();

    /// <summary>Extracts and removes the <c>&lt;&lt;stereotypes&gt;&gt;</c> of a declaration.</summary>
    public static string TakeStereotypes(string text, List<string> into)
    {
        return StereotypeRegex().Replace(text, m =>
        {
            var body = m.Groups[1].Value.Trim();
            // << (S,#color) name >>: the spot letter and color are dropped.
            body = SpotRegex().Replace(body, "").Trim();
            foreach (var part in body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                into.Add(part);
            return " ";
        }).Trim();
    }

    [GeneratedRegex(@"^[\p{L}_][\p{L}\p{N}_.]*$")]
    private static partial Regex SimpleIdRegex();

    [GeneratedRegex(@"(?<=\s|^)(##?\[[^\]]*\][\w#]*|#[\w#;:.\-/\\|]+|\$\w+)")]
    private static partial Regex DecorationRegex();

    [GeneratedRegex(@"<<(.*?)>>")]
    private static partial Regex StereotypeRegex();

    [GeneratedRegex(@"^\([^)]*\)")]
    private static partial Regex SpotRegex();
}

/// <summary>Shared handling of the commands every diagram type accepts (title, direction, skinparam, …).</summary>
internal abstract partial class DiagramParserBase(List<UmlDiagnostic> diagnostics)
{
    protected void Info(PlantUmlLine line, string message) =>
        diagnostics.Add(new UmlDiagnostic(line.Number, UmlDiagnosticSeverity.Info, message));

    protected void Warn(PlantUmlLine line, string message) =>
        diagnostics.Add(new UmlDiagnostic(line.Number, UmlDiagnosticSeverity.Warning, message));

    protected void Error(int line, string message) =>
        diagnostics.Add(new UmlDiagnostic(line, UmlDiagnosticSeverity.Error, message));

    protected void Error(PlantUmlLine line, string message) => Error(line.Number, message);

    protected void Unrecognized(PlantUmlLine line) => Warn(line, $"Unrecognized line: {line.Text}");

    /// <summary>Handles a command common to all diagrams; false when the line is something else.</summary>
    protected bool TryCommon(PlantUmlLine line, PlantUmlCursor cursor, UmlDiagram diagram)
    {
        var text = line.Text;
        if (text.StartsWith('!'))
        {
            if (!text.StartsWith("!pragma", StringComparison.OrdinalIgnoreCase))
                Warn(line, "Preprocessor directives (!include, !define, !theme, …) are not supported and were ignored.");
            return true;
        }

        var title = TitleRegex().Match(text);
        if (title.Success)
        {
            diagram.Title = PlantUmlText.Unescape(title.Groups[1].Value);
            return true;
        }
        if (text.Equals("title", StringComparison.OrdinalIgnoreCase))
        {
            diagram.Title = string.Join("\n", cursor.ReadUntil(EndTitleRegex(), out _));
            return true;
        }

        if (LeftToRightRegex().IsMatch(text))
        {
            diagram.Direction = UmlDirection.LeftToRight;
            return true;
        }
        if (TopToBottomRegex().IsMatch(text))
        {
            diagram.Direction = UmlDirection.TopToBottom;
            return true;
        }

        // The themes do the styling, skinparams are skipped (with their { … } block).
        if (text.StartsWith("skinparam", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("<style>", StringComparison.OrdinalIgnoreCase))
        {
            if (text.EndsWith('{'))
                SkipBlock(cursor);
            else if (text.StartsWith("<style>", StringComparison.OrdinalIgnoreCase) && !text.Contains("</style>"))
                cursor.ReadUntil(EndStyleRegex(), out _);
            return true;
        }

        if (LegendRegex().IsMatch(text))
        {
            cursor.ReadUntil(EndLegendRegex(), out _);
            return true;
        }

        if (IgnoredRegex().IsMatch(text))
            return true;

        return false;
    }

    private static void SkipBlock(PlantUmlCursor cursor)
    {
        var depth = 1;
        while (depth > 0 && cursor.HasMore)
        {
            var text = cursor.Next().Text;
            if (text.EndsWith('{'))
                depth++;
            else if (text.StartsWith('}'))
                depth--;
        }
    }

    [GeneratedRegex(@"^title\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"^end\s*title$", RegexOptions.IgnoreCase)]
    private static partial Regex EndTitleRegex();

    [GeneratedRegex(@"^left\s+to\s+right\s+direction$", RegexOptions.IgnoreCase)]
    private static partial Regex LeftToRightRegex();

    [GeneratedRegex(@"^top\s+to\s+bottom\s+direction$", RegexOptions.IgnoreCase)]
    private static partial Regex TopToBottomRegex();

    [GeneratedRegex(@"^</style>$", RegexOptions.IgnoreCase)]
    private static partial Regex EndStyleRegex();

    [GeneratedRegex(@"^legend(\s+(left|right|top|bottom|center))*$", RegexOptions.IgnoreCase)]
    private static partial Regex LegendRegex();

    [GeneratedRegex(@"^end\s*legend$", RegexOptions.IgnoreCase)]
    private static partial Regex EndLegendRegex();

    [GeneratedRegex(@"^((left|right|center)\s+)?(header|footer)\b|^caption\b|^scale\b|^hide\b|^show\b|^remove\b|^restore\b|^allowmixing$|^allow_mixing$|^set\s|^newpage\b|^mainframe\b|^skin\b|^page\s|^footbox\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex IgnoredRegex();
}
