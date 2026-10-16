using KD.Avalonia.Rice.Controls.Uml;

namespace KD.Avalonia.Rice.Controls.Markdowns;

public enum MermaidDiagramKind
{
    Unknown,
    Flowchart,
    Sequence,
    Class,
    State,
    EntityRelationship,
    Pie,
    Gantt,
    Mindmap,
}

/// <summary>A problem found while reading Mermaid text; <see cref="Line"/> is 1-based.</summary>
public sealed record MermaidDiagnostic(int Line, string Message)
{
    public override string ToString() => $"{Line}: {Message}";
}

/// <summary>
/// Mermaid text (the content of a <c>```mermaid</c> block or an <c>.mmd</c> file) read into a diagram. Sequence,
/// class and state diagrams become the UML models of <see cref="Uml"/> (<see cref="UmlSequenceDiagram"/>,
/// <see cref="UmlGraphDiagram"/>); the others are <see cref="MermaidDiagram"/>s. Reading never throws.
/// </summary>
public sealed class MermaidDocument
{
    internal MermaidDocument(MermaidDiagramKind kind, object? diagram, List<MermaidDiagnostic> diagnostics)
    {
        Kind = kind;
        Diagram = diagram;
        Diagnostics = diagnostics;
    }

    public MermaidDiagramKind Kind { get; }

    /// <summary>A <see cref="UmlDiagram"/> or a <see cref="MermaidDiagram"/>; null when the text is not a supported diagram.</summary>
    public object? Diagram { get; }

    public IReadOnlyList<MermaidDiagnostic> Diagnostics { get; }

    public static MermaidDocument Parse(string? text) => MermaidParser.Parse(text ?? "");

    public static MermaidDocument Load(string path) => Parse(File.ReadAllText(path));
}

/// <summary>A Mermaid diagram that has no UML model: flowchart, entity relationship, pie, gantt, mind map.</summary>
public abstract class MermaidDiagram : IUmlSceneSource
{
    public string? Title { get; set; }

    public abstract MermaidDiagramKind Kind { get; }

    UmlScene IUmlSceneSource.Build(UmlStyle style) => MermaidSceneBuilder.Build(this, style);
}

public enum MermaidDirection
{
    TopToBottom,
    BottomToTop,
    LeftToRight,
    RightToLeft,
}

// ---- flowchart ----

public enum MermaidNodeShape
{
    Rectangle,
    Rounded,
    Stadium,
    Subroutine,
    Cylinder,
    Circle,
    DoubleCircle,
    Diamond,
    Hexagon,
    Parallelogram,
    ParallelogramAlt,
    Trapezoid,
    TrapezoidAlt,
    Asymmetric,
}

public sealed class MermaidNode(string id, string? text = null, MermaidNodeShape shape = MermaidNodeShape.Rectangle)
{
    public string Id { get; } = id;

    public string Text { get; set; } = text ?? id;

    public MermaidNodeShape Shape { get; set; } = shape;

    public override string ToString() => Text;
}

public enum MermaidLineStyle
{
    Solid,
    Dotted,
    Thick,
    Invisible,
}

public enum MermaidEdgeEnd
{
    None,
    Arrow,
    Circle,
    Cross,
}

public sealed class MermaidEdge(string from, string to)
{
    public string From { get; } = from;

    public string To { get; } = to;

    public string? Text { get; set; }

    public MermaidLineStyle Line { get; set; }

    public MermaidEdgeEnd FromEnd { get; set; }

    public MermaidEdgeEnd ToEnd { get; set; } = MermaidEdgeEnd.Arrow;

    /// <summary>Number of ranks the edge spans at least (<c>---&gt;</c> is 2).</summary>
    public int Length { get; set; } = 1;

    public override string ToString() => $"{From} → {To}";
}

public sealed class MermaidSubgraph(string id, string? title = null)
{
    public string Id { get; } = id;

    public string Title { get; set; } = title ?? id;

    public MermaidDirection? Direction { get; set; }

    /// <summary>Ids of the nodes directly in this subgraph.</summary>
    public List<string> Nodes { get; } = [];

    public List<MermaidSubgraph> Subgraphs { get; } = [];

    public override string ToString() => Title;
}

public sealed class MermaidFlowchart : MermaidDiagram
{
    public override MermaidDiagramKind Kind => MermaidDiagramKind.Flowchart;

    public MermaidDirection Direction { get; set; }

    public List<MermaidNode> Nodes { get; } = [];

    public List<MermaidEdge> Edges { get; } = [];

    /// <summary>Top level subgraphs; nested ones are in <see cref="MermaidSubgraph.Subgraphs"/>.</summary>
    public List<MermaidSubgraph> Subgraphs { get; } = [];

    public MermaidNode? Find(string id) => Nodes.FirstOrDefault(n => n.Id == id);
}

// ---- entity relationship ----

public sealed class MermaidAttribute(string type, string name)
{
    public string Type { get; set; } = type;

    public string Name { get; set; } = name;

    /// <summary>PK, FK, UK (comma separated in the source).</summary>
    public List<string> Keys { get; } = [];

    public string? Comment { get; set; }
}

public sealed class MermaidEntity(string id, string? label = null)
{
    public string Id { get; } = id;

    public string Label { get; set; } = label ?? id;

    public List<MermaidAttribute> Attributes { get; } = [];

    public override string ToString() => Label;
}

public enum MermaidCardinality
{
    ZeroOrOne,
    ExactlyOne,
    ZeroOrMore,
    OneOrMore,
}

public sealed class MermaidRelationship(string from, string to)
{
    public string From { get; } = from;

    public string To { get; } = to;

    public MermaidCardinality FromCardinality { get; set; } = MermaidCardinality.ExactlyOne;

    public MermaidCardinality ToCardinality { get; set; } = MermaidCardinality.ExactlyOne;

    /// <summary>Solid line (<c>--</c>); a dashed line (<c>..</c>) is non-identifying.</summary>
    public bool IsIdentifying { get; set; } = true;

    public string? Label { get; set; }

    public override string ToString() => $"{From} → {To}";
}

public sealed class MermaidErDiagram : MermaidDiagram
{
    public override MermaidDiagramKind Kind => MermaidDiagramKind.EntityRelationship;

    public MermaidDirection Direction { get; set; }

    public List<MermaidEntity> Entities { get; } = [];

    public List<MermaidRelationship> Relationships { get; } = [];

    public MermaidEntity GetOrAdd(string id)
    {
        var entity = Entities.FirstOrDefault(e => e.Id == id);
        if (entity is null)
        {
            entity = new MermaidEntity(id);
            Entities.Add(entity);
        }
        return entity;
    }
}

// ---- pie ----

public sealed record MermaidPieSlice(string Label, double Value)
{
    public override string ToString() => Label;
}

public sealed class MermaidPie : MermaidDiagram
{
    public override MermaidDiagramKind Kind => MermaidDiagramKind.Pie;

    /// <summary>Shows the values next to the labels of the legend.</summary>
    public bool ShowData { get; set; }

    public List<MermaidPieSlice> Slices { get; } = [];
}

// ---- gantt ----

public sealed class MermaidGanttTask(string name)
{
    public string Name { get; set; } = name;

    public string? Id { get; set; }

    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public bool IsDone { get; set; }

    public bool IsActive { get; set; }

    public bool IsCritical { get; set; }

    public bool IsMilestone { get; set; }

    public override string ToString() => Name;
}

public sealed class MermaidGanttSection(string name)
{
    public string Name { get; set; } = name;

    public List<MermaidGanttTask> Tasks { get; } = [];

    public override string ToString() => Name;
}

public sealed class MermaidGantt : MermaidDiagram
{
    public override MermaidDiagramKind Kind => MermaidDiagramKind.Gantt;

    /// <summary>.NET format of the time axis labels; null chooses one by the time span.</summary>
    public string? AxisFormat { get; set; }

    public List<MermaidGanttSection> Sections { get; } = [];
}

// ---- mind map ----

public enum MermaidMindmapShape
{
    Default,
    Square,
    Rounded,
    Circle,
    Bang,
    Cloud,
    Hexagon,
}

public sealed class MermaidMindmapNode(string text, MermaidMindmapShape shape = MermaidMindmapShape.Default)
{
    public string Text { get; set; } = text;

    public MermaidMindmapShape Shape { get; set; } = shape;

    public List<MermaidMindmapNode> Children { get; } = [];

    public override string ToString() => Text;
}

public sealed class MermaidMindmap : MermaidDiagram
{
    public override MermaidDiagramKind Kind => MermaidDiagramKind.Mindmap;

    public MermaidMindmapNode? Root { get; set; }
}
