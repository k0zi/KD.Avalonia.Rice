namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>
/// Sequence diagram: <see cref="Participants"/> side by side and the <see cref="Events"/> (messages, activations,
/// fragments, notes, …) from top to bottom, in order.
/// </summary>
public sealed class UmlSequenceDiagram() : UmlDiagram(UmlDiagramKind.Sequence)
{
    /// <summary>Lost and found messages start or end at this pseudo participant on the left …</summary>
    public const string LeftBorder = "[";

    /// <summary>… or on the right.</summary>
    public const string RightBorder = "]";

    public List<UmlParticipant> Participants { get; } = [];

    public List<UmlSequenceEvent> Events { get; } = [];

    /// <summary>Numbers the messages (PlantUML <c>autonumber</c>).</summary>
    public bool AutoNumber { get; set; }

    /// <summary>Repeats the participants at the bottom (PlantUML <c>hide footbox</c> turns it off).</summary>
    public bool ShowFootbox { get; set; } = true;

    public override bool SupportsKind(UmlDiagramKind kind) => kind == UmlDiagramKind.Sequence;

    public UmlParticipant? Find(string id) => Participants.FirstOrDefault(p => p.Id == id);

    public UmlParticipant AddParticipant(string id, string? name = null, UmlParticipantKind kind = UmlParticipantKind.Participant)
    {
        var participant = new UmlParticipant(id, name, kind);
        Participants.Add(participant);
        return participant;
    }

    public UmlMessage AddMessage(string from, string to, string? text = null, UmlMessageKind kind = UmlMessageKind.Synchronous)
    {
        var message = new UmlMessage(from, to, text, kind);
        Events.Add(message);
        return message;
    }
}

public enum UmlParticipantKind
{
    Participant,
    Actor,
    Boundary,
    Control,
    Entity,
    Database,
    Collections,
    Queue,
}

public sealed class UmlParticipant(string id, string? name = null, UmlParticipantKind kind = UmlParticipantKind.Participant)
{
    public string Id { get; set; } = id;

    public string Name
    {
        get => field ?? Id;
        set => field = value;
    } = name;

    public UmlParticipantKind Kind { get; set; } = kind;

    public List<string> Stereotypes { get; } = [];

    public override string ToString() => Name;
}

/// <summary>One step of a sequence diagram.</summary>
public abstract class UmlSequenceEvent;

public enum UmlMessageKind
{
    /// <summary>Solid line, filled arrow (<c>-&gt;</c>).</summary>
    Synchronous,
    /// <summary>Solid line, open arrow (<c>-&gt;&gt;</c>).</summary>
    Asynchronous,
    /// <summary>Dashed line, open arrow (<c>--&gt;</c>).</summary>
    Reply,
}

/// <summary>A message; <see cref="From"/> or <see cref="To"/> can be <see cref="UmlSequenceDiagram.LeftBorder"/> or <see cref="UmlSequenceDiagram.RightBorder"/>.</summary>
public sealed class UmlMessage(string from, string to, string? text = null, UmlMessageKind kind = UmlMessageKind.Synchronous)
    : UmlSequenceEvent
{
    public string From { get; set; } = from;

    public string To { get; set; } = to;

    public string? Text { get; set; } = text;

    public UmlMessageKind Kind { get; set; } = kind;

    /// <summary>Starts an activation on the receiver (PlantUML <c>++</c>).</summary>
    public bool ActivateTarget { get; set; }

    /// <summary>Ends the sender's activation (PlantUML <c>--</c>).</summary>
    public bool DeactivateSource { get; set; }

    /// <summary>The receiver is created by this message, its head is drawn here instead of at the top (<c>**</c>).</summary>
    public bool CreateTarget { get; set; }

    /// <summary>The receiver is destroyed by this message (<c>!!</c>).</summary>
    public bool DestroyTarget { get; set; }

    public bool IsSelf => From == To;

    public override string ToString() => $"{From} → {To}: {Text}";
}

public enum UmlActivationKind
{
    Activate,
    Deactivate,
    Destroy,
    Create,
}

public sealed class UmlActivation(string participant, UmlActivationKind kind) : UmlSequenceEvent
{
    public string Participant { get; set; } = participant;

    public UmlActivationKind Kind { get; set; } = kind;
}

public enum UmlFragmentKind
{
    Alt,
    Opt,
    Loop,
    Par,
    Break,
    Critical,
    Group,
}

/// <summary>Opens a combined fragment; it lasts until the matching <see cref="UmlFragmentEnd"/>.</summary>
public sealed class UmlFragmentStart(UmlFragmentKind kind, string? label = null) : UmlSequenceEvent
{
    public UmlFragmentKind Kind { get; set; } = kind;

    /// <summary>Condition or title, e.g. "[x > 0]".</summary>
    public string? Label { get; set; } = label;
}

/// <summary>Next operand of the enclosing fragment (<c>else</c>).</summary>
public sealed class UmlFragmentElse(string? label = null) : UmlSequenceEvent
{
    public string? Label { get; set; } = label;
}

public sealed class UmlFragmentEnd : UmlSequenceEvent;

/// <summary>Reference to another interaction (<c>ref over A, B : text</c>).</summary>
public sealed class UmlInteractionUse(IEnumerable<string> participants, string text) : UmlSequenceEvent
{
    public List<string> Participants { get; } = [.. participants];

    public string Text { get; set; } = text;
}

/// <summary>Horizontal separator across the diagram (<c>== text ==</c>).</summary>
public sealed class UmlDivider(string? text = null) : UmlSequenceEvent
{
    public string? Text { get; set; } = text;
}

/// <summary>Time passes (<c>...</c> or <c>...text...</c>).</summary>
public sealed class UmlDelay(string? text = null) : UmlSequenceEvent
{
    public string? Text { get; set; } = text;
}

/// <summary>Extra vertical space (<c>|||</c> or <c>||45||</c>).</summary>
public sealed class UmlSpacing(double? height = null) : UmlSequenceEvent
{
    public double? Height { get; set; } = height;
}

public enum UmlNotePosition
{
    Left,
    Right,
    Over,
}

public sealed class UmlSequenceNote(UmlNotePosition position, IEnumerable<string> participants, string text) : UmlSequenceEvent
{
    public UmlNotePosition Position { get; set; } = position;

    /// <summary>One participant, or two for <c>note over A, B</c>.</summary>
    public List<string> Participants { get; } = [.. participants];

    public string Text { get; set; } = text;
}
