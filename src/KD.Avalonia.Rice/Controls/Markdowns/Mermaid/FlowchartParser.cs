using System.Text.RegularExpressions;

namespace KD.Avalonia.Rice.Controls.Markdowns;

/// <summary>
/// Mermaid flowcharts: node chains (<c>A --&gt; B --&gt; C</c>, <c>A &amp; B --&gt; C</c>), the node shapes, the edge
/// kinds with labels (<c>-- text --&gt;</c>, <c>--&gt;|text|</c>), subgraphs with their own direction.
/// </summary>
internal static partial class FlowchartParser
{
    private static readonly (string Open, string Close, MermaidNodeShape Shape)[] Shapes =
    [
        ("(((", ")))", MermaidNodeShape.DoubleCircle),
        ("((", "))", MermaidNodeShape.Circle),
        ("([", "])", MermaidNodeShape.Stadium),
        ("[[", "]]", MermaidNodeShape.Subroutine),
        ("[(", ")]", MermaidNodeShape.Cylinder),
        ("{{", "}}", MermaidNodeShape.Hexagon),
        ("[/", "/]", MermaidNodeShape.Parallelogram),
        ("[\\", "\\]", MermaidNodeShape.ParallelogramAlt),
        ("[/", "\\]", MermaidNodeShape.Trapezoid),
        ("[\\", "/]", MermaidNodeShape.TrapezoidAlt),
        (">", "]", MermaidNodeShape.Asymmetric),
        ("{", "}", MermaidNodeShape.Diamond),
        ("(", ")", MermaidNodeShape.Rounded),
        ("[", "]", MermaidNodeShape.Rectangle),
    ];

    [GeneratedRegex(@"\G[\wÀ-￿](?:[\w.À-￿]|-(?=\w))*")]
    private static partial Regex IdRegex();

    [GeneratedRegex(@"\G(?<l>[<ox])?(?<open>--|==|-\.)\s+(?<text>[^|]*?)\s*(?<line>-{2,}|={2,}|\.+-)(?<r>[>ox])?(?=[\s\w""(\[{]|$)")]
    private static partial Regex LabeledEdgeRegex();

    [GeneratedRegex(@"\G(?<l>[<ox])?(?<line>-{2,}|={2,}|-\.+-|~{3,})(?<r>[>ox])?")]
    private static partial Regex EdgeRegex();

    [GeneratedRegex(@"^subgraph\s+(?:(?<id>[^\s\[""]+)\s*\[(?<title>[^\]]*)\]|""(?<title>[^""]*)""|(?<id>\S+)\s*$|(?<title>.+))$")]
    private static partial Regex SubgraphRegex();

    private sealed class State(MermaidFlowchart chart, List<MermaidDiagnostic> diagnostics)
    {
        public MermaidFlowchart Chart { get; } = chart;
        public List<MermaidDiagnostic> Diagnostics { get; } = diagnostics;
        public Dictionary<string, MermaidNode> Nodes { get; } = [];
        public Dictionary<string, MermaidSubgraph?> Membership { get; } = [];
        public Stack<MermaidSubgraph> Scopes { get; } = new();
        public List<MermaidSubgraph> AllSubgraphs { get; } = [];
    }

    public static MermaidFlowchart Parse(string header, List<MermaidLine> lines, List<MermaidDiagnostic> diagnostics)
    {
        var chart = new MermaidFlowchart { Direction = MermaidParser.Direction(header) ?? MermaidDirection.TopToBottom };
        var state = new State(chart, diagnostics);
        var anonymous = 0;
        foreach (var line in lines)
        {
            var text = line.Text;
            if (MermaidParser.IsStyling(text))
                continue;
            if (text.StartsWith("direction ", StringComparison.Ordinal))
            {
                var direction = MermaidParser.Direction(text[10..]);
                if (state.Scopes.TryPeek(out var scope))
                    scope.Direction = direction;
                else if (direction is { } d)
                    chart.Direction = d;
                continue;
            }
            if (text == "end")
            {
                if (!state.Scopes.TryPop(out _))
                    diagnostics.Add(new MermaidDiagnostic(line.Number, "'end' without a subgraph."));
                continue;
            }
            if (text.StartsWith("subgraph", StringComparison.Ordinal) && (text.Length == 8 || char.IsWhiteSpace(text[8])))
            {
                var m = SubgraphRegex().Match(text);
                var title = m.Groups["title"].Success ? MermaidParser.Label(m.Groups["title"].Value) : null;
                var id = m.Groups["id"].Success ? m.Groups["id"].Value : title ?? $"subgraph{++anonymous}";
                var subgraph = new MermaidSubgraph(id, title);
                (state.Scopes.TryPeek(out var parent) ? parent.Subgraphs : chart.Subgraphs).Add(subgraph);
                state.AllSubgraphs.Add(subgraph);
                state.Scopes.Push(subgraph);
                continue;
            }
            ParseStatement(state, line);
        }
        if (state.Scopes.Count > 0)
            diagnostics.Add(new MermaidDiagnostic(lines.LastOrDefault().Number, "Missing 'end' of a subgraph."));

        // Ids of subgraphs used in edges are the subgraphs, not nodes.
        var subgraphIds = state.AllSubgraphs.Select(s => s.Id).ToHashSet();
        chart.Nodes.RemoveAll(n => subgraphIds.Contains(n.Id) && n.Text == n.Id);
        foreach (var (id, subgraph) in state.Membership)
            if (subgraph is not null && chart.Find(id) is not null)
                subgraph.Nodes.Add(id);
        return chart;
    }

    private static void ParseStatement(State state, MermaidLine line)
    {
        var text = line.Text;
        var pos = 0;
        var previous = ParseGroup(state, text, ref pos);
        if (previous.Count == 0)
        {
            state.Diagnostics.Add(new MermaidDiagnostic(line.Number, $"Unrecognized line: {text}"));
            return;
        }
        while (true)
        {
            SkipSpaces(text, ref pos);
            if (pos >= text.Length)
                return;
            var edge = ParseEdge(text, ref pos);
            if (edge is null)
            {
                state.Diagnostics.Add(new MermaidDiagnostic(line.Number, $"Unrecognized text: {text[pos..]}"));
                return;
            }
            SkipSpaces(text, ref pos);
            var next = ParseGroup(state, text, ref pos);
            if (next.Count == 0)
            {
                state.Diagnostics.Add(new MermaidDiagnostic(line.Number, "An edge without a target."));
                return;
            }
            foreach (var from in previous)
                foreach (var to in next)
                    state.Chart.Edges.Add(new MermaidEdge(from, to)
                    {
                        Text = edge.Value.Text,
                        Line = edge.Value.Line,
                        FromEnd = edge.Value.FromEnd,
                        ToEnd = edge.Value.ToEnd,
                        Length = edge.Value.Length,
                    });
            previous = next;
        }
    }

    private static void SkipSpaces(string text, ref int pos)
    {
        while (pos < text.Length && char.IsWhiteSpace(text[pos]))
            pos++;
    }

    /// <summary><c>A</c>, <c>A[text]</c>, <c>A &amp; B</c>; returns the ids.</summary>
    private static List<string> ParseGroup(State state, string text, ref int pos)
    {
        var ids = new List<string>();
        while (true)
        {
            SkipSpaces(text, ref pos);
            var id = ParseNode(state, text, ref pos);
            if (id is null)
                break;
            ids.Add(id);
            var save = pos;
            SkipSpaces(text, ref pos);
            if (pos < text.Length && text[pos] == '&')
            {
                pos++;
                continue;
            }
            pos = save;
            break;
        }
        return ids;
    }

    private static string? ParseNode(State state, string text, ref int pos)
    {
        var m = IdRegex().Match(text, pos);
        if (!m.Success)
            return null;
        var id = m.Value;
        pos += m.Length;

        string? label = null;
        MermaidNodeShape? shape = null;
        foreach (var (open, close, kind) in Shapes)
        {
            if (string.CompareOrdinal(text, pos, open, 0, open.Length) != 0)
                continue;
            var start = pos + open.Length;
            int end;
            if (start < text.Length && text[start] == '"')
            {
                var quote = text.IndexOf('"', start + 1);
                end = quote < 0 ? -1 : text.IndexOf(close, quote + 1, StringComparison.Ordinal);
            }
            else
                end = text.IndexOf(close, start, StringComparison.Ordinal);
            if (end < 0)
                continue;
            // [/…\] and [\…/] would also match the parallelograms; the first closer decides.
            if (kind is MermaidNodeShape.Parallelogram or MermaidNodeShape.ParallelogramAlt)
            {
                var other = text.IndexOf(kind == MermaidNodeShape.Parallelogram ? "\\]" : "/]", start, StringComparison.Ordinal);
                if (other >= 0 && other < end)
                    continue;
            }
            label = MermaidParser.Label(text[start..end]);
            shape = kind;
            pos = end + close.Length;
            break;
        }
        // Style class: A:::warning
        if (string.CompareOrdinal(text, pos, ":::", 0, 3) == 0)
        {
            pos += 3;
            while (pos < text.Length && (char.IsLetterOrDigit(text[pos]) || text[pos] is '_' or '-'))
                pos++;
        }

        if (!state.Nodes.TryGetValue(id, out var node))
        {
            node = new MermaidNode(id);
            state.Nodes[id] = node;
            state.Chart.Nodes.Add(node);
        }
        if (shape is { } s)
        {
            node.Shape = s;
            node.Text = label ?? id;
        }
        if (state.Scopes.TryPeek(out var scope))
            state.Membership[id] = scope;
        else
            state.Membership.TryAdd(id, null);
        return id;
    }

    private readonly record struct EdgeSpec(string? Text, MermaidLineStyle Line, MermaidEdgeEnd FromEnd, MermaidEdgeEnd ToEnd, int Length);

    private static EdgeSpec? ParseEdge(string text, ref int pos)
    {
        string? label = null;
        string line;
        string? left, right;
        if (LabeledEdgeRegex().Match(text, pos) is { Success: true } labeled && labeled.Groups["text"].Length > 0)
        {
            label = MermaidParser.Label(labeled.Groups["text"].Value);
            line = labeled.Groups["line"].Value;
            left = labeled.Groups["l"].Value;
            right = labeled.Groups["r"].Value;
            pos += labeled.Length;
        }
        else if (EdgeRegex().Match(text, pos) is { Success: true } plain)
        {
            line = plain.Groups["line"].Value;
            left = plain.Groups["l"].Value;
            right = plain.Groups["r"].Value;
            pos += plain.Length;
        }
        else
            return null;

        // Label after the edge: -->|text|
        var save = pos;
        SkipSpaces(text, ref pos);
        if (pos < text.Length && text[pos] == '|')
        {
            var end = text.IndexOf('|', pos + 1);
            if (end > pos)
            {
                label = MermaidParser.Label(text[(pos + 1)..end]);
                pos = end + 1;
            }
            else
                pos = save;
        }
        else
            pos = save;

        var style = line.Contains('~') ? MermaidLineStyle.Invisible
            : line.Contains('=') ? MermaidLineStyle.Thick
            : line.Contains('.') ? MermaidLineStyle.Dotted
            : MermaidLineStyle.Solid;
        var length = style switch
        {
            MermaidLineStyle.Dotted => Math.Max(1, line.Count(c => c == '.')),
            MermaidLineStyle.Thick => Math.Max(1, line.Count(c => c == '=') - (right.Length > 0 ? 1 : 2) + 1),
            _ => Math.Max(1, line.Count(c => c == '-') - (right.Length > 0 ? 1 : 2)),
        };
        return new EdgeSpec(label, style, End(left, true), End(right, false), Math.Min(length, 6));
    }

    private static MermaidEdgeEnd End(string? mark, bool left) => left && mark == ">" ? MermaidEdgeEnd.None : mark switch
    {
        "<" or ">" => MermaidEdgeEnd.Arrow,
        "o" => MermaidEdgeEnd.Circle,
        "x" => MermaidEdgeEnd.Cross,
        _ => MermaidEdgeEnd.None,
    };
}
