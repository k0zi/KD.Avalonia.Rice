using System.Text.RegularExpressions;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>Reads the "new" PlantUML activity syntax (<c>start</c>, <c>:action;</c>, <c>if</c>, <c>while</c>, <c>fork</c>, <c>|lane|</c>, …).</summary>
internal sealed partial class ActivityDiagramParser(List<UmlDiagnostic> diagnostics, UmlDiagramKind kind)
    : DiagramParserBase(diagnostics)
{
    private readonly UmlActivityDiagram _diagram = new(kind);

    public UmlActivityDiagram Parse(PlantUmlBlock block)
    {
        var cursor = new PlantUmlCursor(block.Lines);
        var stop = ParseSequence(_diagram.Statements, cursor, null);
        if (stop is { } line)
            Warn(line, $"'{line.Text}' without a matching opening statement.");
        return _diagram;
    }

    /// <summary>
    /// Reads statements into <paramref name="target"/> until a line matching <paramref name="terminator"/>, which is
    /// returned (not consumed into the list); null at the end of the block.
    /// </summary>
    private PlantUmlLine? ParseSequence(List<UmlActivityStatement> target, PlantUmlCursor cursor, Regex? terminator)
    {
        while (cursor.HasMore)
        {
            var line = cursor.Next();
            var text = line.Text;
            if (terminator?.IsMatch(text) == true)
                return line;
            if (TryCommon(line, cursor, _diagram))
                continue;

            switch (text.ToLowerInvariant())
            {
                case "start":
                    target.Add(new UmlActivityStart());
                    continue;
                case "stop":
                    target.Add(new UmlActivityStop());
                    continue;
                case "end":
                    target.Add(new UmlActivityEnd());
                    continue;
                case "kill" or "detach":
                    target.Add(new UmlActivityDetach());
                    continue;
                case "}":
                    continue;
            }

            if (text.StartsWith(':'))
            {
                target.Add(ReadAction(line, cursor));
                continue;
            }

            var lane = LaneRegex().Match(text);
            if (lane.Success)
            {
                target.Add(new UmlActivityLane(PlantUmlText.Unescape(lane.Groups["name"].Value)));
                continue;
            }

            var arrow = ArrowRegex().Match(text);
            if (arrow.Success)
            {
                var label = arrow.Groups["label"].Value.Trim();
                target.Add(new UmlActivityArrow(label.Length > 0 ? PlantUmlText.Unescape(label) : null));
                continue;
            }

            var ifMatch = IfRegex().Match(text);
            if (ifMatch.Success)
            {
                target.Add(ReadIf(ifMatch, line, cursor));
                continue;
            }

            var whileMatch = WhileRegex().Match(text);
            if (whileMatch.Success)
            {
                var loop = new UmlActivityWhile(PlantUmlText.Unescape(whileMatch.Groups["cond"].Value))
                {
                    Label = Optional(whileMatch.Groups["label"]),
                };
                var end = ParseSequence(loop.Body, cursor, EndWhileRegex());
                if (end is null)
                    Error(line, "'endwhile' is missing.");
                else
                    loop.EndLabel = Optional(EndWhileRegex().Match(end.Value.Text).Groups["label"]);
                target.Add(loop);
                continue;
            }

            if (text.Equals("repeat", StringComparison.OrdinalIgnoreCase))
            {
                var loop = new UmlActivityRepeat("");
                var end = ParseSequence(loop.Body, cursor, RepeatWhileRegex());
                if (end is null)
                    Error(line, "'repeat while' is missing.");
                else
                {
                    var m = RepeatWhileRegex().Match(end.Value.Text);
                    loop.Condition = PlantUmlText.Unescape(m.Groups["cond"].Value);
                    loop.Label = Optional(m.Groups["label"]);
                    loop.EndLabel = Optional(m.Groups["not"]);
                }
                target.Add(loop);
                continue;
            }

            var fork = ForkRegex().Match(text);
            if (fork.Success)
            {
                target.Add(ReadFork(fork.Groups["kw"].Value.Equals("split", StringComparison.OrdinalIgnoreCase), line, cursor));
                continue;
            }

            if (TryNote(line, cursor, target))
                continue;

            var partition = PartitionRegex().Match(text);
            if (partition.Success)
            {
                Info(line, "Partitions are drawn without their frame.");
                continue;
            }

            Unrecognized(line);
        }
        return null;
    }

    private UmlActivityStatement ReadAction(PlantUmlLine line, PlantUmlCursor cursor)
    {
        // The action ends with ';' (or one of the SDL shape endings), possibly lines later.
        var text = line.Text[1..];
        while (!EndsAction(text) && cursor.HasMore)
            text += "\n" + cursor.Next().Text;
        if (EndsAction(text))
            text = text[..^1];
        else
            Error(line, "The action is not closed with ';'.");

        text = PlantUmlText.Unescape(text.Trim());
        var reference = RefActionRegex().Match(text);
        if (reference.Success && (kind == UmlDiagramKind.InteractionOverview || text.StartsWith("ref ", StringComparison.Ordinal)))
            return new UmlActivityReference(reference.Groups[1].Value.Trim());
        return new UmlActivityAction(text);
    }

    private static bool EndsAction(string text)
    {
        var trimmed = text.TrimEnd();
        return trimmed.Length > 0 && ";|<>/]}".Contains(trimmed[^1]);
    }

    private UmlActivityIf ReadIf(Match match, PlantUmlLine line, PlantUmlCursor cursor)
    {
        var statement = new UmlActivityIf();
        var branch = new UmlActivityBranch(PlantUmlText.Unescape(match.Groups["cond"].Value), Optional(match.Groups["label"]));
        while (true)
        {
            statement.Branches.Add(branch);
            var end = ParseSequence(branch.Statements, cursor, IfContinuationRegex());
            if (end is null)
            {
                Error(line, "'endif' is missing.");
                return statement;
            }

            var text = end.Value.Text;
            var elseIf = ElseIfRegex().Match(text);
            if (elseIf.Success)
            {
                branch = new UmlActivityBranch(PlantUmlText.Unescape(elseIf.Groups["cond"].Value), Optional(elseIf.Groups["label"]));
                continue;
            }
            var elseMatch = ElseRegex().Match(text);
            if (elseMatch.Success)
            {
                branch = new UmlActivityBranch(null, Optional(elseMatch.Groups["label"]));
                continue;
            }
            return statement;
        }
    }

    private UmlActivityFork ReadFork(bool isSplit, PlantUmlLine line, PlantUmlCursor cursor)
    {
        var fork = new UmlActivityFork(isSplit);
        var again = isSplit ? SplitAgainRegex() : ForkAgainRegex();
        var endRegex = isSplit ? EndSplitRegex() : EndForkRegex();
        while (true)
        {
            var branch = new List<UmlActivityStatement>();
            fork.Branches.Add(branch);
            var end = ParseSequence(branch, cursor, AnyOf(again, endRegex));
            if (end is null)
            {
                Error(line, isSplit ? "'end split' is missing." : "'end fork' is missing.");
                return fork;
            }
            if (again.IsMatch(end.Value.Text))
                continue;
            fork.IsMerge = end.Value.Text.Contains("merge", StringComparison.OrdinalIgnoreCase);
            return fork;
        }
    }

    private bool TryNote(PlantUmlLine line, PlantUmlCursor cursor, List<UmlActivityStatement> target)
    {
        var m = NoteRegex().Match(line.Text);
        if (!m.Success)
            return false;
        var position = m.Groups["pos"].Value.Equals("left", StringComparison.OrdinalIgnoreCase)
            ? UmlNotePosition.Left : UmlNotePosition.Right;
        string text;
        if (m.Groups["text"].Success)
            text = PlantUmlText.Unescape(m.Groups["text"].Value);
        else
        {
            text = string.Join("\n", cursor.ReadUntil(EndNoteRegex(), out var found));
            if (!found)
                Error(line, "'end note' is missing.");
        }
        target.Add(new UmlActivityNote(text, position));
        return true;
    }

    private static string? Optional(Group group)
    {
        if (!group.Success)
            return null;
        var text = PlantUmlText.Unescape(group.Value);
        return text.Length > 0 ? text : null;
    }

    private static Regex AnyOf(Regex a, Regex b) => new($"(?:{a})|(?:{b})", RegexOptions.IgnoreCase);

    [GeneratedRegex(@"^\|(?:#[^|]*\|)?(?<name>[^|]+)\|$")]
    private static partial Regex LaneRegex();

    [GeneratedRegex(@"^-+(?:\[[^\]]*\])?>\s*(?<label>[^;]*);?$")]
    private static partial Regex ArrowRegex();

    [GeneratedRegex(@"^if\s*\((?<cond>.*)\)\s*(?:is\s*\((?<label2>[^)]*)\)\s*)?then\s*(?:\((?<label>[^)]*)\))?$", RegexOptions.IgnoreCase)]
    private static partial Regex IfRegex();

    [GeneratedRegex(@"^(?:else\s*if|elseif\s*\(.*|else\b.*|end\s*if)$|^elseif\b", RegexOptions.IgnoreCase)]
    private static partial Regex IfContinuationRegex();

    [GeneratedRegex(@"^else\s*if\s*\((?<cond>.*)\)\s*then\s*(?:\((?<label>[^)]*)\))?$", RegexOptions.IgnoreCase)]
    private static partial Regex ElseIfRegex();

    [GeneratedRegex(@"^else\s*(?:\((?<label>[^)]*)\))?$", RegexOptions.IgnoreCase)]
    private static partial Regex ElseRegex();

    [GeneratedRegex(@"^while\s*\((?<cond>.*?)\)\s*(?:is\s*\((?<label>[^)]*)\))?$", RegexOptions.IgnoreCase)]
    private static partial Regex WhileRegex();

    [GeneratedRegex(@"^end\s*while\s*(?:\((?<label>[^)]*)\))?$", RegexOptions.IgnoreCase)]
    private static partial Regex EndWhileRegex();

    [GeneratedRegex(@"^repeat\s+while\s*\((?<cond>.*?)\)\s*(?:is\s*\((?<label>[^)]*)\))?\s*(?:not\s*\((?<not>[^)]*)\))?$", RegexOptions.IgnoreCase)]
    private static partial Regex RepeatWhileRegex();

    [GeneratedRegex(@"^(?<kw>fork|split)$", RegexOptions.IgnoreCase)]
    private static partial Regex ForkRegex();

    [GeneratedRegex(@"^fork\s+again$", RegexOptions.IgnoreCase)]
    private static partial Regex ForkAgainRegex();

    [GeneratedRegex(@"^end\s*(fork|merge)(\s*\{.*\})?$", RegexOptions.IgnoreCase)]
    private static partial Regex EndForkRegex();

    [GeneratedRegex(@"^split\s+again$", RegexOptions.IgnoreCase)]
    private static partial Regex SplitAgainRegex();

    [GeneratedRegex(@"^end\s*split$", RegexOptions.IgnoreCase)]
    private static partial Regex EndSplitRegex();

    [GeneratedRegex(@"^(?:floating\s+)?note\s+(?<pos>left|right)\s*(?::\s*(?<text>.*))?$", RegexOptions.IgnoreCase)]
    private static partial Regex NoteRegex();

    [GeneratedRegex(@"^end\s*note$", RegexOptions.IgnoreCase)]
    private static partial Regex EndNoteRegex();

    [GeneratedRegex(@"^(partition|group)\b.*$", RegexOptions.IgnoreCase)]
    private static partial Regex PartitionRegex();

    [GeneratedRegex(@"^ref\s+(.+)$", RegexOptions.Singleline)]
    private static partial Regex RefActionRegex();
}
