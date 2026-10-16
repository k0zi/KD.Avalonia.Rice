namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>
/// Activity diagram as a structured flow (the "new" PlantUML activity syntax): actions, decisions, loops, forks
/// and swimlanes in <see cref="Statements"/>. With <see cref="UmlDiagramKind.InteractionOverview"/> the steps are
/// mostly <see cref="UmlActivityReference"/>s to other interactions.
/// </summary>
public sealed class UmlActivityDiagram(UmlDiagramKind kind = UmlDiagramKind.Activity) : UmlDiagram(kind)
{
    public List<UmlActivityStatement> Statements { get; } = [];

    public override bool SupportsKind(UmlDiagramKind kind) =>
        kind is UmlDiagramKind.Activity or UmlDiagramKind.InteractionOverview;
}

public abstract class UmlActivityStatement;

/// <summary>Initial node (<c>start</c>).</summary>
public sealed class UmlActivityStart : UmlActivityStatement;

/// <summary>Activity final node (<c>stop</c>).</summary>
public sealed class UmlActivityStop : UmlActivityStatement;

/// <summary>Flow final node (<c>end</c>).</summary>
public sealed class UmlActivityEnd : UmlActivityStatement;

/// <summary>The flow stops without a node (<c>kill</c> / <c>detach</c>).</summary>
public sealed class UmlActivityDetach : UmlActivityStatement;

public sealed class UmlActivityAction(string text) : UmlActivityStatement
{
    public string Text { get; set; } = text;
}

/// <summary>Interaction occurrence of an interaction overview diagram (<c>:ref Name;</c>).</summary>
public sealed class UmlActivityReference(string name) : UmlActivityStatement
{
    public string Name { get; set; } = name;
}

public sealed class UmlActivityBranch(string? condition = null, string? label = null)
{
    /// <summary>Condition of <c>if</c> / <c>elseif</c>; null for <c>else</c>.</summary>
    public string? Condition { get; set; } = condition;

    /// <summary>Text on the outgoing line, e.g. "yes".</summary>
    public string? Label { get; set; } = label;

    public List<UmlActivityStatement> Statements { get; } = [];
}

/// <summary><c>if</c> / <c>elseif</c> / <c>else</c> / <c>endif</c>; the last branch is the else when its condition is null.</summary>
public sealed class UmlActivityIf : UmlActivityStatement
{
    public List<UmlActivityBranch> Branches { get; } = [];
}

/// <summary><c>while (condition) is (label)</c> … <c>endwhile (endLabel)</c>.</summary>
public sealed class UmlActivityWhile(string condition) : UmlActivityStatement
{
    public string Condition { get; set; } = condition;

    public string? Label { get; set; }

    public string? EndLabel { get; set; }

    public List<UmlActivityStatement> Body { get; } = [];
}

/// <summary><c>repeat</c> … <c>repeat while (condition) is (label) not (endLabel)</c>.</summary>
public sealed class UmlActivityRepeat(string condition) : UmlActivityStatement
{
    public string Condition { get; set; } = condition;

    public string? Label { get; set; }

    public string? EndLabel { get; set; }

    public List<UmlActivityStatement> Body { get; } = [];
}

/// <summary>
/// Parallel branches: <c>fork</c> / <c>fork again</c> / <c>end fork</c> with bars, or <c>split</c> / <c>split again</c>
/// / <c>end split</c> without.
/// </summary>
public sealed class UmlActivityFork(bool isSplit = false) : UmlActivityStatement
{
    public bool IsSplit { get; set; } = isSplit;

    /// <summary>The branches join with a merge instead of a join bar (<c>end merge</c>).</summary>
    public bool IsMerge { get; set; }

    public List<List<UmlActivityStatement>> Branches { get; } = [];
}

/// <summary>The following steps belong to this swimlane (<c>|Name|</c>).</summary>
public sealed class UmlActivityLane(string name) : UmlActivityStatement
{
    public string Name { get; set; } = name;
}

/// <summary>Text on the next line (<c>-&gt; text;</c>).</summary>
public sealed class UmlActivityArrow(string? label) : UmlActivityStatement
{
    public string? Label { get; set; } = label;
}

/// <summary>Note next to the previous step.</summary>
public sealed class UmlActivityNote(string text, UmlNotePosition position = UmlNotePosition.Right) : UmlActivityStatement
{
    public string Text { get; set; } = text;

    public UmlNotePosition Position { get; set; } = position;
}
