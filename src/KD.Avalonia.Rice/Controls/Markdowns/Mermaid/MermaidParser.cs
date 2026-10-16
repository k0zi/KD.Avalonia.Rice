using System.Text.RegularExpressions;

namespace KD.Avalonia.Rice.Controls.Markdowns;

/// <summary>A line of Mermaid text without its comment; <see cref="Number"/> is 1-based.</summary>
internal readonly record struct MermaidLine(int Number, string Text, int Indent);

/// <summary>Reads Mermaid text: front matter, comments, the diagram type keyword, then the parser of that type.</summary>
internal static partial class MermaidParser
{
    [GeneratedRegex(@"^\s*title\s*:\s*(.*)$")]
    private static partial Regex FrontMatterTitleRegex();

    public static MermaidDocument Parse(string text)
    {
        var diagnostics = new List<MermaidDiagnostic>();
        var lines = new List<MermaidLine>();
        string? title = null;
        var raw = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var i = 0;

        // Front matter: --- title: … ---
        while (i < raw.Length && raw[i].Trim().Length == 0)
            i++;
        if (i < raw.Length && raw[i].Trim() == "---")
        {
            for (i++; i < raw.Length && raw[i].Trim() != "---"; i++)
                if (FrontMatterTitleRegex().Match(raw[i]) is { Success: true } m)
                    title = Unquote(m.Groups[1].Value.Trim());
            i++;
        }

        for (; i < raw.Length; i++)
        {
            var line = raw[i];
            var comment = line.IndexOf("%%", StringComparison.Ordinal);
            if (comment >= 0)
                line = line[..comment];
            if (line.Trim().Length == 0)
                continue;
            var indent = 0;
            while (indent < line.Length && char.IsWhiteSpace(line[indent]))
                indent += line[indent] == '\t' ? 4 : 1;
            lines.Add(new MermaidLine(i + 1, line.Trim().TrimEnd(';').Trim(), indent));
        }

        if (lines.Count == 0)
        {
            diagnostics.Add(new MermaidDiagnostic(1, "Empty diagram."));
            return new MermaidDocument(MermaidDiagramKind.Unknown, null, diagnostics);
        }

        var header = lines[0];
        var keyword = header.Text.Split([' ', '\t'], 2)[0];
        var body = lines.Skip(1).ToList();
        var rest = header.Text[keyword.Length..].Trim();
        try
        {
            var (kind, diagram) = keyword switch
            {
                "graph" or "flowchart" or "flowchart-elk" => (MermaidDiagramKind.Flowchart, (object)FlowchartParser.Parse(rest, body, diagnostics)),
                "sequenceDiagram" => (MermaidDiagramKind.Sequence, MermaidSequenceParser.Parse(body, diagnostics)),
                "classDiagram" or "classDiagram-v2" => (MermaidDiagramKind.Class, MermaidClassParser.Parse(body, diagnostics)),
                "stateDiagram" or "stateDiagram-v2" => (MermaidDiagramKind.State, MermaidStateParser.Parse(body, diagnostics)),
                "erDiagram" => (MermaidDiagramKind.EntityRelationship, ErParser.Parse(body, diagnostics)),
                "pie" => (MermaidDiagramKind.Pie, MermaidChartParsers.ParsePie(rest, body, diagnostics)),
                "gantt" => (MermaidDiagramKind.Gantt, MermaidChartParsers.ParseGantt(body, diagnostics)),
                "mindmap" => (MermaidDiagramKind.Mindmap, MermaidChartParsers.ParseMindmap(body, diagnostics)),
                _ => (MermaidDiagramKind.Unknown, null),
            };
            if (kind == MermaidDiagramKind.Unknown)
                diagnostics.Add(new MermaidDiagnostic(header.Number, $"Unsupported diagram type '{keyword}'."));
            if (title is not null)
            {
                switch (diagram)
                {
                    case MermaidDiagram m when m.Title is null:
                        m.Title = title;
                        break;
                    case Uml.UmlDiagram u when u.Title is null:
                        u.Title = title;
                        break;
                }
            }
            return new MermaidDocument(kind, diagram, diagnostics);
        }
        catch (Exception e)
        {
            // A parser bug must not break the view; the diagram is just not shown.
            diagnostics.Add(new MermaidDiagnostic(header.Number, e.Message));
            return new MermaidDocument(MermaidDiagramKind.Unknown, null, diagnostics);
        }
    }

    public static string Unquote(string text) =>
        text.Length >= 2 && (text[0] == '"' && text[^1] == '"' || text[0] == '\'' && text[^1] == '\'') ? text[1..^1] : text;

    /// <summary>Label text: quotes removed, <c>&lt;br&gt;</c> and <c>\n</c> as new lines, markdown strings (<c>"`…`"</c>) without the backticks.</summary>
    public static string Label(string text)
    {
        text = Unquote(text.Trim());
        if (text.Length >= 2 && text[0] == '`' && text[^1] == '`')
            text = text[1..^1].Replace("**", "").Replace("*", "");
        text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase).Replace("\\n", "\n");
        text = Regex.Replace(text, "#([a-z]+|#?[0-9]+);", m => System.Net.WebUtility.HtmlDecode("&" + m.Groups[1].Value + ";"));
        return text;
    }

    public static MermaidDirection? Direction(string text) => text.Trim().ToUpperInvariant() switch
    {
        "TB" or "TD" => MermaidDirection.TopToBottom,
        "BT" => MermaidDirection.BottomToTop,
        "LR" => MermaidDirection.LeftToRight,
        "RL" => MermaidDirection.RightToLeft,
        _ => null,
    };

    /// <summary>Styling statements: accepted and ignored, the theme does the colors.</summary>
    public static bool IsStyling(string line) =>
        Regex.IsMatch(line, @"^(classDef|class|style|linkStyle|click|cssClass|callback|link|accTitle|accDescr|%%)\b");
}
