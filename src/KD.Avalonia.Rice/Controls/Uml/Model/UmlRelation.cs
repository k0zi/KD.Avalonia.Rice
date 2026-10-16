namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>Symbol at one end of a line.</summary>
public enum UmlArrowHead
{
    None,
    /// <summary>Open arrow (navigability, dependency, transition, message).</summary>
    Arrow,
    /// <summary>Hollow triangle (generalization, realization).</summary>
    Triangle,
    /// <summary>Filled triangle (extension in profiles).</summary>
    FilledTriangle,
    /// <summary>Hollow diamond (aggregation).</summary>
    Diamond,
    /// <summary>Filled diamond (composition).</summary>
    FilledDiamond,
    /// <summary>Circle with a cross (nesting, containment).</summary>
    Plus,
    /// <summary>Not navigable end.</summary>
    Cross,
    Square,
    /// <summary>Provided interface ("lollipop") end.</summary>
    Circle,
    /// <summary>Required interface ("socket") end.</summary>
    Socket,
}

public enum UmlLineStyle
{
    Solid,
    Dashed,
    Dotted,
    Bold,
    /// <summary>Not drawn, only influences the layout (PlantUML <c>-[hidden]-</c>).</summary>
    Hidden,
}

/// <summary>Layout hint of a relation: where <c>To</c> goes relative to <c>From</c> (PlantUML <c>-up-&gt;</c>, …).</summary>
public enum UmlLinkDirection
{
    Auto,
    Up,
    Down,
    Left,
    Right,
}

/// <summary>Semantic kind of a relation, derived from its line and arrow heads.</summary>
public enum UmlRelationKind
{
    Association,
    DirectedAssociation,
    Aggregation,
    Composition,
    Generalization,
    Realization,
    Dependency,
    Include,
    Extend,
    Nesting,
    /// <summary>Plain dashed line, e.g. between a note and its element.</summary>
    Link,
    /// <summary>Transition (state diagram), flow (activity) or message (communication): a solid line with an arrow.</summary>
    Transition,
}

/// <summary>Direction of a label or a communication message relative to the line.</summary>
public enum UmlLabelDirection
{
    None,
    /// <summary>From <c>From</c> to <c>To</c>.</summary>
    Forward,
    Backward,
}

/// <summary>A message of a communication diagram, drawn along its link, e.g. "1: login()".</summary>
public sealed record UmlLinkMessage(string Text, UmlLabelDirection Direction = UmlLabelDirection.Forward);

/// <summary>A line between two elements of a <see cref="UmlGraphDiagram"/>; the ends refer to <see cref="UmlElement.Id"/>s.</summary>
public sealed class UmlRelation(string from, string to)
{
    public string From { get; set; } = from;

    public string To { get; set; } = to;

    public UmlArrowHead FromHead { get; set; }

    public UmlArrowHead ToHead { get; set; }

    public UmlLineStyle LineStyle { get; set; }

    public string? Label { get; set; }

    public UmlLabelDirection LabelDirection { get; set; }

    /// <summary>Multiplicity or role at the <c>From</c> end, e.g. "1".</summary>
    public string? FromLabel { get; set; }

    /// <summary>Multiplicity or role at the <c>To</c> end, e.g. "0..*".</summary>
    public string? ToLabel { get; set; }

    public UmlLinkDirection Direction { get; set; }

    /// <summary>
    /// Line length in PlantUML dashes: 1 places both ends side by side, 2 (default) one rank apart, more stretches it.
    /// </summary>
    public int Length { get; set; } = 2;

    /// <summary>Note attached to the line (PlantUML <c>note on link</c>).</summary>
    public string? Note { get; set; }

    /// <summary>Messages of a communication diagram.</summary>
    public List<UmlLinkMessage> Messages { get; } = [];

    public bool IsDashed => LineStyle is UmlLineStyle.Dashed or UmlLineStyle.Dotted;

    public UmlRelationKind Kind
    {
        get
        {
            var heads = new[] { FromHead, ToHead };
            if (heads.Contains(UmlArrowHead.Triangle) || heads.Contains(UmlArrowHead.FilledTriangle))
                return IsDashed ? UmlRelationKind.Realization : UmlRelationKind.Generalization;
            if (heads.Contains(UmlArrowHead.FilledDiamond))
                return UmlRelationKind.Composition;
            if (heads.Contains(UmlArrowHead.Diamond))
                return UmlRelationKind.Aggregation;
            if (heads.Contains(UmlArrowHead.Plus))
                return UmlRelationKind.Nesting;
            if (heads.Contains(UmlArrowHead.Arrow))
            {
                if (!IsDashed)
                    return UmlRelationKind.DirectedAssociation;
                if (Label?.Contains("include", StringComparison.OrdinalIgnoreCase) == true)
                    return UmlRelationKind.Include;
                if (Label?.Contains("extend", StringComparison.OrdinalIgnoreCase) == true)
                    return UmlRelationKind.Extend;
                return UmlRelationKind.Dependency;
            }
            return IsDashed ? UmlRelationKind.Link : UmlRelationKind.Association;
        }
    }

    /// <summary>
    /// Creates a relation drawn the UML way. The symbol goes to the end it belongs to: generalization and
    /// realization point to <paramref name="to"/> (the parent), the aggregation and composition diamond is at
    /// <paramref name="from"/> (the whole).
    /// </summary>
    public static UmlRelation Create(UmlRelationKind kind, string from, string to)
    {
        var r = new UmlRelation(from, to);
        switch (kind)
        {
            case UmlRelationKind.DirectedAssociation or UmlRelationKind.Transition:
                r.ToHead = UmlArrowHead.Arrow;
                break;
            case UmlRelationKind.Aggregation:
                r.FromHead = UmlArrowHead.Diamond;
                break;
            case UmlRelationKind.Composition:
                r.FromHead = UmlArrowHead.FilledDiamond;
                break;
            case UmlRelationKind.Generalization:
                r.ToHead = UmlArrowHead.Triangle;
                break;
            case UmlRelationKind.Realization:
                r.ToHead = UmlArrowHead.Triangle;
                r.LineStyle = UmlLineStyle.Dashed;
                break;
            case UmlRelationKind.Dependency:
                r.ToHead = UmlArrowHead.Arrow;
                r.LineStyle = UmlLineStyle.Dashed;
                break;
            case UmlRelationKind.Include or UmlRelationKind.Extend:
                r.ToHead = UmlArrowHead.Arrow;
                r.LineStyle = UmlLineStyle.Dashed;
                r.Label = kind == UmlRelationKind.Include ? "<<include>>" : "<<extend>>";
                break;
            case UmlRelationKind.Nesting:
                r.FromHead = UmlArrowHead.Plus;
                break;
            case UmlRelationKind.Link:
                r.LineStyle = UmlLineStyle.Dashed;
                break;
        }
        return r;
    }

    public override string ToString() => $"{From} → {To}";
}
