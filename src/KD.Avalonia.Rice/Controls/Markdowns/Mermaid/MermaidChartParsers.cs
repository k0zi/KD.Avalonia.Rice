using System.Globalization;
using System.Text.RegularExpressions;

namespace KD.Avalonia.Rice.Controls.Markdowns;

/// <summary>Mermaid entity relationship diagrams.</summary>
internal static partial class ErParser
{
    [GeneratedRegex(@"^([\w-]+)(?:\[""?([^\]""]*)""?\])?\s*(\|o|\|\||\}o|\}\|)(--|\.\.)(o\||\|\||o\{|\|\{)\s*([\w-]+)(?:\[""?([^\]""]*)""?\])?\s*(?::\s*(.*))?$")]
    private static partial Regex RelationshipRegex();

    [GeneratedRegex(@"^([\w-]+)(?:\[""?([^\]""]*)""?\])?\s*\{\s*(\})?$")]
    private static partial Regex EntityRegex();

    [GeneratedRegex(@"^([\w\[\]()<>,.*-]+)\s+([\w*-]+)((?:\s+(?:PK|FK|UK)\s*,?)*)\s*(?:""([^""]*)"")?$")]
    private static partial Regex AttributeRegex();

    public static MermaidErDiagram Parse(List<MermaidLine> lines, List<MermaidDiagnostic> diagnostics)
    {
        var diagram = new MermaidErDiagram();
        MermaidEntity? body = null;
        foreach (var line in lines)
        {
            var text = line.Text;
            if (body is not null)
            {
                if (text == "}")
                {
                    body = null;
                    continue;
                }
                if (AttributeRegex().Match(text) is { Success: true } a)
                {
                    var attribute = new MermaidAttribute(a.Groups[1].Value, a.Groups[2].Value)
                    {
                        Comment = a.Groups[4].Success ? a.Groups[4].Value : null,
                    };
                    attribute.Keys.AddRange(Regex.Matches(a.Groups[3].Value, "PK|FK|UK").Select(k => k.Value));
                    body.Attributes.Add(attribute);
                }
                else
                    diagnostics.Add(new MermaidDiagnostic(line.Number, $"Unrecognized attribute: {text}"));
                continue;
            }
            if (text.StartsWith("direction ", StringComparison.Ordinal))
            {
                diagram.Direction = MermaidParser.Direction(text[10..]) ?? diagram.Direction;
                continue;
            }
            if (EntityRegex().Match(text) is { Success: true } e)
            {
                var entity = diagram.GetOrAdd(e.Groups[1].Value);
                if (e.Groups[2].Success && e.Groups[2].Length > 0)
                    entity.Label = e.Groups[2].Value;
                if (!e.Groups[3].Success)
                    body = entity;
                continue;
            }
            if (RelationshipRegex().Match(text) is { Success: true } r)
            {
                var from = diagram.GetOrAdd(r.Groups[1].Value);
                var to = diagram.GetOrAdd(r.Groups[6].Value);
                if (r.Groups[2].Success && r.Groups[2].Length > 0)
                    from.Label = r.Groups[2].Value;
                if (r.Groups[7].Success && r.Groups[7].Length > 0)
                    to.Label = r.Groups[7].Value;
                diagram.Relationships.Add(new MermaidRelationship(from.Id, to.Id)
                {
                    FromCardinality = Cardinality(r.Groups[3].Value),
                    ToCardinality = Cardinality(r.Groups[5].Value),
                    IsIdentifying = r.Groups[4].Value == "--",
                    Label = r.Groups[8].Success && r.Groups[8].Length > 0 ? MermaidParser.Label(r.Groups[8].Value) : null,
                });
                continue;
            }
            if (Regex.IsMatch(text, @"^[\w-]+$"))
            {
                diagram.GetOrAdd(text);
                continue;
            }
            if (Regex.IsMatch(text, @"^(classDef|class|style)\b"))
                continue;
            diagnostics.Add(new MermaidDiagnostic(line.Number, $"Unrecognized line: {text}"));
        }
        return diagram;
    }

    private static MermaidCardinality Cardinality(string mark) => mark switch
    {
        "|o" or "o|" => MermaidCardinality.ZeroOrOne,
        "}o" or "o{" => MermaidCardinality.ZeroOrMore,
        "}|" or "|{" => MermaidCardinality.OneOrMore,
        _ => MermaidCardinality.ExactlyOne,
    };
}

/// <summary>Mermaid pie charts, gantt charts and mind maps.</summary>
internal static partial class MermaidChartParsers
{
    [GeneratedRegex(@"^""([^""]*)""\s*:\s*([-+]?\d+(?:\.\d+)?)$")]
    private static partial Regex SliceRegex();

    [GeneratedRegex(@"^(\d+(?:\.\d+)?)\s*(ms|s|m|h|d|w|M|y)$")]
    private static partial Regex DurationRegex();

    public static MermaidPie ParsePie(string header, List<MermaidLine> lines, List<MermaidDiagnostic> diagnostics)
    {
        var pie = new MermaidPie();
        foreach (var word in new[] { header }.Concat(lines.Select(l => l.Text)).Where(t => t.Length > 0).ToList())
        {
            var text = word;
            if (text.StartsWith("showData", StringComparison.Ordinal))
            {
                pie.ShowData = true;
                text = text[8..].Trim();
                if (text.Length == 0)
                    continue;
            }
            if (text.StartsWith("title", StringComparison.Ordinal))
            {
                pie.Title = text[5..].Trim();
                continue;
            }
            if (SliceRegex().Match(text) is { Success: true } m)
            {
                var value = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                if (value > 0)
                    pie.Slices.Add(new MermaidPieSlice(m.Groups[1].Value, value));
                continue;
            }
            var number = lines.FirstOrDefault(l => l.Text == word).Number;
            diagnostics.Add(new MermaidDiagnostic(number == 0 ? 1 : number, $"Unrecognized line: {text}"));
        }
        return pie;
    }

    // ---- gantt ----

    public static MermaidGantt ParseGantt(List<MermaidLine> lines, List<MermaidDiagnostic> diagnostics)
    {
        var gantt = new MermaidGantt();
        var dateFormat = "YYYY-MM-DD";
        MermaidGanttSection? section = null;
        var tasks = new Dictionary<string, MermaidGanttTask>();
        MermaidGanttTask? previous = null;

        foreach (var line in lines)
        {
            var text = line.Text;
            var keyword = text.Split(' ', 2)[0];
            var value = text.Length > keyword.Length ? text[keyword.Length..].Trim() : "";
            switch (keyword)
            {
                case "title":
                    gantt.Title = value;
                    continue;
                case "dateFormat":
                    dateFormat = value;
                    continue;
                case "axisFormat":
                    gantt.AxisFormat = AxisFormat(value);
                    continue;
                case "section":
                    section = new MermaidGanttSection(value);
                    gantt.Sections.Add(section);
                    continue;
                case "excludes" or "includes" or "todayMarker" or "tickInterval" or "weekday" or "inclusiveEndDates" or "topAxis" or "displayMode":
                    continue;
            }
            if (Regex.IsMatch(text, @"^(classDef|class|style|click)\b"))
                continue;

            var colon = text.IndexOf(':');
            if (colon <= 0)
            {
                diagnostics.Add(new MermaidDiagnostic(line.Number, $"Unrecognized line: {text}"));
                continue;
            }
            var task = new MermaidGanttTask(text[..colon].Trim());
            var parts = text[(colon + 1)..].Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            while (parts.Count > 0 && parts[0] is "done" or "active" or "crit" or "milestone")
            {
                switch (parts[0])
                {
                    case "done":
                        task.IsDone = true;
                        break;
                    case "active":
                        task.IsActive = true;
                        break;
                    case "crit":
                        task.IsCritical = true;
                        break;
                    default:
                        task.IsMilestone = true;
                        break;
                }
                parts.RemoveAt(0);
            }
            if (parts.Count >= 3)
            {
                task.Id = parts[0];
                parts.RemoveAt(0);
            }

            string? startText = parts.Count >= 2 ? parts[0] : null;
            var endText = parts.Count >= 1 ? parts[^1] : "1d";
            DateTime start;
            if (startText is null)
                start = previous?.End ?? DateTime.Today;
            else if (startText.StartsWith("after ", StringComparison.Ordinal))
            {
                var ends = startText[6..].Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => tasks.TryGetValue(id, out var t) ? t.End : (DateTime?)null).Where(d => d is not null).ToList();
                if (ends.Count == 0)
                    diagnostics.Add(new MermaidDiagnostic(line.Number, $"Unknown task in '{startText}'."));
                start = ends.Count > 0 ? ends.Max()!.Value : previous?.End ?? DateTime.Today;
            }
            else if (ParseDate(startText, dateFormat) is { } date)
                start = date;
            else
            {
                diagnostics.Add(new MermaidDiagnostic(line.Number, $"Unreadable date '{startText}'."));
                start = previous?.End ?? DateTime.Today;
            }

            var end = start.AddDays(1);
            if (DurationRegex().Match(endText) is { Success: true } duration)
            {
                var amount = double.Parse(duration.Groups[1].Value, CultureInfo.InvariantCulture);
                end = duration.Groups[2].Value switch
                {
                    "ms" => start.AddMilliseconds(amount),
                    "s" => start.AddSeconds(amount),
                    "m" => start.AddMinutes(amount),
                    "h" => start.AddHours(amount),
                    "w" => start.AddDays(amount * 7),
                    "M" => start.AddMonths((int)amount),
                    "y" => start.AddYears((int)amount),
                    _ => start.AddDays(amount),
                };
            }
            else if (endText.StartsWith("until ", StringComparison.Ordinal) && tasks.TryGetValue(endText[6..].Trim(), out var until))
                end = until.Start;
            else if (ParseDate(endText, dateFormat) is { } endDate)
                end = endDate;
            else if (startText is not null || parts.Count > 0)
                diagnostics.Add(new MermaidDiagnostic(line.Number, $"Unreadable end '{endText}'."));

            task.Start = start;
            task.End = end < start ? start : end;
            if (section is null)
            {
                section = new MermaidGanttSection("");
                gantt.Sections.Add(section);
            }
            section.Tasks.Add(task);
            if (task.Id is not null)
                tasks[task.Id] = task;
            previous = task;
        }
        return gantt;
    }

    private static DateTime? ParseDate(string text, string format)
    {
        if (format is "X" or "x" && long.TryParse(text, out var epoch))
            return (format == "X" ? DateTimeOffset.FromUnixTimeSeconds(epoch) : DateTimeOffset.FromUnixTimeMilliseconds(epoch)).LocalDateTime;
        // Day.js tokens to .NET ones.
        var net = Regex.Replace(format, "YYYY|YY|MMMM|MMM|MM|M|DD|D|HH|H|hh|h|mm|m|ss|s|A|a", m => m.Value switch
        {
            "YYYY" => "yyyy",
            "YY" => "yy",
            "DD" => "dd",
            "D" => "d",
            "A" or "a" => "tt",
            _ => m.Value,
        });
        if (DateTime.TryParseExact(text, net, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var exact))
            return exact;
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var any) ? any : null;
    }

    /// <summary>The d3 time format of <c>axisFormat</c> (<c>%Y-%m-%d</c>) to a .NET one.</summary>
    private static string AxisFormat(string format) => Regex.Replace(format, "%[a-zA-Z%]", m => m.Value switch
    {
        "%Y" => "yyyy",
        "%y" => "yy",
        "%m" => "MM",
        "%d" => "dd",
        "%e" => "d",
        "%b" => "MMM",
        "%B" => "MMMM",
        "%a" => "ddd",
        "%A" => "dddd",
        "%H" => "HH",
        "%I" => "hh",
        "%M" => "mm",
        "%S" => "ss",
        "%p" => "tt",
        "%%" => "%",
        _ => "",
    });

    // ---- mind map ----

    private static readonly (string Open, string Close, MermaidMindmapShape Shape)[] MindmapShapes =
    [
        ("((", "))", MermaidMindmapShape.Circle),
        ("))", "((", MermaidMindmapShape.Bang),
        ("{{", "}}", MermaidMindmapShape.Hexagon),
        (")", "(", MermaidMindmapShape.Cloud),
        ("(", ")", MermaidMindmapShape.Rounded),
        ("[", "]", MermaidMindmapShape.Square),
    ];

    public static MermaidMindmap ParseMindmap(List<MermaidLine> lines, List<MermaidDiagnostic> diagnostics)
    {
        var map = new MermaidMindmap();
        var stack = new Stack<(int Indent, MermaidMindmapNode Node)>();
        foreach (var line in lines)
        {
            var text = line.Text;
            if (text.StartsWith("::", StringComparison.Ordinal) || text.StartsWith(":::", StringComparison.Ordinal))
                continue;
            var node = Node(text);
            while (stack.Count > 0 && stack.Peek().Indent >= line.Indent)
                stack.Pop();
            if (stack.Count == 0)
            {
                if (map.Root is null)
                    map.Root = node;
                else
                {
                    diagnostics.Add(new MermaidDiagnostic(line.Number, "A mind map has one root; indent this node."));
                    map.Root.Children.Add(node);
                }
            }
            else
                stack.Peek().Node.Children.Add(node);
            stack.Push((line.Indent, node));
        }
        return map;
    }

    private static MermaidMindmapNode Node(string text)
    {
        text = Regex.Replace(text, @":::\S+", "").Trim();
        foreach (var (open, close, shape) in MindmapShapes)
        {
            var start = text.IndexOf(open, StringComparison.Ordinal);
            if (start < 0 || !text.EndsWith(close, StringComparison.Ordinal) || start + open.Length > text.Length - close.Length)
                continue;
            // Only an id may come before the shape.
            if (start > 0 && !Regex.IsMatch(text[..start], @"^[\w-]+$"))
                continue;
            return new MermaidMindmapNode(MermaidParser.Label(text[(start + open.Length)..^close.Length]), shape);
        }
        return new MermaidMindmapNode(MermaidParser.Label(text));
    }
}
