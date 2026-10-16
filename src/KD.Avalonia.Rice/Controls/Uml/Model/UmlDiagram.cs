namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>The 14 diagram types of UML 2.</summary>
public enum UmlDiagramKind
{
    // Structure
    Class,
    Object,
    Package,
    Component,
    CompositeStructure,
    Deployment,
    Profile,

    // Behavior
    UseCase,
    Activity,
    State,

    // Interaction
    Sequence,
    Communication,
    Timing,
    InteractionOverview,
}

/// <summary>Main flow direction of the automatic layout.</summary>
public enum UmlDirection
{
    TopToBottom,
    LeftToRight,
}

/// <summary>
/// Common part of every diagram. The concrete model depends on the kind: <see cref="UmlGraphDiagram"/> (boxes and
/// lines), <see cref="UmlActivityDiagram"/>, <see cref="UmlSequenceDiagram"/> and <see cref="UmlTimingDiagram"/>.
/// </summary>
public abstract class UmlDiagram
{
    private UmlDiagramKind _kind;

    protected UmlDiagram(UmlDiagramKind kind) => Kind = kind;

    /// <summary>The diagram type; it has to be one the model class supports (see <see cref="SupportsKind"/>).</summary>
    public UmlDiagramKind Kind
    {
        get => _kind;
        set
        {
            if (!SupportsKind(value))
                throw new ArgumentException($"{GetType().Name} cannot hold a {value} diagram.", nameof(value));
            _kind = value;
        }
    }

    /// <summary>Name after <c>@startuml</c>, PlantUML uses it as the default file name of exported images.</summary>
    public string? Name { get; set; }

    public string? Title { get; set; }

    public UmlDirection Direction { get; set; }

    public abstract bool SupportsKind(UmlDiagramKind kind);

    /// <summary>The diagram as PlantUML text (<c>@startuml</c> … <c>@enduml</c>).</summary>
    public string ToPlantUml() => PlantUmlWriter.Write(this);

    public override string ToString() => Title ?? Name ?? Kind.ToString();
}
