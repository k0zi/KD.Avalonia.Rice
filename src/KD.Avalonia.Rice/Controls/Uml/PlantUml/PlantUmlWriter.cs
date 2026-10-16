using System.Globalization;
using System.Text;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>
/// Writes the diagram model as PlantUML text. When PlantUML would recognize the text as another diagram type (the
/// kinds without a syntax of their own), a <c>' rice:kind=…</c> comment keeps the type for <see cref="PlantUmlParser"/>.
/// </summary>
public static class PlantUmlWriter
{
    public static string Write(UmlDiagram diagram)
    {
        var body = new Body();
        if (diagram.Title is { Length: > 0 } title)
            body.Line(title.Contains('\n') ? "title\n" + title + "\nend title" : "title " + title);
        if (diagram.Direction == UmlDirection.LeftToRight)
            body.Line("left to right direction");

        switch (diagram)
        {
            case UmlGraphDiagram graph:
                WriteGraph(graph, body);
                break;
            case UmlActivityDiagram activity:
                WriteStatements(activity.Statements, body);
                break;
            case UmlSequenceDiagram sequence:
                WriteSequence(sequence, body);
                break;
            case UmlTimingDiagram timing:
                WriteTiming(timing, body);
                break;
        }

        var sb = new StringBuilder();
        sb.Append("@startuml");
        if (diagram.Name is { Length: > 0 } name)
            sb.Append(' ').Append(name);
        sb.Append('\n');

        var lines = body.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select((l, i) => new PlantUmlLine(i + 1, l.Trim())).ToList();
        if (PlantUmlKindDetector.Detect(lines) != diagram.Kind)
            sb.Append("' rice:kind=").Append(diagram.Kind.ToString().ToLowerInvariant()).Append('\n');
        sb.Append(body);
        sb.Append("@enduml\n");
        return sb.ToString();
    }

    private sealed class Body
    {
        private readonly StringBuilder _sb = new();

        public int Indent { get; set; }

        public void Line(string text)
        {
            foreach (var line in text.Split('\n'))
                _sb.Append(' ', Indent * 2).Append(line).Append('\n');
        }

        public override string ToString() => _sb.ToString();
    }

    // ---- graph diagrams ----

    private static void WriteGraph(UmlGraphDiagram diagram, Body body)
    {
        var parents = new Dictionary<string, UmlElement?>();
        foreach (var e in diagram.Elements)
            CollectParents(e, null, parents);

        // Relations touching [*] or [H] belong into the block of their container, the rest goes after the elements.
        UmlElement? ScopeOf(UmlRelation r)
        {
            foreach (var id in new[] { r.From, r.To })
                if (diagram.Find(id) is { IsPseudoState: true } pseudo && parents.TryGetValue(pseudo.Id, out var parent))
                    return parent;
            return null;
        }
        var scoped = diagram.Relations.ToLookup(r => ScopeOf(r)?.Id);

        foreach (var element in diagram.Elements)
            WriteElement(diagram, element, body, scoped);
        foreach (var relation in scoped[null])
            WriteRelation(diagram, relation, body);
    }

    private static void CollectParents(UmlElement element, UmlElement? parent, Dictionary<string, UmlElement?> parents)
    {
        parents[element.Id] = parent;
        foreach (var child in element.Children)
            CollectParents(child, element, parents);
    }

    private static void WriteElement(UmlGraphDiagram diagram, UmlElement element, Body body, ILookup<string?, UmlRelation> scoped)
    {
        if (element.IsPseudoState)
            return;

        if (element.Kind == UmlElementKind.Note)
        {
            var text = element.Text ?? "";
            if (text.Contains('\n'))
                body.Line($"note as {PlantUmlText.Ref(element.Id)}\n{text}\nend note");
            else
                body.Line($"note {PlantUmlText.Quote(text)} as {PlantUmlText.Ref(element.Id)}");
            return;
        }

        var sb = new StringBuilder(Keyword(diagram, element)).Append(' ');
        if (element.Name == element.Id)
            sb.Append(PlantUmlText.Ref(element.Id));
        else
            sb.Append(PlantUmlText.Quote(element.Name)).Append(" as ").Append(PlantUmlText.Ref(element.Id));

        var stereotype = element.Kind switch
        {
            UmlElementKind.Choice => "choice",
            UmlElementKind.Fork => "fork",
            UmlElementKind.Join => "join",
            UmlElementKind.EntryPoint => "entryPoint",
            UmlElementKind.ExitPoint => "exitPoint",
            _ => null,
        };
        foreach (var s in element.Stereotypes.Prepend(stereotype).OfType<string>())
            sb.Append(" <<").Append(s).Append(">>");

        var classifier = element.Kind is UmlElementKind.Class or UmlElementKind.AbstractClass or UmlElementKind.Interface
            or UmlElementKind.Enum or UmlElementKind.Annotation or UmlElementKind.Entity or UmlElementKind.Metaclass
            or UmlElementKind.Stereotype or UmlElementKind.Object;

        if (classifier && element.Members.Count > 0)
        {
            body.Line(sb + " {");
            body.Indent++;
            foreach (var member in element.Members)
                body.Line(FormatMember(member));
            body.Indent--;
            body.Line("}");
        }
        else if (element.Children.Count > 0)
        {
            body.Line(sb + " {");
            body.Indent++;
            foreach (var child in element.Children)
                WriteElement(diagram, child, body, scoped);
            foreach (var relation in scoped[element.Id])
                WriteRelation(diagram, relation, body);
            body.Indent--;
            body.Line("}");
        }
        else
            body.Line(sb.ToString());

        // Members of non-classifiers (internal activities of states, descriptions): "Id : text" lines.
        if (!classifier)
            foreach (var member in element.Members)
                body.Line($"{PlantUmlText.Ref(element.Id)} : {FormatMember(member)}");
    }

    private static string Keyword(UmlGraphDiagram diagram, UmlElement element)
    {
        var structural = diagram.Kind is UmlDiagramKind.Component or UmlDiagramKind.Deployment
            or UmlDiagramKind.CompositeStructure or UmlDiagramKind.UseCase or UmlDiagramKind.Package;
        return element.Kind switch
        {
            UmlElementKind.AbstractClass => "abstract class",
            UmlElementKind.InterfaceCircle => structural ? "interface" : "circle",
            UmlElementKind.EntityIcon or UmlElementKind.Entity => "entity",
            UmlElementKind.Choice or UmlElementKind.Fork or UmlElementKind.Join or UmlElementKind.EntryPoint
                or UmlElementKind.ExitPoint => "state",
            UmlElementKind.UseCase => "usecase",
            UmlElementKind.PortIn => "portin",
            UmlElementKind.PortOut => "portout",
            _ => element.Kind.ToString().ToLowerInvariant(),
        };
    }

    private static string FormatMember(UmlMember member)
    {
        if (member.Kind == UmlMemberKind.Separator)
            return member.Text.Length > 0 ? $"-- {member.Text} --" : "--";

        var sb = new StringBuilder();
        if (member.IsStatic)
            sb.Append("{static} ");
        if (member.IsAbstract)
            sb.Append("{abstract} ");
        var looksLikeMethod = member.Text.Contains('(');
        if (member.Kind == UmlMemberKind.Field && looksLikeMethod)
            sb.Append("{field} ");
        else if (member.Kind == UmlMemberKind.Method && !looksLikeMethod)
            sb.Append("{method} ");
        sb.Append(member.Visibility switch
        {
            UmlVisibility.Public => "+",
            UmlVisibility.Private => "-",
            UmlVisibility.Protected => "#",
            UmlVisibility.Package => "~",
            _ => "",
        });
        sb.Append(PlantUmlText.Escape(member.Text));
        return sb.ToString();
    }

    private static void WriteRelation(UmlGraphDiagram diagram, UmlRelation relation, Body body)
    {
        string Ref(string id) => diagram.Find(id) switch
        {
            { Kind: UmlElementKind.InitialState or UmlElementKind.FinalState } => "[*]",
            { Kind: UmlElementKind.History } => "[H]",
            { Kind: UmlElementKind.DeepHistory } => "[H*]",
            _ => PlantUmlText.Ref(id),
        };

        var sb = new StringBuilder(Ref(relation.From));
        if (relation.FromLabel is { Length: > 0 } fromLabel)
            sb.Append(' ').Append(PlantUmlText.Quote(fromLabel));
        sb.Append(' ').Append(PlantUmlArrow.Format(relation)).Append(' ');
        if (relation.ToLabel is { Length: > 0 } toLabel)
            sb.Append(PlantUmlText.Quote(toLabel)).Append(' ');
        sb.Append(Ref(relation.To));

        string? label = null;
        if (relation.Messages.Count > 0)
            label = string.Join("\\n", relation.Messages.Select(m => m.Direction == UmlLabelDirection.Backward
                ? "< " + PlantUmlText.Escape(m.Text)
                : PlantUmlText.Escape(m.Text) + " >"));
        else if (relation.Label is { Length: > 0 } text)
            label = relation.LabelDirection switch
            {
                UmlLabelDirection.Forward => PlantUmlText.Escape(text) + " >",
                UmlLabelDirection.Backward => "< " + PlantUmlText.Escape(text),
                _ => PlantUmlText.Escape(text),
            };
        if (label is not null)
            sb.Append(" : ").Append(label);
        body.Line(sb.ToString());

        if (relation.Note is { Length: > 0 } note)
            body.Line(note.Contains('\n') ? $"note on link\n{note}\nend note" : $"note on link : {note}");
    }

    // ---- activity diagrams ----

    private static void WriteStatements(IEnumerable<UmlActivityStatement> statements, Body body)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case UmlActivityStart:
                    body.Line("start");
                    break;
                case UmlActivityStop:
                    body.Line("stop");
                    break;
                case UmlActivityEnd:
                    body.Line("end");
                    break;
                case UmlActivityDetach:
                    body.Line("detach");
                    break;
                case UmlActivityAction action:
                    body.Line($":{action.Text};");
                    break;
                case UmlActivityReference reference:
                    body.Line($":ref {reference.Name};");
                    break;
                case UmlActivityLane lane:
                    body.Line($"|{lane.Name}|");
                    break;
                case UmlActivityArrow arrow:
                    body.Line(arrow.Label is null ? "->" : $"-> {PlantUmlText.Escape(arrow.Label)};");
                    break;
                case UmlActivityNote note:
                    var side = note.Position == UmlNotePosition.Left ? "left" : "right";
                    body.Line(note.Text.Contains('\n') ? $"note {side}\n{note.Text}\nend note" : $"note {side}: {note.Text}");
                    break;
                case UmlActivityIf conditional:
                    for (var i = 0; i < conditional.Branches.Count; i++)
                    {
                        var branch = conditional.Branches[i];
                        var label = branch.Label is null ? "" : $" ({PlantUmlText.Escape(branch.Label)})";
                        if (i == 0)
                            body.Line($"if ({PlantUmlText.Escape(branch.Condition ?? "")}) then{label}");
                        else if (branch.Condition is null)
                            body.Line($"else{label}");
                        else
                            body.Line($"elseif ({PlantUmlText.Escape(branch.Condition)}) then{label}");
                        body.Indent++;
                        WriteStatements(branch.Statements, body);
                        body.Indent--;
                    }
                    body.Line("endif");
                    break;
                case UmlActivityWhile loop:
                    body.Line($"while ({PlantUmlText.Escape(loop.Condition)}){Is(loop.Label)}");
                    body.Indent++;
                    WriteStatements(loop.Body, body);
                    body.Indent--;
                    body.Line("endwhile" + (loop.EndLabel is null ? "" : $" ({PlantUmlText.Escape(loop.EndLabel)})"));
                    break;
                case UmlActivityRepeat repeat:
                    body.Line("repeat");
                    body.Indent++;
                    WriteStatements(repeat.Body, body);
                    body.Indent--;
                    body.Line($"repeat while ({PlantUmlText.Escape(repeat.Condition)}){Is(repeat.Label)}"
                              + (repeat.EndLabel is null ? "" : $" not ({PlantUmlText.Escape(repeat.EndLabel)})"));
                    break;
                case UmlActivityFork fork:
                    var keyword = fork.IsSplit ? "split" : "fork";
                    for (var i = 0; i < fork.Branches.Count; i++)
                    {
                        body.Line(i == 0 ? keyword : keyword + " again");
                        body.Indent++;
                        WriteStatements(fork.Branches[i], body);
                        body.Indent--;
                    }
                    body.Line(fork.IsSplit ? "end split" : fork.IsMerge ? "end merge" : "end fork");
                    break;
            }
        }

        static string Is(string? label) => label is null ? "" : $" is ({PlantUmlText.Escape(label)})";
    }

    // ---- sequence diagrams ----

    private static void WriteSequence(UmlSequenceDiagram diagram, Body body)
    {
        if (diagram.AutoNumber)
            body.Line("autonumber");
        if (!diagram.ShowFootbox)
            body.Line("hide footbox");

        foreach (var p in diagram.Participants)
        {
            var sb = new StringBuilder(p.Kind.ToString().ToLowerInvariant()).Append(' ');
            sb.Append(p.Name == p.Id ? PlantUmlText.Ref(p.Id) : $"{PlantUmlText.Quote(p.Name)} as {PlantUmlText.Ref(p.Id)}");
            foreach (var s in p.Stereotypes)
                sb.Append(" <<").Append(s).Append(">>");
            body.Line(sb.ToString());
        }

        string Ref(string id) => id is UmlSequenceDiagram.LeftBorder or UmlSequenceDiagram.RightBorder ? id : PlantUmlText.Ref(id);

        foreach (var e in diagram.Events)
        {
            switch (e)
            {
                case UmlMessage m:
                    var arrow = m.Kind switch
                    {
                        UmlMessageKind.Asynchronous => "->>",
                        UmlMessageKind.Reply => "-->",
                        _ => "->",
                    };
                    var modifiers = (m.CreateTarget ? "**" : "") + (m.ActivateTarget ? "++" : "")
                                    + (m.DeactivateSource ? "--" : "") + (m.DestroyTarget ? "!!" : "");
                    var line = $"{Ref(m.From)} {arrow} {Ref(m.To)}" + (modifiers.Length > 0 ? " " + modifiers : "");
                    if (m.Text is { Length: > 0 } text)
                        line += " : " + PlantUmlText.Escape(text);
                    body.Line(line);
                    break;
                case UmlActivation a:
                    body.Line($"{a.Kind.ToString().ToLowerInvariant()} {PlantUmlText.Ref(a.Participant)}");
                    break;
                case UmlFragmentStart f:
                    body.Line(f.Kind.ToString().ToLowerInvariant() + (f.Label is null ? "" : " " + PlantUmlText.Escape(f.Label)));
                    body.Indent++;
                    break;
                case UmlFragmentElse f:
                    body.Indent = Math.Max(0, body.Indent - 1);
                    body.Line("else" + (f.Label is null ? "" : " " + PlantUmlText.Escape(f.Label)));
                    body.Indent++;
                    break;
                case UmlFragmentEnd:
                    body.Indent = Math.Max(0, body.Indent - 1);
                    body.Line("end");
                    break;
                case UmlInteractionUse r:
                    var over = string.Join(", ", r.Participants.Select(PlantUmlText.Ref));
                    body.Line(r.Text.Contains('\n') ? $"ref over {over}\n{r.Text}\nend ref" : $"ref over {over} : {r.Text}");
                    break;
                case UmlDivider d:
                    body.Line($"== {d.Text ?? ""} ==");
                    break;
                case UmlDelay d:
                    body.Line(d.Text is null ? "..." : $"...{d.Text}...");
                    break;
                case UmlSpacing s:
                    body.Line(s.Height is { } h ? $"||{h.ToString("0", CultureInfo.InvariantCulture)}||" : "|||");
                    break;
                case UmlSequenceNote n:
                    var target = n.Position switch
                    {
                        UmlNotePosition.Left => "left of ",
                        UmlNotePosition.Right => "right of ",
                        _ => "over ",
                    } + string.Join(", ", n.Participants.Select(PlantUmlText.Ref));
                    body.Line(n.Text.Contains('\n') ? $"note {target}\n{n.Text}\nend note" : $"note {target} : {n.Text}");
                    break;
            }
        }
    }

    // ---- timing diagrams ----

    private static void WriteTiming(UmlTimingDiagram diagram, Body body)
    {
        static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        foreach (var l in diagram.Lifelines)
        {
            var line = $"{l.Kind.ToString().ToLowerInvariant()} {PlantUmlText.Quote(l.Name)} as {PlantUmlText.Ref(l.Id)}";
            if (l.Kind == UmlTimingLifelineKind.Clock)
            {
                line += $" with period {N(l.ClockPeriod)}";
                if (l.ClockPulse > 0)
                    line += $" pulse {N(l.ClockPulse)}";
                if (l.ClockOffset > 0)
                    line += $" offset {N(l.ClockOffset)}";
            }
            body.Line(line);
        }
        foreach (var l in diagram.Lifelines.Where(l => l.States.Count > 0))
            body.Line($"{PlantUmlText.Ref(l.Id)} has {string.Join(",", l.States.Select(PlantUmlText.Ref))}");

        foreach (var group in diagram.Changes.GroupBy(c => c.Time).OrderBy(g => g.Key))
        {
            body.Line("");
            body.Line("@" + N(group.Key));
            foreach (var change in group)
                body.Line($"{PlantUmlText.Ref(change.Lifeline)} is {PlantUmlText.Ref(change.State)}");
        }

        if (diagram.Messages.Count > 0)
            body.Line("");
        foreach (var m in diagram.Messages)
            body.Line($"{PlantUmlText.Ref(m.From)}@{N(m.FromTime)} -> {PlantUmlText.Ref(m.To)}@{N(m.ToTime)}"
                      + (m.Text is null ? "" : " : " + PlantUmlText.Escape(m.Text)));
    }
}
