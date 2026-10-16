using Avalonia;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>
/// Diagram of boxes (<see cref="Elements"/>, nested into containers) and lines between them (<see cref="Relations"/>):
/// class, object, package, component, composite structure, deployment, profile, use case, state and communication
/// diagrams.
/// </summary>
public sealed class UmlGraphDiagram(UmlDiagramKind kind = UmlDiagramKind.Class) : UmlDiagram(kind)
{
    /// <summary>The top level elements; containers hold their content in <see cref="UmlElement.Children"/>.</summary>
    public List<UmlElement> Elements { get; } = [];

    public List<UmlRelation> Relations { get; } = [];

    public override bool SupportsKind(UmlDiagramKind kind) => kind is not (UmlDiagramKind.Activity
        or UmlDiagramKind.InteractionOverview or UmlDiagramKind.Sequence or UmlDiagramKind.Timing);

    /// <summary>Every element, containers before their children.</summary>
    public IEnumerable<UmlElement> AllElements() => Elements.SelectMany(e => e.SelfAndDescendants());

    public UmlElement? Find(string id) => AllElements().FirstOrDefault(e => e.Id == id);

    /// <summary>Adds an element at the top level and returns it, for building diagrams in code.</summary>
    public UmlElement Add(UmlElement element)
    {
        Elements.Add(element);
        return element;
    }

    /// <summary>Adds a relation and returns it, for building diagrams in code.</summary>
    public UmlRelation Connect(UmlRelationKind kind, string from, string to, string? label = null)
    {
        var relation = UmlRelation.Create(kind, from, to);
        relation.Label = label;
        Relations.Add(relation);
        return relation;
    }
}

public enum UmlElementKind
{
    // Classifiers (boxes with compartments)
    Class,
    AbstractClass,
    Interface,
    Enum,
    Annotation,
    Entity,
    Metaclass,
    Stereotype,
    Object,

    // Use cases
    Actor,
    UseCase,

    // Components and deployment
    Component,
    /// <summary>Provided interface drawn as a circle ("lollipop").</summary>
    InterfaceCircle,
    Port,
    PortIn,
    PortOut,
    Node,
    Artifact,
    Database,
    Cloud,
    Folder,
    Frame,
    Package,
    Rectangle,
    Card,
    Queue,
    Storage,
    Stack,
    File,
    Hexagon,
    Collections,
    Agent,
    Person,
    Label,
    Boundary,
    Control,
    EntityIcon,

    // States
    State,
    InitialState,
    FinalState,
    Choice,
    Fork,
    Join,
    History,
    DeepHistory,
    EntryPoint,
    ExitPoint,

    Note,
}

/// <summary>A box of a <see cref="UmlGraphDiagram"/>; elements with <see cref="Children"/> are drawn as containers.</summary>
public sealed class UmlElement(UmlElementKind kind, string id, string? name = null)
{
    public UmlElementKind Kind { get; set; } = kind;

    /// <summary>Identifier the relations refer to; unique in the diagram.</summary>
    public string Id { get; set; } = id;

    /// <summary>Displayed name; the <see cref="Id"/> when not set.</summary>
    public string Name
    {
        get => field ?? Id;
        set => field = value;
    } = name;

    /// <summary>Stereotypes without the guillemets, e.g. <c>entity</c> for «entity».</summary>
    public List<string> Stereotypes { get; } = [];

    /// <summary>Attributes and operations of classifiers, slots of objects, internal activities of states.</summary>
    public List<UmlMember> Members { get; } = [];

    /// <summary>Content of a container (package, node, composite state, system boundary, …) and the ports of a component.</summary>
    public List<UmlElement> Children { get; } = [];

    /// <summary>Text of a note.</summary>
    public string? Text { get; set; }

    /// <summary>Fixed top-left position (relative to the parent container); the automatic layout arranges the rest around it.</summary>
    public Point? Position { get; set; }

    /// <summary>Fixed size instead of the measured one.</summary>
    public Size? Size { get; set; }

    public bool IsPseudoState => Kind is UmlElementKind.InitialState or UmlElementKind.FinalState
        or UmlElementKind.History or UmlElementKind.DeepHistory;

    public bool IsPort => Kind is UmlElementKind.Port or UmlElementKind.PortIn or UmlElementKind.PortOut;

    public UmlElement Add(UmlElement child)
    {
        Children.Add(child);
        return child;
    }

    public UmlElement AddMember(string text, UmlVisibility visibility = UmlVisibility.None)
    {
        Members.Add(new UmlMember(text, visibility));
        return this;
    }

    public IEnumerable<UmlElement> SelfAndDescendants() => Children.SelectMany(c => c.SelfAndDescendants()).Prepend(this);

    public override string ToString() => Name;
}

public enum UmlVisibility
{
    None,
    Public,
    Private,
    Protected,
    Package,
}

public enum UmlMemberKind
{
    /// <summary>Decided by the text: operation when it contains "(", attribute otherwise.</summary>
    Auto,
    Field,
    Method,
    /// <summary>Compartment separator, <see cref="UmlMember.Text"/> is its optional title.</summary>
    Separator,
}

public sealed class UmlMember(string text, UmlVisibility visibility = UmlVisibility.None)
{
    public string Text { get; set; } = text;

    public UmlVisibility Visibility { get; set; } = visibility;

    public UmlMemberKind Kind { get; set; }

    public bool IsStatic { get; set; }

    public bool IsAbstract { get; set; }

    public bool IsMethod => Kind == UmlMemberKind.Method || (Kind == UmlMemberKind.Auto && Text.Contains('('));

    public static UmlMember Separator(string? title = null) => new(title ?? "") { Kind = UmlMemberKind.Separator };

    public override string ToString() => Text;
}
