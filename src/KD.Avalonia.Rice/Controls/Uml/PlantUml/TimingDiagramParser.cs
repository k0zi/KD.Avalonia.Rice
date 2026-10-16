using System.Globalization;
using System.Text.RegularExpressions;

namespace KD.Avalonia.Rice.Controls.Uml;

internal sealed partial class TimingDiagramParser(List<UmlDiagnostic> diagnostics) : DiagramParserBase(diagnostics)
{
    private readonly UmlTimingDiagram _diagram = new();
    private double _time;
    // Set by "@Id": the following "time is State" lines belong to that lifeline.
    private UmlTimingLifeline? _focus;

    public UmlTimingDiagram Parse(PlantUmlBlock block)
    {
        var cursor = new PlantUmlCursor(block.Lines);
        while (cursor.HasMore)
        {
            var line = cursor.Next();
            if (!ParseLine(line, cursor))
                Unrecognized(line);
        }
        return _diagram;
    }

    private bool ParseLine(PlantUmlLine line, PlantUmlCursor cursor)
    {
        var text = line.Text;
        if (TryCommon(line, cursor, _diagram))
            return true;

        var declaration = DeclarationRegex().Match(text);
        if (declaration.Success)
        {
            var rest = declaration.Groups["rest"].Value.Trim();
            var clock = ClockRegex().Match(rest);
            if (clock.Success)
                rest = rest[..clock.Index].Trim();
            var name = NameRegex().Match(rest);
            if (!name.Success)
                return false;

            var first = PlantUmlText.Unescape(name.Groups["a"].Value);
            var (id, display) = name.Groups["b"].Success ? (PlantUmlText.Unescape(name.Groups["b"].Value), first) : (first, first);
            var lifeline = new UmlTimingLifeline(id, display == id ? null : display,
                Enum.Parse<UmlTimingLifelineKind>(declaration.Groups["kw"].Value, true));
            if (clock.Success)
            {
                lifeline.ClockPeriod = Number(clock.Groups["period"].Value);
                if (clock.Groups["pulse"].Success)
                    lifeline.ClockPulse = Number(clock.Groups["pulse"].Value);
                if (clock.Groups["offset"].Success)
                    lifeline.ClockOffset = Number(clock.Groups["offset"].Value);
            }
            _diagram.Lifelines.Add(lifeline);
            return true;
        }

        var at = AtRegex().Match(text);
        if (at.Success)
        {
            var target = at.Groups["target"].Value;
            if (TryTime(target, out var time))
            {
                _time = time;
                _focus = null;
                return true;
            }
            _focus = Find(PlantUmlText.Unescape(target), line);
            return _focus is not null;
        }

        var has = HasRegex().Match(text);
        if (has.Success && Find(has.Groups["id"].Value, line) is { } withStates)
        {
            foreach (var state in has.Groups["states"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                withStates.States.Add(PlantUmlText.Unescape(state));
            return true;
        }

        var message = MessageRegex().Match(text);
        if (message.Success)
        {
            var from = Find(message.Groups["from"].Value, line);
            var to = Find(message.Groups["to"].Value, line);
            if (from is null || to is null)
                return true;
            var fromTime = message.Groups["ft"].Success ? ParseTime(message.Groups["ft"].Value, line) : _time;
            var toTime = message.Groups["tt"].Success ? ParseTime(message.Groups["tt"].Value, line) : fromTime;
            var label = message.Groups["text"].Success ? PlantUmlText.Unescape(message.Groups["text"].Value) : null;
            _diagram.Messages.Add(new UmlTimingMessage(from.Id, fromTime, to.Id, toTime, string.IsNullOrEmpty(label) ? null : label));
            return true;
        }

        // In an "@Id" block: "100 is Busy"; otherwise "Id is Busy" at the current time.
        var change = ChangeRegex().Match(text);
        if (change.Success)
        {
            var subject = change.Groups["subject"].Value;
            var state = PlantUmlText.Unescape(change.Groups["state"].Value.Trim());
            if (_focus is not null && TryTime(subject, out var time))
            {
                _time = time;
                _diagram.Changes.Add(new UmlTimingChange(_focus.Id, time, state));
                return true;
            }
            if (Find(PlantUmlText.Unescape(subject), line) is { } lifeline)
                _diagram.Changes.Add(new UmlTimingChange(lifeline.Id, _time, state));
            return true;
        }

        if (IgnoredRegex().IsMatch(text))
        {
            Info(line, $"'{text}' is not drawn.");
            return true;
        }
        return false;
    }

    private UmlTimingLifeline? Find(string id, PlantUmlLine line)
    {
        id = PlantUmlText.Unescape(id);
        var lifeline = _diagram.Find(id) ?? _diagram.Lifelines.FirstOrDefault(l => l.Name == id);
        if (lifeline is null)
            Warn(line, $"Unknown lifeline '{id}'.");
        return lifeline;
    }

    private bool TryTime(string text, out double time)
    {
        text = text.Trim();
        var relative = text.StartsWith('+');
        if (double.TryParse(relative ? text[1..] : text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            time = relative ? _time + value : value;
            return true;
        }
        time = 0;
        return false;
    }

    private double ParseTime(string text, PlantUmlLine line)
    {
        if (TryTime(text, out var time))
            return time;
        Warn(line, $"Invalid time '{text}'.");
        return _time;
    }

    private static double Number(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;

    [GeneratedRegex(@"^(?<kw>robust|concise|clock|binary)\s+(?<rest>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex DeclarationRegex();

    [GeneratedRegex(@"\s+with\s+period\s+(?<period>[\d.]+)(?:\s+pulse\s+(?<pulse>[\d.]+))?(?:\s+offset\s+(?<offset>[\d.]+))?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ClockRegex();

    [GeneratedRegex(@"^(?<a>""[^""]*""|\S+)(?:\s+as\s+(?<b>""[^""]*""|\S+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"^@\s*(?<target>[^\s]+|""[^""]*"")$")]
    private static partial Regex AtRegex();

    [GeneratedRegex(@"^(?<id>[\p{L}\p{N}_.]+)\s+has\s+(?<states>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex HasRegex();

    [GeneratedRegex(@"^(?<from>""[^""]*""|[\p{L}\p{N}_.]+)(?:@(?<ft>\+?[\d.]+))?\s*-+>\s*(?<to>""[^""]*""|[\p{L}\p{N}_.]+)(?:@(?<tt>\+?[\d.]+))?\s*(?::\s*(?<text>.*))?$")]
    private static partial Regex MessageRegex();

    [GeneratedRegex(@"^(?<subject>""[^""]*""|\+?[\p{L}\p{N}_.]+)\s+is\s+(?<state>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ChangeRegex();

    [GeneratedRegex(@"^(highlight|note|mode|hide|show|scale|\d+\s*<->)\b", RegexOptions.IgnoreCase)]
    private static partial Regex IgnoredRegex();
}
