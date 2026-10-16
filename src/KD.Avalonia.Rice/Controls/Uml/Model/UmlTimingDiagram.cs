namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>Timing diagram: state changes of <see cref="Lifelines"/> along a common time axis.</summary>
public sealed class UmlTimingDiagram() : UmlDiagram(UmlDiagramKind.Timing)
{
    public List<UmlTimingLifeline> Lifelines { get; } = [];

    public List<UmlTimingChange> Changes { get; } = [];

    public List<UmlTimingMessage> Messages { get; } = [];

    public override bool SupportsKind(UmlDiagramKind kind) => kind == UmlDiagramKind.Timing;

    public UmlTimingLifeline? Find(string id) => Lifelines.FirstOrDefault(l => l.Id == id);
}

public enum UmlTimingLifelineKind
{
    /// <summary>One level per state, the line steps between them.</summary>
    Robust,
    /// <summary>One band, the state name written in it.</summary>
    Concise,
    /// <summary>Square wave with <see cref="UmlTimingLifeline.ClockPeriod"/>.</summary>
    Clock,
    /// <summary>High / low.</summary>
    Binary,
}

public sealed class UmlTimingLifeline(string id, string? name = null, UmlTimingLifelineKind kind = UmlTimingLifelineKind.Concise)
{
    public string Id { get; set; } = id;

    public string Name
    {
        get => field ?? Id;
        set => field = value;
    } = name;

    public UmlTimingLifelineKind Kind { get; set; } = kind;

    /// <summary>Order of the levels of a robust lifeline (<c>X has A,B,C</c>); states not listed are added in order of use.</summary>
    public List<string> States { get; } = [];

    public double ClockPeriod { get; set; } = 1;

    /// <summary>High time of a clock; half of the period when 0.</summary>
    public double ClockPulse { get; set; }

    public double ClockOffset { get; set; }

    public override string ToString() => Name;
}

/// <summary>From <see cref="Time"/> on, <see cref="Lifeline"/> is in <see cref="State"/> (for binary: high / low).</summary>
public sealed record UmlTimingChange(string Lifeline, double Time, string State);

public sealed record UmlTimingMessage(string From, double FromTime, string To, double ToTime, string? Text = null);
