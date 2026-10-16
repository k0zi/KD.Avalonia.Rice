using System.Text.RegularExpressions;
using KD.Avalonia.Rice.Controls.Uml;

namespace KD.Avalonia.Rice.Controls.Markdowns;

/// <summary>Mermaid sequence diagrams, read into a <see cref="UmlSequenceDiagram"/>.</summary>
internal static partial class MermaidSequenceParser
{
    [GeneratedRegex(@"^(?:create\s+)?(participant|actor)\s+(.+?)(?:\s+as\s+(.+))?$")]
    private static partial Regex ParticipantRegex();

    // A->>B: text, A-->>+B: text, A-)B, A-xB, A<<->>B
    [GeneratedRegex(@"^(.+?)\s*(<<-->>|<<->>|-->>|->>|-->|->|--x|-x|--\)|-\))\s*([+-]?)\s*([^:]+?)\s*(?::\s*(.*))?$")]
    private static partial Regex MessageRegex();

    [GeneratedRegex(@"^[Nn]ote\s+(left of|right of|over)\s+([^:]+?)\s*:\s*(.*)$")]
    private static partial Regex NoteRegex();

    [GeneratedRegex(@"^(loop|alt|opt|par|critical|break|rect|box)\b\s*(.*)$")]
    private static partial Regex FragmentRegex();

    public static UmlSequenceDiagram Parse(List<MermaidLine> lines, List<MermaidDiagnostic> diagnostics)
    {
        var diagram = new UmlSequenceDiagram { ShowFootbox = false };
        var pendingCreate = new HashSet<string>();
        // rect and box have no fragment of their own; their end is skipped.
        var blocks = new Stack<bool>();

        UmlParticipant Participant(string id)
        {
            id = id.Trim();
            return diagram.Find(id) ?? diagram.AddParticipant(id);
        }

        foreach (var line in lines)
        {
            var text = line.Text;
            if (text == "autonumber" || text.StartsWith("autonumber ", StringComparison.Ordinal))
            {
                diagram.AutoNumber = true;
                continue;
            }
            if (text.StartsWith("title ", StringComparison.Ordinal) || text.StartsWith("title:", StringComparison.Ordinal))
            {
                diagram.Title = text[6..].Trim();
                continue;
            }
            if (ParticipantRegex().Match(text) is { Success: true } p)
            {
                var id = p.Groups[2].Value.Trim();
                var participant = diagram.Find(id) ?? diagram.AddParticipant(id);
                participant.Kind = p.Groups[1].Value == "actor" ? UmlParticipantKind.Actor : UmlParticipantKind.Participant;
                if (p.Groups[3].Success)
                    participant.Name = MermaidParser.Label(p.Groups[3].Value);
                if (text.StartsWith("create", StringComparison.Ordinal))
                    pendingCreate.Add(id);
                continue;
            }
            if (text.StartsWith("destroy ", StringComparison.Ordinal))
            {
                diagram.Events.Add(new UmlActivation(Participant(text[8..]).Id, UmlActivationKind.Destroy));
                continue;
            }
            if (text.StartsWith("activate ", StringComparison.Ordinal) || text.StartsWith("deactivate ", StringComparison.Ordinal))
            {
                var activate = text[0] == 'a';
                diagram.Events.Add(new UmlActivation(Participant(text[(activate ? 9 : 11)..]).Id,
                    activate ? UmlActivationKind.Activate : UmlActivationKind.Deactivate));
                continue;
            }
            if (NoteRegex().Match(text) is { Success: true } note)
            {
                var position = note.Groups[1].Value switch
                {
                    "left of" => UmlNotePosition.Left,
                    "right of" => UmlNotePosition.Right,
                    _ => UmlNotePosition.Over,
                };
                var ids = note.Groups[2].Value.Split(',').Select(s => Participant(s).Id).ToList();
                diagram.Events.Add(new UmlSequenceNote(position, ids, MermaidParser.Label(note.Groups[3].Value)));
                continue;
            }
            if (FragmentRegex().Match(text) is { Success: true } fragment)
            {
                var label = fragment.Groups[2].Value.Trim();
                var kind = fragment.Groups[1].Value switch
                {
                    "loop" => UmlFragmentKind.Loop,
                    "alt" => UmlFragmentKind.Alt,
                    "opt" => UmlFragmentKind.Opt,
                    "par" => UmlFragmentKind.Par,
                    "critical" => UmlFragmentKind.Critical,
                    "break" => UmlFragmentKind.Break,
                    _ => (UmlFragmentKind?)null,
                };
                blocks.Push(kind is not null);
                if (kind is { } k)
                    diagram.Events.Add(new UmlFragmentStart(k, label.Length > 0 ? MermaidParser.Label(label) : null));
                continue;
            }
            if (Regex.Match(text, @"^(else|and|option)\b\s*(.*)$") is { Success: true } other)
            {
                var label = other.Groups[2].Value.Trim();
                diagram.Events.Add(new UmlFragmentElse(label.Length > 0 ? MermaidParser.Label(label) : null));
                continue;
            }
            if (text == "end")
            {
                if (!blocks.TryPop(out var real))
                    diagnostics.Add(new MermaidDiagnostic(line.Number, "'end' without a block."));
                else if (real)
                    diagram.Events.Add(new UmlFragmentEnd());
                continue;
            }
            if (MessageRegex().Match(text) is { Success: true } m)
            {
                var from = Participant(m.Groups[1].Value);
                var to = Participant(m.Groups[4].Value);
                var arrow = m.Groups[2].Value;
                var kind = arrow switch
                {
                    "-->>" or "-->" or "--x" or "--)" or "<<-->>" => UmlMessageKind.Reply,
                    "-)" => UmlMessageKind.Asynchronous,
                    _ => UmlMessageKind.Synchronous,
                };
                var message = diagram.AddMessage(from.Id, to.Id, m.Groups[5].Success ? MermaidParser.Label(m.Groups[5].Value) : null, kind);
                if (m.Groups[3].Value == "+")
                    message.ActivateTarget = true;
                else if (m.Groups[3].Value == "-")
                    message.DeactivateSource = true;
                if (pendingCreate.Remove(to.Id))
                    message.CreateTarget = true;
                continue;
            }
            diagnostics.Add(new MermaidDiagnostic(line.Number, $"Unrecognized line: {text}"));
        }
        return diagram;
    }
}

/// <summary>Mermaid class diagrams, read into a <see cref="UmlGraphDiagram"/> of <see cref="UmlDiagramKind.Class"/>.</summary>
internal static partial class MermaidClassParser
{
    [GeneratedRegex(@"^class\s+([\w.]+)(?:~([^~]+)~)?(?:\[""?([^\]""]*)""?\])?(?::::\w+)?\s*(\{)?\s*(\})?$")]
    private static partial Regex ClassRegex();

    [GeneratedRegex(@"^([\w.]+)\s*(?:""([^""]*)""\s*)?(<\||\*|o|<)?(--|\.\.)(\|>|\*|o|>)?\s*(?:""([^""]*)""\s*)?([\w.]+)\s*(?::\s*(.*))?$")]
    private static partial Regex RelationRegex();

    [GeneratedRegex(@"^<<\s*(\w+)\s*>>\s*([\w.]+)?$")]
    private static partial Regex AnnotationRegex();

    [GeneratedRegex(@"^([\w.]+)\s*:\s*(.+)$")]
    private static partial Regex MemberLineRegex();

    [GeneratedRegex(@"^note\s+(?:for\s+([\w.]+)\s+)?""(.*)""$")]
    private static partial Regex NoteRegex();

    public static UmlGraphDiagram Parse(List<MermaidLine> lines, List<MermaidDiagnostic> diagnostics)
    {
        var diagram = new UmlGraphDiagram(UmlDiagramKind.Class);
        var classes = new Dictionary<string, UmlElement>();
        var namespaces = new Stack<UmlElement>();
        UmlElement? body = null;
        var notes = 0;

        UmlElement Class(string id)
        {
            if (classes.TryGetValue(id, out var element))
                return element;
            element = new UmlElement(UmlElementKind.Class, id);
            classes[id] = element;
            if (namespaces.TryPeek(out var ns))
                ns.Add(element);
            else
                diagram.Add(element);
            return element;
        }

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
                if (AnnotationRegex().Match(text) is { Success: true } inner)
                    Annotate(body, inner.Groups[1].Value);
                else
                    body.Members.Add(Member(text));
                continue;
            }
            if (text.StartsWith("direction ", StringComparison.Ordinal))
            {
                if (MermaidParser.Direction(text[10..]) is MermaidDirection.LeftToRight or MermaidDirection.RightToLeft)
                    diagram.Direction = UmlDirection.LeftToRight;
                continue;
            }
            if (text.StartsWith("namespace ", StringComparison.Ordinal) && text.EndsWith('{'))
            {
                var name = text[10..^1].Trim();
                var package = new UmlElement(UmlElementKind.Package, name);
                if (namespaces.TryPeek(out var outer))
                    outer.Add(package);
                else
                    diagram.Add(package);
                namespaces.Push(package);
                continue;
            }
            if (text == "}" && namespaces.Count > 0)
            {
                namespaces.Pop();
                continue;
            }
            if (ClassRegex().Match(text) is { Success: true } c)
            {
                var element = Class(c.Groups[1].Value);
                if (c.Groups[2].Success)
                    element.Name = $"{c.Groups[1].Value}<{c.Groups[2].Value.Replace('~', '<')}>";
                if (c.Groups[3].Success && c.Groups[3].Length > 0)
                    element.Name = c.Groups[3].Value;
                if (c.Groups[4].Success && !c.Groups[5].Success)
                    body = element;
                continue;
            }
            if (AnnotationRegex().Match(text) is { Success: true } a && a.Groups[2].Success)
            {
                Annotate(Class(a.Groups[2].Value), a.Groups[1].Value);
                continue;
            }
            if (NoteRegex().Match(text) is { Success: true } n)
            {
                var noteElement = new UmlElement(UmlElementKind.Note, $"note{++notes}") { Text = MermaidParser.Label(n.Groups[2].Value) };
                diagram.Add(noteElement);
                if (n.Groups[1].Success)
                    diagram.Relations.Add(new UmlRelation(noteElement.Id, Class(n.Groups[1].Value).Id) { LineStyle = UmlLineStyle.Dashed, Length = 1 });
                continue;
            }
            if (RelationRegex().Match(text) is { Success: true } r)
            {
                var from = Class(r.Groups[1].Value);
                var to = Class(r.Groups[7].Value);
                diagram.Relations.Add(new UmlRelation(from.Id, to.Id)
                {
                    FromHead = Head(r.Groups[3].Value),
                    ToHead = Head(r.Groups[5].Value),
                    LineStyle = r.Groups[4].Value == ".." ? UmlLineStyle.Dashed : UmlLineStyle.Solid,
                    FromLabel = r.Groups[2].Success ? r.Groups[2].Value : null,
                    ToLabel = r.Groups[6].Success ? r.Groups[6].Value : null,
                    Label = r.Groups[8].Success ? MermaidParser.Label(r.Groups[8].Value) : null,
                });
                continue;
            }
            if (MemberLineRegex().Match(text) is { Success: true } member)
            {
                var value = member.Groups[2].Value.Trim();
                var annotation = AnnotationRegex().Match(value);
                if (annotation.Success)
                    Annotate(Class(member.Groups[1].Value), annotation.Groups[1].Value);
                else
                    Class(member.Groups[1].Value).Members.Add(Member(value));
                continue;
            }
            if (Regex.IsMatch(text, @"^(classDef|cssClass|style|click|callback|link)\b"))
                continue;
            diagnostics.Add(new MermaidDiagnostic(line.Number, $"Unrecognized line: {text}"));
        }
        return diagram;
    }

    private static void Annotate(UmlElement element, string annotation)
    {
        switch (annotation.ToLowerInvariant())
        {
            case "interface":
                element.Kind = UmlElementKind.Interface;
                break;
            case "abstract":
                element.Kind = UmlElementKind.AbstractClass;
                break;
            case "enumeration" or "enum":
                element.Kind = UmlElementKind.Enum;
                break;
            default:
                element.Stereotypes.Add(annotation);
                break;
        }
    }

    private static UmlArrowHead Head(string mark) => mark switch
    {
        "<|" or "|>" => UmlArrowHead.Triangle,
        "*" => UmlArrowHead.FilledDiamond,
        "o" => UmlArrowHead.Diamond,
        "<" or ">" => UmlArrowHead.Arrow,
        _ => UmlArrowHead.None,
    };

    /// <summary><c>+List~int~ items</c>, <c>+Area()$ double</c>, <c>-Speak()* void</c>.</summary>
    private static UmlMember Member(string text)
    {
        var visibility = text.Length > 0 ? text[0] switch
        {
            '+' => UmlVisibility.Public,
            '-' => UmlVisibility.Private,
            '#' => UmlVisibility.Protected,
            '~' => UmlVisibility.Package,
            _ => UmlVisibility.None,
        } : UmlVisibility.None;
        if (visibility != UmlVisibility.None)
            text = text[1..];
        var isStatic = false;
        var isAbstract = false;
        var close = text.LastIndexOf(')');
        if (close >= 0 && close + 1 < text.Length && text[close + 1] is '$' or '*')
        {
            isStatic = text[close + 1] == '$';
            isAbstract = text[close + 1] == '*';
            text = text.Remove(close + 1, 1);
        }
        else if (text.EndsWith('$') || text.EndsWith('*'))
        {
            isStatic = text[^1] == '$';
            isAbstract = text[^1] == '*';
            text = text[..^1];
        }
        text = Regex.Replace(text, "~([^~]+)~", "<$1>").Trim();
        // Method return type after the parentheses: Fetch() bool → Fetch() : bool
        if (close >= 0 && text.LastIndexOf(')') is var paren and >= 0 && paren + 1 < text.Length && !text[(paren + 1)..].TrimStart().StartsWith(':'))
            text = $"{text[..(paren + 1)]} : {text[(paren + 1)..].Trim()}";
        return new UmlMember(text, visibility) { IsStatic = isStatic, IsAbstract = isAbstract };
    }
}

/// <summary>Mermaid state diagrams, read into a <see cref="UmlGraphDiagram"/> of <see cref="UmlDiagramKind.State"/>.</summary>
internal static partial class MermaidStateParser
{
    [GeneratedRegex(@"^(\[\*\]|[\w.]+)\s*-->\s*(\[\*\]|[\w.]+)\s*(?::\s*(.*))?$")]
    private static partial Regex TransitionRegex();

    [GeneratedRegex(@"^state\s+(?:""([^""]*)""\s+as\s+([\w.]+)|([\w.]+)(?:\s+as\s+""?([^""{]*)""?)?)\s*(<<\s*(\w+)\s*>>)?\s*(\{)?$")]
    private static partial Regex StateRegex();

    [GeneratedRegex(@"^([\w.]+)\s*:\s*(.*)$")]
    private static partial Regex DescriptionRegex();

    [GeneratedRegex(@"^note\s+(left of|right of)\s+([\w.]+)\s*(?::\s*(.*))?$")]
    private static partial Regex NoteRegex();

    public static UmlGraphDiagram Parse(List<MermaidLine> lines, List<MermaidDiagnostic> diagnostics)
    {
        var diagram = new UmlGraphDiagram(UmlDiagramKind.State);
        var states = new Dictionary<string, UmlElement>();
        var scopes = new Stack<UmlElement>();
        var notes = 0;
        UmlElement? openNote = null;

        void Place(UmlElement element)
        {
            if (scopes.TryPeek(out var parent))
                parent.Add(element);
            else
                diagram.Add(element);
        }

        UmlElement State(string id)
        {
            if (states.TryGetValue(id, out var element))
                return element;
            element = new UmlElement(UmlElementKind.State, id);
            states[id] = element;
            Place(element);
            return element;
        }

        // [*] is the initial state as a source and the final state as a target, one of each per composite state.
        UmlElement Pseudo(bool target)
        {
            var owner = scopes.TryPeek(out var parent) ? parent.Id : "";
            var id = owner + (target ? "[*]end" : "[*]start");
            if (states.TryGetValue(id, out var element))
                return element;
            element = new UmlElement(target ? UmlElementKind.FinalState : UmlElementKind.InitialState, id, "");
            states[id] = element;
            Place(element);
            return element;
        }

        foreach (var line in lines)
        {
            var text = line.Text;
            if (openNote is not null)
            {
                if (text is "end note" or "endnote")
                    openNote = null;
                else
                    openNote.Text = openNote.Text is { Length: > 0 } t ? t + "\n" + text : text;
                continue;
            }
            if (text.StartsWith("direction ", StringComparison.Ordinal))
            {
                if (scopes.Count == 0 && MermaidParser.Direction(text[10..]) is MermaidDirection.LeftToRight or MermaidDirection.RightToLeft)
                    diagram.Direction = UmlDirection.LeftToRight;
                continue;
            }
            if (text == "}")
            {
                if (!scopes.TryPop(out _))
                    diagnostics.Add(new MermaidDiagnostic(line.Number, "'}' without a composite state."));
                continue;
            }
            // Separator of concurrent regions; drawn as one region.
            if (text is "--" or "||")
                continue;
            if (TransitionRegex().Match(text) is { Success: true } t2)
            {
                var from = t2.Groups[1].Value == "[*]" ? Pseudo(false) : State(t2.Groups[1].Value);
                var to = t2.Groups[2].Value == "[*]" ? Pseudo(true) : State(t2.Groups[2].Value);
                diagram.Relations.Add(new UmlRelation(from.Id, to.Id)
                {
                    ToHead = UmlArrowHead.Arrow,
                    Label = t2.Groups[3].Success && t2.Groups[3].Length > 0 ? MermaidParser.Label(t2.Groups[3].Value) : null,
                });
                continue;
            }
            if (StateRegex().Match(text) is { Success: true } s)
            {
                var id = s.Groups[2].Success ? s.Groups[2].Value : s.Groups[3].Value;
                var element = State(id);
                if (s.Groups[1].Success)
                    element.Name = MermaidParser.Label(s.Groups[1].Value);
                else if (s.Groups[4].Success && s.Groups[4].Value.Trim().Length > 0)
                    element.Name = MermaidParser.Label(s.Groups[4].Value.Trim());
                if (s.Groups[6].Success)
                {
                    element.Kind = s.Groups[6].Value switch
                    {
                        "choice" => UmlElementKind.Choice,
                        "fork" => UmlElementKind.Fork,
                        "join" => UmlElementKind.Join,
                        _ => element.Kind,
                    };
                    if (element.Kind != UmlElementKind.State)
                        element.Name = "";
                }
                if (s.Groups[7].Success)
                    scopes.Push(element);
                continue;
            }
            if (NoteRegex().Match(text) is { Success: true } n)
            {
                var note = new UmlElement(UmlElementKind.Note, $"note{++notes}") { Text = n.Groups[3].Success ? MermaidParser.Label(n.Groups[3].Value) : "" };
                Place(note);
                diagram.Relations.Add(new UmlRelation(note.Id, State(n.Groups[2].Value).Id)
                {
                    LineStyle = UmlLineStyle.Dashed,
                    Length = 1,
                    Direction = n.Groups[1].Value == "left of" ? UmlLinkDirection.Right : UmlLinkDirection.Left,
                });
                if (!n.Groups[3].Success)
                    openNote = note;
                continue;
            }
            if (DescriptionRegex().Match(text) is { Success: true } d)
            {
                State(d.Groups[1].Value).AddMember(MermaidParser.Label(d.Groups[2].Value));
                continue;
            }
            if (Regex.IsMatch(text, @"^(classDef|class|style|click)\b") || text.StartsWith(':'))
                continue;
            if (Regex.IsMatch(text, @"^[\w.]+$"))
            {
                State(text);
                continue;
            }
            diagnostics.Add(new MermaidDiagnostic(line.Number, $"Unrecognized line: {text}"));
        }
        return diagram;
    }
}
