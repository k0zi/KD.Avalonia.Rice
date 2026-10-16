using System.Text.RegularExpressions;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>
/// Reads the box-and-line diagrams: class, object, package, component, composite structure, deployment, profile,
/// use case, state and communication.
/// </summary>
internal sealed partial class GraphDiagramParser(List<UmlDiagnostic> diagnostics, UmlDiagramKind kind)
    : DiagramParserBase(diagnostics)
{
    private enum RefForm
    {
        Bare,
        Quoted,
        Actor,
        UseCase,
        Component,
        Initial,
        History,
        DeepHistory,
    }

    private readonly record struct RefToken(RefForm Form, string Text, string? Owner = null);

    private sealed record Scope(UmlElement? Container, bool Transparent);

    private readonly UmlGraphDiagram _diagram = new(kind);
    private readonly Dictionary<string, UmlElement> _elements = new(StringComparer.Ordinal);
    private readonly Dictionary<UmlElement, UmlElement?> _parents = [];
    private readonly List<Scope> _scopes = [new(null, false)];
    private UmlElement? _lastElement;
    private UmlRelation? _lastRelation;
    private int _noteCounter;

    private bool IsStructural => kind is UmlDiagramKind.Component or UmlDiagramKind.Deployment
        or UmlDiagramKind.CompositeStructure or UmlDiagramKind.UseCase or UmlDiagramKind.Package;

    /// <summary>The innermost real container (transparent "together" groups are skipped).</summary>
    private UmlElement? Container => _scopes.LastOrDefault(s => !s.Transparent)?.Container;

    public UmlGraphDiagram Parse(PlantUmlBlock block)
    {
        var cursor = new PlantUmlCursor(block.Lines);
        while (cursor.HasMore)
        {
            var line = cursor.Next();
            try
            {
                ParseLine(line, cursor);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
            {
                Error(line, ex.Message);
            }
        }
        if (_scopes.Count > 1)
            Error(cursor.LastNumber, "A '{' block is not closed.");
        return _diagram;
    }

    private void ParseLine(PlantUmlLine line, PlantUmlCursor cursor)
    {
        var text = line.Text;

        if (text.StartsWith('}'))
        {
            if (_scopes.Count > 1)
                _scopes.RemoveAt(_scopes.Count - 1);
            else
                Warn(line, "'}' without an open block.");
            return;
        }

        if (TryCommon(line, cursor, _diagram) || TryNote(line, cursor))
            return;

        if (TogetherRegex().IsMatch(text))
        {
            _scopes.Add(new Scope(Container, true));
            return;
        }

        // Concurrent regions of a composite state: drawn as one region.
        if (text is "--" or "||")
            return;

        var declaration = DeclarationRegex().Match(text);
        if (declaration.Success && TryDeclaration(line, declaration, cursor))
            return;

        if (TryRelation(line) || TryShorthandDeclaration(text) || TryMemberLine(text))
            return;

        Unrecognized(line);
    }

    // ---- declarations ----

    private bool TryDeclaration(PlantUmlLine line, Match match, PlantUmlCursor cursor)
    {
        var keyword = WhitespaceRegex().Replace(match.Groups["kw"].Value.ToLowerInvariant(), " ");
        var rest = match.Groups["rest"].Value.Trim();
        if (rest.Length == 0 && keyword != "together")
            return false;

        var elementKind = KeywordKind(keyword);
        if (elementKind is null)
            return false;

        var opensBlock = false;
        string? inlineBody = null;
        string? description = null;

        // "class A { +x }" on one line, or "package P {" opening a block.
        var brace = IndexOutsideQuotes(rest, '{');
        if (brace >= 0)
        {
            var close = rest.LastIndexOf('}');
            if (close > brace)
                inlineBody = rest[(brace + 1)..close];
            else
                opensBlock = true;
            rest = rest[..brace].Trim();
        }

        // "state S : entry / x", "usecase U : description"
        var colon = IndexOutsideQuotes(rest, ':', skipShorthands: true);
        if (colon >= 0)
        {
            description = rest[(colon + 1)..].Trim();
            rest = rest[..colon].Trim();
        }

        var stereotypes = new List<string>();
        rest = PlantUmlText.TakeStereotypes(rest, stereotypes);
        rest = PlantUmlText.StripDecorations(rest);

        // class A extends B implements C, D
        var inheritance = InheritanceRegex().Match(rest);
        string? extends = null, implements = null;
        if (inheritance.Success)
        {
            extends = inheritance.Groups["ext"].Success ? inheritance.Groups["ext"].Value : null;
            implements = inheritance.Groups["impl"].Success ? inheritance.Groups["impl"].Value : null;
            rest = rest[..inheritance.Index].Trim();
        }

        if (!TryParseName(rest, out var id, out var name))
            return false;

        var resolvedKind = elementKind.Value;
        if (resolvedKind == UmlElementKind.State)
            resolvedKind = StateKind(stereotypes) ?? resolvedKind;
        if (keyword is "struct" or "exception" or "protocol" or "record" or "dataclass" or "device")
            stereotypes.Insert(0, keyword);

        var element = Declare(id, name, resolvedKind);
        foreach (var s in stereotypes.Where(s => !element.Stereotypes.Contains(s) && StateKind([s]) is null))
            element.Stereotypes.Add(s);

        if (description is { Length: > 0 })
        {
            if (resolvedKind == UmlElementKind.Note)
                element.Text = PlantUmlText.Unescape(description);
            else
                element.Members.Add(ParseMember(PlantUmlText.Unescape(description)));
        }

        if (extends is not null)
            foreach (var parent in extends.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                AddRelation(UmlRelation.Create(UmlRelationKind.Generalization, element.Id, Resolve(new RefToken(RefForm.Bare, parent), true).Id));
        if (implements is not null)
            foreach (var parent in implements.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                AddRelation(UmlRelation.Create(UmlRelationKind.Realization, element.Id, Resolve(new RefToken(RefForm.Bare, parent), true, UmlElementKind.Interface).Id));

        if (inlineBody is not null)
        {
            foreach (var memberText in inlineBody.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                element.Members.Add(ParseMember(memberText));
        }
        else if (opensBlock)
        {
            if (IsClassifier(element.Kind))
                ReadMembers(element, cursor, line);
            else
                _scopes.Add(new Scope(element, false));
        }
        return true;
    }

    private UmlElementKind? KeywordKind(string keyword) => keyword switch
    {
        "class" or "struct" or "exception" or "protocol" or "record" or "dataclass" => UmlElementKind.Class,
        "abstract" or "abstract class" => UmlElementKind.AbstractClass,
        "interface" => IsStructural ? UmlElementKind.InterfaceCircle : UmlElementKind.Interface,
        "circle" or "()" => UmlElementKind.InterfaceCircle,
        "enum" => UmlElementKind.Enum,
        "annotation" => UmlElementKind.Annotation,
        "entity" => kind is UmlDiagramKind.Class or UmlDiagramKind.Profile or UmlDiagramKind.Object
            ? UmlElementKind.Entity : UmlElementKind.EntityIcon,
        "metaclass" => UmlElementKind.Metaclass,
        "stereotype" => UmlElementKind.Stereotype,
        "object" or "map" => UmlElementKind.Object,
        "actor" => UmlElementKind.Actor,
        "usecase" => UmlElementKind.UseCase,
        "component" => UmlElementKind.Component,
        "port" => UmlElementKind.Port,
        "portin" => UmlElementKind.PortIn,
        "portout" => UmlElementKind.PortOut,
        "node" or "device" => UmlElementKind.Node,
        "artifact" => UmlElementKind.Artifact,
        "database" => UmlElementKind.Database,
        "cloud" => UmlElementKind.Cloud,
        "folder" => UmlElementKind.Folder,
        "frame" => UmlElementKind.Frame,
        "package" or "namespace" => UmlElementKind.Package,
        "rectangle" => UmlElementKind.Rectangle,
        "card" => UmlElementKind.Card,
        "agent" => UmlElementKind.Agent,
        "queue" => UmlElementKind.Queue,
        "storage" => UmlElementKind.Storage,
        "stack" => UmlElementKind.Stack,
        "file" => UmlElementKind.File,
        "hexagon" => UmlElementKind.Hexagon,
        "collections" => UmlElementKind.Collections,
        "boundary" => UmlElementKind.Boundary,
        "control" => UmlElementKind.Control,
        "person" => UmlElementKind.Person,
        "label" => UmlElementKind.Label,
        "state" => UmlElementKind.State,
        "diamond" => UmlElementKind.Choice,
        _ => null,
    };

    private static UmlElementKind? StateKind(IEnumerable<string> stereotypes)
    {
        foreach (var s in stereotypes)
        {
            switch (s.ToLowerInvariant())
            {
                case "choice":
                    return UmlElementKind.Choice;
                case "fork":
                    return UmlElementKind.Fork;
                case "join":
                    return UmlElementKind.Join;
                case "end":
                    return UmlElementKind.FinalState;
                case "start":
                    return UmlElementKind.InitialState;
                case "history":
                    return UmlElementKind.History;
                case "history*":
                    return UmlElementKind.DeepHistory;
                case "entrypoint":
                    return UmlElementKind.EntryPoint;
                case "exitpoint":
                    return UmlElementKind.ExitPoint;
            }
        }
        return null;
    }

    /// <summary>Lines of a classifier body until the closing brace.</summary>
    private void ReadMembers(UmlElement element, PlantUmlCursor cursor, PlantUmlLine opening)
    {
        while (cursor.HasMore)
        {
            var line = cursor.Next();
            if (line.Text.StartsWith('}'))
                return;
            element.Members.Add(ParseMember(line.Text));
        }
        Error(opening, $"The body of '{element.Name}' is not closed with '}}'.");
    }

    private static UmlMember ParseMember(string text)
    {
        var separator = SeparatorRegex().Match(text);
        if (separator.Success)
            return UmlMember.Separator(separator.Groups[2].Value.Trim() is { Length: > 0 } title ? title : null);

        var member = new UmlMember("");
        // Modifiers ({static}, {abstract}, …) may stand before or after the visibility.
        text = TakeModifiers(text, member);
        if (text.Length > 1 && "+-#~".Contains(text[0]))
        {
            member.Visibility = text[0] switch
            {
                '+' => UmlVisibility.Public,
                '-' => UmlVisibility.Private,
                '#' => UmlVisibility.Protected,
                _ => UmlVisibility.Package,
            };
            text = TakeModifiers(text[1..].TrimStart(), member);
        }
        member.Text = PlantUmlText.Unescape(text);
        return member;
    }

    private static string TakeModifiers(string text, UmlMember member)
    {
        var modifier = ModifierRegex().Match(text);
        while (modifier.Success)
        {
            switch (modifier.Groups[1].Value.ToLowerInvariant())
            {
                case "static" or "classifier":
                    member.IsStatic = true;
                    break;
                case "abstract":
                    member.IsAbstract = true;
                    break;
                case "field":
                    member.Kind = UmlMemberKind.Field;
                    break;
                case "method":
                    member.Kind = UmlMemberKind.Method;
                    break;
            }
            text = text[modifier.Length..].TrimStart();
            modifier = ModifierRegex().Match(text);
        }
        return text;
    }

    private static bool IsClassifier(UmlElementKind kind) => kind is UmlElementKind.Class or UmlElementKind.AbstractClass
        or UmlElementKind.Interface or UmlElementKind.Enum or UmlElementKind.Annotation or UmlElementKind.Entity
        or UmlElementKind.Metaclass or UmlElementKind.Stereotype or UmlElementKind.Object;

    /// <summary>Name part of a declaration: <c>Id</c>, <c>"Name" as Id</c>, <c>Id as "Name"</c>, <c>(Use case) as U</c>, …</summary>
    private static bool TryParseName(string text, out string id, out string name)
    {
        id = name = "";
        var m = NameRegex().Match(text);
        if (!m.Success)
            return false;

        var first = Shorthand(m.Groups["a"].Value);
        if (m.Groups["b"].Success)
        {
            var second = Shorthand(m.Groups["b"].Value);
            // Exactly one of the two is the identifier; the quoted one (or the first) is the display name.
            if (m.Groups["b"].Value.StartsWith('"') && !m.Groups["a"].Value.StartsWith('"'))
                (id, name) = (first, second);
            else
                (id, name) = (second, first);
        }
        else
        {
            id = name = first;
            // class Foo<T>: the identifier is Foo.
            var generic = GenericRegex().Match(first);
            if (generic.Success)
                id = generic.Groups[1].Value;
        }
        return id.Length > 0;
    }

    private static string Shorthand(string token)
    {
        token = token.Trim();
        if (token.Length >= 2 && ((token[0] == '(' && token[^1] == ')') || (token[0] == '[' && token[^1] == ']')
                                  || (token[0] == ':' && token[^1] == ':')))
            token = token[1..^1];
        return PlantUmlText.Unescape(token);
    }

    /// <summary>A line that is only <c>:Actor:</c>, <c>(Use case)</c>, <c>[Component]</c> or <c>() Interface</c> (with alias and stereotypes).</summary>
    private bool TryShorthandDeclaration(string text)
    {
        var stereotypes = new List<string>();
        text = PlantUmlText.StripDecorations(PlantUmlText.TakeStereotypes(text, stereotypes));

        UmlElementKind elementKind;
        if (text.StartsWith("()"))
        {
            elementKind = UmlElementKind.InterfaceCircle;
            text = text[2..].Trim();
        }
        else if (text.Length > 1 && text[0] == ':' && text.IndexOf(':', 1) > 0)
            elementKind = UmlElementKind.Actor;
        else if (text.StartsWith('('))
            elementKind = UmlElementKind.UseCase;
        else if (text.StartsWith('[') && !text.StartsWith("[*]"))
            elementKind = UmlElementKind.Component;
        else
            return false;

        if (!TryParseName(text, out var id, out var name))
            return false;
        var element = Declare(id, name, elementKind);
        element.Stereotypes.AddRange(stereotypes.Where(s => !element.Stereotypes.Contains(s)));
        return true;
    }

    /// <summary><c>Id : text</c> adds a member (attribute, operation, internal activity of a state).</summary>
    private bool TryMemberLine(string text)
    {
        var position = 0;
        if (!TryScanRef(text, ref position, out var token) || token.Form is RefForm.Initial or RefForm.History or RefForm.DeepHistory)
            return false;
        SkipSpaces(text, ref position);
        if (position >= text.Length || text[position] != ':')
            return false;

        var element = Resolve(token, false);
        var memberText = text[(position + 1)..].Trim();
        if (element.Kind == UmlElementKind.Note)
            element.Text = string.IsNullOrEmpty(element.Text) ? memberText : element.Text + "\n" + memberText;
        else if (memberText.Length > 0)
            element.Members.Add(ParseMember(memberText));
        return true;
    }

    private UmlElement Declare(string id, string name, UmlElementKind elementKind)
    {
        if (_elements.TryGetValue(id, out var existing))
        {
            // Declared after first use (auto-created): the declaration decides the kind and the name.
            existing.Kind = elementKind;
            if (name != id)
                existing.Name = name;
            Move(existing, Container);
            _lastElement = existing;
            return existing;
        }

        var element = new UmlElement(elementKind, id, name == id ? null : name);
        Place(element, Container);
        _lastElement = element;
        return element;
    }

    private void Place(UmlElement element, UmlElement? container)
    {
        (container?.Children ?? _diagram.Elements).Add(element);
        _parents[element] = container;
        _elements[element.Id] = element;
    }

    /// <summary>Moves an auto-created element into the container where it is declared.</summary>
    private void Move(UmlElement element, UmlElement? container)
    {
        if (_parents.TryGetValue(element, out var current) && current == container)
            return;
        if (container is not null && element.SelfAndDescendants().Contains(container))
            return;
        (current?.Children ?? _diagram.Elements).Remove(element);
        (container?.Children ?? _diagram.Elements).Add(element);
        _parents[element] = container;
    }

    // ---- notes ----

    private bool TryNote(PlantUmlLine line, PlantUmlCursor cursor)
    {
        var text = line.Text;
        if (!text.StartsWith("note", StringComparison.OrdinalIgnoreCase)
            && !text.StartsWith("hnote", StringComparison.OrdinalIgnoreCase)
            && !text.StartsWith("rnote", StringComparison.OrdinalIgnoreCase))
            return false;

        string ReadText(string? inline)
        {
            if (inline is not null)
                return PlantUmlText.Unescape(inline);
            var lines = cursor.ReadUntil(EndNoteRegex(), out var found);
            if (!found)
                Error(line, "'end note' is missing.");
            return string.Join("\n", lines);
        }

        var onLink = NoteOnLinkRegex().Match(text);
        if (onLink.Success)
        {
            var noteText = ReadText(onLink.Groups["text"].Success ? onLink.Groups["text"].Value : null);
            if (_lastRelation is null)
                Warn(line, "'note on link' without a link before it.");
            else
                _lastRelation.Note = noteText;
            return true;
        }

        var floating = NoteAsRegex().Match(text);
        if (floating.Success)
        {
            var quoted = floating.Groups["quoted"];
            var noteText = ReadText(quoted.Success ? quoted.Value : null);
            var id = PlantUmlText.Unescape(floating.Groups["id"].Value);
            Declare(id, id, UmlElementKind.Note).Text = noteText;
            return true;
        }

        var quotedAs = NoteQuotedAsRegex().Match(text);
        if (quotedAs.Success)
        {
            var id = PlantUmlText.Unescape(quotedAs.Groups["id"].Value);
            Declare(id, id, UmlElementKind.Note).Text = PlantUmlText.Unescape(quotedAs.Groups["quoted"].Value);
            return true;
        }

        var attached = NoteOfRegex().Match(text);
        if (!attached.Success)
            return false;

        var direction = attached.Groups["pos"].Value.ToLowerInvariant() switch
        {
            "left" => UmlLinkDirection.Left,
            "right" => UmlLinkDirection.Right,
            "top" => UmlLinkDirection.Up,
            _ => UmlLinkDirection.Down,
        };

        UmlElement? target = _lastElement;
        var rest = attached.Groups["rest"].Value.Trim();
        if (rest.StartsWith("of ", StringComparison.OrdinalIgnoreCase))
        {
            rest = rest[3..].TrimStart();
            var position = 0;
            if (!TryScanRef(rest, ref position, out var token))
                return false;
            target = Resolve(token, false);
            rest = rest[position..].Trim();
        }
        var stereotypes = new List<string>();
        rest = PlantUmlText.StripDecorations(PlantUmlText.TakeStereotypes(rest, stereotypes));
        string? inline = null;
        if (rest.StartsWith(':'))
            inline = rest[1..].Trim();
        else if (rest.Length > 0)
            return false;

        var note = new UmlElement(UmlElementKind.Note, $"__note{++_noteCounter}") { Text = ReadText(inline) };
        Place(note, target is not null && _parents.TryGetValue(target, out var parent) ? parent : Container);
        if (target is null)
        {
            Warn(line, "The note has no element to attach to.");
            return true;
        }
        AddRelation(new UmlRelation(target.Id, note.Id) { LineStyle = UmlLineStyle.Dashed, Direction = direction });
        return true;
    }

    // ---- relations ----

    private bool TryRelation(PlantUmlLine line)
    {
        var text = line.Text;
        var position = 0;
        if (!TryScanRef(text, ref position, out var left))
            return false;
        SkipSpaces(text, ref position);

        string? fromLabel = null;
        if (position < text.Length && text[position] == '"')
        {
            if (!TryScanQuoted(text, ref position, out var quoted))
                return false;
            fromLabel = quoted;
            SkipSpaces(text, ref position);
        }

        if (!PlantUmlArrow.TryParse(text, position, out var arrow, out position))
            return false;
        SkipSpaces(text, ref position);

        string? toLabel = null;
        RefToken right;
        if (position < text.Length && text[position] == '"')
        {
            var afterQuote = position;
            if (!TryScanQuoted(text, ref afterQuote, out var quoted))
                return false;
            var afterSpaces = afterQuote;
            SkipSpaces(text, ref afterSpaces);
            if (afterSpaces > afterQuote && TryScanRef(text, ref afterSpaces, out right))
            {
                toLabel = quoted;
                position = afterSpaces;
            }
            else
            {
                right = new RefToken(RefForm.Quoted, quoted);
                position = afterQuote;
            }
        }
        else if (!TryScanRef(text, ref position, out right))
            return false;

        SkipSpaces(text, ref position);
        string? label = null;
        if (position < text.Length)
        {
            if (text[position] != ':')
            {
                // Trailing decorations (#color) are allowed, anything else means this is not a relation.
                if (PlantUmlText.StripDecorations(text[position..]).Length > 0)
                    return false;
            }
            else
                label = text[(position + 1)..].Trim();
        }

        var from = Resolve(left, true, isTarget: false);
        var to = Resolve(right, true, isTarget: true);
        var relation = new UmlRelation(from.Id, to.Id)
        {
            FromHead = arrow.FromHead,
            ToHead = arrow.ToHead,
            LineStyle = arrow.Style,
            Direction = arrow.Direction,
            Length = arrow.Length,
            FromLabel = fromLabel is null ? null : PlantUmlText.Unescape(fromLabel),
            ToLabel = toLabel is null ? null : PlantUmlText.Unescape(toLabel),
        };
        if (label is { Length: > 0 })
            SetLabel(relation, label);
        AddRelation(relation);
        return true;
    }

    private void SetLabel(UmlRelation relation, string label)
    {
        if (kind == UmlDiagramKind.Communication)
        {
            foreach (var part in PlantUmlText.Unescape(label).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var (message, direction) = SplitDirection(part);
                relation.Messages.Add(new UmlLinkMessage(message, direction == UmlLabelDirection.None ? UmlLabelDirection.Forward : direction));
            }
            return;
        }
        var (text, labelDirection) = SplitDirection(label);
        relation.Label = PlantUmlText.Unescape(text);
        relation.LabelDirection = labelDirection;
    }

    /// <summary>PlantUML draws a small arrow for "label &gt;" and "&lt; label" (but "&lt;&lt;include&gt;&gt;" is a stereotype).</summary>
    private static (string Text, UmlLabelDirection Direction) SplitDirection(string label)
    {
        label = label.Trim();
        if (label.Length > 1 && label[^1] == '>' && label[^2] != '>' && label[^2] != '-')
            return (label[..^1].Trim(), UmlLabelDirection.Forward);
        if (label.Length > 1 && label[0] == '<' && label[1] != '<')
            return (label[1..].Trim(), UmlLabelDirection.Backward);
        return (label, UmlLabelDirection.None);
    }

    private void AddRelation(UmlRelation relation)
    {
        _diagram.Relations.Add(relation);
        _lastRelation = relation;
    }

    /// <summary>The element a reference points to; created on first use, like PlantUML does.</summary>
    private UmlElement Resolve(RefToken token, bool create, UmlElementKind? defaultKind = null, bool isTarget = false)
    {
        switch (token.Form)
        {
            case RefForm.Initial:
                return Pseudo(Container, isTarget ? UmlElementKind.FinalState : UmlElementKind.InitialState);
            case RefForm.History or RefForm.DeepHistory:
            {
                var owner = token.Owner is null ? Container : Resolve(new RefToken(RefForm.Bare, token.Owner), true);
                return Pseudo(owner, token.Form == RefForm.History ? UmlElementKind.History : UmlElementKind.DeepHistory);
            }
        }

        if (_elements.TryGetValue(token.Text, out var element))
            return element;
        // "(Login)" also finds 'usecase "Login" as UC1'.
        element = _elements.Values.FirstOrDefault(e => e.Name == token.Text && e.Kind != UmlElementKind.Note);
        if (element is not null)
            return element;

        var elementKind = token.Form switch
        {
            RefForm.Actor => UmlElementKind.Actor,
            RefForm.UseCase => UmlElementKind.UseCase,
            RefForm.Component => UmlElementKind.Component,
            _ => defaultKind ?? DefaultKind(),
        };
        element = new UmlElement(elementKind, token.Text);
        Place(element, Container);
        return element;
    }

    private UmlElementKind DefaultKind() => kind switch
    {
        UmlDiagramKind.Object or UmlDiagramKind.Communication => UmlElementKind.Object,
        UmlDiagramKind.UseCase => UmlElementKind.Actor,
        UmlDiagramKind.Component or UmlDiagramKind.CompositeStructure => UmlElementKind.Component,
        UmlDiagramKind.Deployment => UmlElementKind.Node,
        UmlDiagramKind.Package => UmlElementKind.Package,
        UmlDiagramKind.State => UmlElementKind.State,
        _ => UmlElementKind.Class,
    };

    /// <summary>[*] and [H]: one of each per container.</summary>
    private UmlElement Pseudo(UmlElement? owner, UmlElementKind pseudoKind)
    {
        var suffix = pseudoKind switch
        {
            UmlElementKind.InitialState => "[*]start",
            UmlElementKind.FinalState => "[*]end",
            UmlElementKind.History => "[H]",
            _ => "[H*]",
        };
        var id = (owner?.Id ?? "") + suffix;
        if (_elements.TryGetValue(id, out var element))
            return element;
        element = new UmlElement(pseudoKind, id, "");
        Place(element, owner);
        return element;
    }

    // ---- scanning ----

    private static bool TryScanRef(string text, ref int position, out RefToken token)
    {
        token = default;
        if (position >= text.Length)
            return false;

        var start = position;
        var c = text[position];
        if (string.CompareOrdinal(text, position, "[*]", 0, 3) == 0)
        {
            position += 3;
            token = new RefToken(RefForm.Initial, "[*]");
            return true;
        }
        if (string.Compare(text, position, "[H*]", 0, 4, StringComparison.OrdinalIgnoreCase) == 0)
        {
            position += 4;
            token = new RefToken(RefForm.DeepHistory, "[H*]");
            return true;
        }
        if (string.Compare(text, position, "[H]", 0, 3, StringComparison.OrdinalIgnoreCase) == 0)
        {
            position += 3;
            token = new RefToken(RefForm.History, "[H]");
            return true;
        }

        switch (c)
        {
            case '"':
                if (!TryScanQuoted(text, ref position, out var quoted))
                    return false;
                token = new RefToken(RefForm.Quoted, PlantUmlText.Unescape(quoted));
                return true;
            case ':':
            {
                var end = text.IndexOf(':', position + 1);
                if (end <= position + 1)
                    return false;
                token = new RefToken(RefForm.Actor, PlantUmlText.Unescape(text[(position + 1)..end]));
                position = end + 1;
                return true;
            }
            case '(':
            {
                var end = text.IndexOf(')', position + 1);
                if (end <= position + 1)
                    return false;
                token = new RefToken(RefForm.UseCase, PlantUmlText.Unescape(text[(position + 1)..end]));
                position = end + 1;
                return true;
            }
            case '[':
            {
                var end = text.IndexOf(']', position + 1);
                if (end <= position + 1)
                    return false;
                token = new RefToken(RefForm.Component, PlantUmlText.Unescape(text[(position + 1)..end]));
                position = end + 1;
                return true;
            }
        }

        while (position < text.Length && IsIdChar(text, position))
            position++;
        if (position == start)
            return false;

        var id = text[start..position];
        // State[H] / State[H*]: history of that composite state.
        if (string.Compare(text, position, "[H*]", 0, 4, StringComparison.OrdinalIgnoreCase) == 0)
        {
            position += 4;
            token = new RefToken(RefForm.DeepHistory, "[H*]", id);
            return true;
        }
        if (string.Compare(text, position, "[H]", 0, 3, StringComparison.OrdinalIgnoreCase) == 0)
        {
            position += 3;
            token = new RefToken(RefForm.History, "[H]", id);
            return true;
        }
        token = new RefToken(RefForm.Bare, id);
        return true;
    }

    // A dot belongs to the name only between name characters (a.b.C), so "A..>B" stays a dashed arrow.
    private static bool IsIdChar(string text, int i)
    {
        var c = text[i];
        if (char.IsLetterOrDigit(c) || c == '_')
            return true;
        return c == '.' && i > 0 && i + 1 < text.Length && char.IsLetterOrDigit(text[i - 1])
               && (char.IsLetterOrDigit(text[i + 1]) || text[i + 1] == '_');
    }

    private static bool TryScanQuoted(string text, ref int position, out string value)
    {
        value = "";
        var end = text.IndexOf('"', position + 1);
        if (end < 0)
            return false;
        value = text[(position + 1)..end];
        position = end + 1;
        return true;
    }

    private static void SkipSpaces(string text, ref int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position]))
            position++;
    }

    private static int IndexOutsideQuotes(string text, char target, bool skipShorthands = false)
    {
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
                quoted = !quoted;
            else if (!quoted && skipShorthands && c == ':' && i == 0)
            {
                // ":Actor:" at the start is a name, not a description.
                var close = text.IndexOf(':', 1);
                if (close > 0)
                    i = close;
            }
            else if (!quoted && c == '<' && i + 1 < text.Length && text[i + 1] == '<')
            {
                var close = text.IndexOf(">>", i, StringComparison.Ordinal);
                if (close > 0)
                    i = close + 1;
            }
            else if (!quoted && c == target)
                return i;
        }
        return -1;
    }

    [GeneratedRegex(@"^(?<kw>abstract\s+class|abstract|class|interface|enum|annotation|entity|struct|exception|protocol|record|dataclass|metaclass|stereotype|object|map|actor|usecase|component|portin|portout|port|node|device|artifact|database|cloud|folder|frame|package|namespace|rectangle|card|agent|queue|storage|stack|file|hexagon|collections|boundary|control|person|label|state|circle|diamond)(?=\s|$)(?<rest>.*)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex DeclarationRegex();

    [GeneratedRegex(@"^together\s*\{$", RegexOptions.IgnoreCase)]
    private static partial Regex TogetherRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    // Only matches when at least one of the two keywords is there.
    [GeneratedRegex(@"\s+(?=extends\s|implements\s)(?:extends\s+(?<ext>.+?))?\s*(?:implements\s+(?<impl>.+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex InheritanceRegex();

    [GeneratedRegex(@"^(?<a>""[^""]*""|\([^)]*\)|\[[^\]]*\]|:[^:]+:|\S+)(?:\s+as\s+(?<b>""[^""]*""|\([^)]*\)|\[[^\]]*\]|:[^:]+:|\S+))?$",
        RegexOptions.IgnoreCase)]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"^([\p{L}_][\p{L}\p{N}_.]*)\s*<.+>$")]
    private static partial Regex GenericRegex();

    [GeneratedRegex(@"^(--|\.\.|==|__)(.*?)(--|\.\.|==|__)?$")]
    private static partial Regex SeparatorRegex();

    [GeneratedRegex(@"^\{(static|classifier|abstract|field|method)\}\s*", RegexOptions.IgnoreCase)]
    private static partial Regex ModifierRegex();

    [GeneratedRegex(@"^end\s*note$", RegexOptions.IgnoreCase)]
    private static partial Regex EndNoteRegex();

    [GeneratedRegex(@"^[hr]?note\s+on\s+link\b[^:]*(?::\s*(?<text>.*))?$", RegexOptions.IgnoreCase)]
    private static partial Regex NoteOnLinkRegex();

    [GeneratedRegex(@"^[hr]?note\s+(?:""(?<quoted>[^""]*)""\s+)?as\s+(?<id>\S+)\s*(?:<<.*>>)?\s*(?:#\S+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex NoteAsRegex();

    [GeneratedRegex(@"^[hr]?note\s+""(?<quoted>[^""]*)""\s+as\s+(?<id>\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex NoteQuotedAsRegex();

    [GeneratedRegex(@"^[hr]?note\s+(?<pos>left|right|top|bottom)\b(?<rest>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex NoteOfRegex();
}
