using System.Text.RegularExpressions;

namespace KD.Avalonia.Rice.Controls.Uml;

internal sealed partial class SequenceDiagramParser(List<UmlDiagnostic> diagnostics) : DiagramParserBase(diagnostics)
{
    private readonly UmlSequenceDiagram _diagram = new();
    private readonly List<(string Caller, string Callee)> _calls = [];
    private readonly HashSet<string> _pendingCreates = [];
    private int _openFragments;

    public UmlSequenceDiagram Parse(PlantUmlBlock block)
    {
        var cursor = new PlantUmlCursor(block.Lines);
        while (cursor.HasMore)
        {
            var line = cursor.Next();
            if (!ParseLine(line, cursor))
                Unrecognized(line);
        }
        if (_openFragments > 0)
            Error(cursor.LastNumber, $"{_openFragments} fragment(s) not closed with 'end'.");
        return _diagram;
    }

    private bool ParseLine(PlantUmlLine line, PlantUmlCursor cursor)
    {
        var text = line.Text;

        if (FootboxRegex().IsMatch(text))
        {
            _diagram.ShowFootbox = false;
            return true;
        }
        if (text.StartsWith("autonumber", StringComparison.OrdinalIgnoreCase))
        {
            _diagram.AutoNumber = !text.Contains("stop", StringComparison.OrdinalIgnoreCase);
            return true;
        }
        if (TryCommon(line, cursor, _diagram))
            return true;

        var participant = ParticipantRegex().Match(text);
        if (participant.Success)
            return TryParticipant(participant.Groups["kw"].Value, participant.Groups["rest"].Value, line);

        var create = CreateRegex().Match(text);
        if (create.Success)
        {
            var rest = create.Groups["rest"].Value;
            var kindMatch = ParticipantRegex().Match(rest);
            if (kindMatch.Success)
                TryParticipant(kindMatch.Groups["kw"].Value, kindMatch.Groups["rest"].Value, line);
            var id = kindMatch.Success ? LastParticipantId() : Ensure(PlantUmlText.Unescape(rest)).Id;
            if (id is not null)
                _pendingCreates.Add(id);
            return true;
        }

        var activation = ActivationRegex().Match(text);
        if (activation.Success)
        {
            var id = Ensure(PlantUmlText.Unescape(PlantUmlText.StripDecorations(activation.Groups["id"].Value))).Id;
            var kind = activation.Groups["kw"].Value.ToLowerInvariant() switch
            {
                "activate" => UmlActivationKind.Activate,
                "deactivate" => UmlActivationKind.Deactivate,
                _ => UmlActivationKind.Destroy,
            };
            _diagram.Events.Add(new UmlActivation(id, kind));
            if (kind == UmlActivationKind.Activate && LastMessage() is { } last && last.To == id)
                _calls.Add((last.From, id));
            else if (kind == UmlActivationKind.Deactivate)
                PopCall(id);
            return true;
        }

        var ret = ReturnRegex().Match(text);
        if (ret.Success)
        {
            if (_calls.Count == 0)
            {
                Warn(line, "'return' without an activation to return from.");
                return true;
            }
            var (caller, callee) = _calls[^1];
            _calls.RemoveAt(_calls.Count - 1);
            _diagram.Events.Add(new UmlMessage(callee, caller, NullIfEmpty(ret.Groups["text"].Value), UmlMessageKind.Reply)
            {
                DeactivateSource = true,
            });
            return true;
        }

        var fragment = FragmentRegex().Match(text);
        if (fragment.Success)
        {
            var kind = Enum.Parse<UmlFragmentKind>(fragment.Groups["kw"].Value, true);
            _diagram.Events.Add(new UmlFragmentStart(kind, NullIfEmpty(fragment.Groups["label"].Value)));
            _openFragments++;
            return true;
        }

        var elseMatch = ElseRegex().Match(text);
        if (elseMatch.Success)
        {
            if (_openFragments == 0)
                Warn(line, "'else' outside of a fragment.");
            _diagram.Events.Add(new UmlFragmentElse(NullIfEmpty(elseMatch.Groups["label"].Value)));
            return true;
        }

        if (EndRegex().IsMatch(text))
        {
            if (_openFragments == 0)
            {
                Warn(line, "'end' without an open fragment.");
                return true;
            }
            _openFragments--;
            _diagram.Events.Add(new UmlFragmentEnd());
            return true;
        }

        var reference = RefRegex().Match(text);
        if (reference.Success)
        {
            var ids = reference.Groups["ids"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(i => Ensure(PlantUmlText.Unescape(i)).Id).ToList();
            string refText;
            if (reference.Groups["text"].Success)
                refText = PlantUmlText.Unescape(reference.Groups["text"].Value);
            else
            {
                refText = string.Join("\n", cursor.ReadUntil(EndRefRegex(), out var found));
                if (!found)
                    Error(line, "'end ref' is missing.");
            }
            _diagram.Events.Add(new UmlInteractionUse(ids, refText));
            return true;
        }

        var divider = DividerRegex().Match(text);
        if (divider.Success)
        {
            _diagram.Events.Add(new UmlDivider(NullIfEmpty(divider.Groups[1].Value.Trim())));
            return true;
        }

        var delay = DelayRegex().Match(text);
        if (delay.Success)
        {
            _diagram.Events.Add(new UmlDelay(NullIfEmpty(delay.Groups[1].Value.Trim())));
            return true;
        }

        var spacing = SpacingRegex().Match(text);
        if (spacing.Success)
        {
            _diagram.Events.Add(new UmlSpacing(spacing.Groups[1].Success ? double.Parse(spacing.Groups[1].Value) : null));
            return true;
        }

        if (TryNote(line, cursor))
            return true;

        return TryMessage(text);
    }

    private bool TryParticipant(string keyword, string rest, PlantUmlLine line)
    {
        var stereotypes = new List<string>();
        rest = PlantUmlText.TakeStereotypes(rest, stereotypes);
        rest = OrderRegex().Replace(PlantUmlText.StripDecorations(rest), "").Trim();

        var m = NameRegex().Match(rest);
        if (!m.Success)
        {
            Warn(line, $"Invalid participant: {rest}");
            return true;
        }
        var first = PlantUmlText.Unescape(m.Groups["a"].Value);
        var (id, name) = first switch
        {
            _ when !m.Groups["b"].Success => (first, first),
            _ when m.Groups["b"].Value.StartsWith('"') && !m.Groups["a"].Value.StartsWith('"') =>
                (first, PlantUmlText.Unescape(m.Groups["b"].Value)),
            _ => (PlantUmlText.Unescape(m.Groups["b"].Value), first),
        };

        var participantKind = Enum.Parse<UmlParticipantKind>(keyword, true);
        var participant = _diagram.Find(id);
        if (participant is null)
            participant = _diagram.AddParticipant(id, name == id ? null : name, participantKind);
        else
        {
            participant.Kind = participantKind;
            if (name != id)
                participant.Name = name;
        }
        participant.Stereotypes.AddRange(stereotypes);
        return true;
    }

    private string? LastParticipantId() => _diagram.Participants.Count > 0 ? _diagram.Participants[^1].Id : null;

    private bool TryMessage(string text)
    {
        var m = MessageRegex().Match(text);
        if (!m.Success || (m.Groups["l"].Length == 0 && m.Groups["r"].Length == 0))
            return false;

        var left = m.Groups["from"].Success ? m.Groups["from"].Value : "";
        var right = m.Groups["to"].Success ? m.Groups["to"].Value : "";
        var leftHead = m.Groups["l"].Value;
        var rightHead = m.Groups["r"].Value;
        var dashed = m.Groups["b1"].Length + m.Groups["b2"].Length >= 2;
        var reversed = leftHead.Contains('<') && rightHead.Length == 0;

        string Endpoint(string token, bool isLeft) => token switch
        {
            "" or "[" or "?" when isLeft => UmlSequenceDiagram.LeftBorder,
            "" or "]" or "?" => UmlSequenceDiagram.RightBorder,
            "[" => UmlSequenceDiagram.LeftBorder,
            _ => Ensure(PlantUmlText.Unescape(token)).Id,
        };

        var leftId = Endpoint(left, true);
        var rightId = Endpoint(right, false);
        var (from, to) = reversed ? (rightId, leftId) : (leftId, rightId);
        if (left.Length == 0 && right.Length == 0)
            return false;

        var head = reversed ? leftHead : rightHead;
        var kind = dashed ? UmlMessageKind.Reply
            : head is ">>" or "<<" or "\\\\" or "//" or "\\" or "/" ? UmlMessageKind.Asynchronous
            : UmlMessageKind.Synchronous;

        var message = new UmlMessage(from, to, m.Groups["text"].Success ? NullIfEmpty(PlantUmlText.Unescape(m.Groups["text"].Value)) : null, kind);
        var modifiers = m.Groups["mods"].Value;
        message.ActivateTarget = modifiers.Contains("++");
        message.DeactivateSource = modifiers.Contains("--");
        message.CreateTarget = modifiers.Contains("**") || _pendingCreates.Remove(to);
        message.DestroyTarget = modifiers.Contains("!!");
        _diagram.Events.Add(message);

        if (message.ActivateTarget)
            _calls.Add((from, to));
        if (message.DeactivateSource)
            PopCall(from);
        return true;
    }

    private bool TryNote(PlantUmlLine line, PlantUmlCursor cursor)
    {
        var m = NoteRegex().Match(line.Text);
        if (!m.Success)
            return false;

        var position = m.Groups["pos"].Value.ToLowerInvariant() switch
        {
            "left" => UmlNotePosition.Left,
            "right" => UmlNotePosition.Right,
            _ => UmlNotePosition.Over,
        };
        var ids = m.Groups["ids"].Success
            ? m.Groups["ids"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(i => Ensure(PlantUmlText.Unescape(PlantUmlText.StripDecorations(i))).Id).ToList()
            : [];
        // "note left" without "of": next to the participant of the previous message.
        if (ids.Count == 0 && LastMessage() is { } last)
            ids.Add(position == UmlNotePosition.Left ? last.From : last.To);
        ids.RemoveAll(i => i is UmlSequenceDiagram.LeftBorder or UmlSequenceDiagram.RightBorder);
        if (ids.Count == 0 && _diagram.Participants.Count > 0)
            ids.Add(_diagram.Participants[0].Id);

        string text;
        if (m.Groups["text"].Success)
            text = PlantUmlText.Unescape(m.Groups["text"].Value);
        else
        {
            text = string.Join("\n", cursor.ReadUntil(EndNoteRegex(), out var found));
            if (!found)
                Error(line, "'end note' is missing.");
        }
        _diagram.Events.Add(new UmlSequenceNote(position, ids, text));
        return true;
    }

    private UmlParticipant Ensure(string id)
    {
        id = id.Trim();
        return _diagram.Find(id) ?? _diagram.Participants.FirstOrDefault(p => p.Name == id) ?? _diagram.AddParticipant(id);
    }

    private UmlMessage? LastMessage() => _diagram.Events.OfType<UmlMessage>().LastOrDefault();

    private void PopCall(string callee)
    {
        var index = _calls.FindLastIndex(c => c.Callee == callee);
        if (index >= 0)
            _calls.RemoveAt(index);
    }

    private static string? NullIfEmpty(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    [GeneratedRegex(@"^hide\s+footbox$", RegexOptions.IgnoreCase)]
    private static partial Regex FootboxRegex();

    [GeneratedRegex(@"^(?<kw>participant|actor|boundary|control|entity|database|collections|queue)\s+(?<rest>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ParticipantRegex();

    [GeneratedRegex(@"^create\s+(?<rest>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex CreateRegex();

    [GeneratedRegex(@"\s+order\s+-?\d+", RegexOptions.IgnoreCase)]
    private static partial Regex OrderRegex();

    [GeneratedRegex(@"^(?<a>""[^""]*""|\S+)(?:\s+as\s+(?<b>""[^""]*""|\S+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"^(?<kw>activate|deactivate|destroy)\s+(?<id>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ActivationRegex();

    [GeneratedRegex(@"^return\b\s*(?<text>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex ReturnRegex();

    [GeneratedRegex(@"^(?<kw>alt|opt|loop|par|break|critical|group)\b\s*(?<label>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex FragmentRegex();

    [GeneratedRegex(@"^else\b\s*(?<label>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex ElseRegex();

    [GeneratedRegex(@"^end(\s+(alt|opt|loop|par|break|critical|group))?$", RegexOptions.IgnoreCase)]
    private static partial Regex EndRegex();

    [GeneratedRegex(@"^ref\s+over\s+(?<ids>[^:]+?)\s*(?::\s*(?<text>.*))?$", RegexOptions.IgnoreCase)]
    private static partial Regex RefRegex();

    [GeneratedRegex(@"^end\s*ref$", RegexOptions.IgnoreCase)]
    private static partial Regex EndRefRegex();

    [GeneratedRegex(@"^==(.*)==$")]
    private static partial Regex DividerRegex();

    [GeneratedRegex(@"^\.\.\.(.*?)(?:\.\.\.)?$")]
    private static partial Regex DelayRegex();

    [GeneratedRegex(@"^\|\|(\d+)?\|\|\|?$")]
    private static partial Regex SpacingRegex();

    [GeneratedRegex(@"^[hr]?note\s+(?<pos>left|right|over)(?:\s+of)?\s*(?<ids>[^:#<]+?)?\s*(?:<<[^>]*>>)?\s*(?:#\S+)?\s*(?::\s*(?<text>.*))?$", RegexOptions.IgnoreCase)]
    private static partial Regex NoteRegex();

    [GeneratedRegex(@"^end\s*[hr]?note$", RegexOptions.IgnoreCase)]
    private static partial Regex EndNoteRegex();

    // from, arrow (l, b1, [options], b2, r), to, ++/--/**/!! modifiers, ": text"
    [GeneratedRegex(@"^(?<from>""[^""]*""|[\p{L}\p{N}_.]+|\[|\?)?\s*(?<l>x|o|<<|<|\\\\|\\|//|/)?(?<b1>-+)(?:\[[^\]]*\])?(?<b2>-*)(?<r>>>|>|\\\\|\\|//|/|x|o)?\s*(?<to>""[^""]*""|[\p{L}\p{N}_.]+|\]|\?)?\s*(?<mods>(?:\+\+|--|\*\*|!!)*)\s*(?::\s*(?<text>.*))?$")]
    private static partial Regex MessageRegex();
}
