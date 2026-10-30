using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls.Scheduling;

/// <summary>How a <see cref="RiceSchedulerEventChip"/> is drawn.</summary>
public enum SchedulerChipKind
{
    /// <summary>A bar over one or more days (all day and multi day events, and every event in the month view).</summary>
    Bar,

    /// <summary>A timed event in the month view: a dot, the time and the title, without a background.</summary>
    Dot,

    /// <summary>A block in the hours of the week and day views.</summary>
    Block,
}

/// <summary>
/// One event of a <see cref="RiceScheduler"/> on screen (<c>Content</c> is the title).
/// Pseudo classes: <c>:bar</c>, <c>:dot</c>, <c>:block</c>, <c>:selected</c>, <c>:dragging</c>, <c>:past</c>,
/// <c>:continues-before</c> / <c>:continues-after</c> (the event goes on in the previous / next row).
/// </summary>
public class RiceSchedulerEventChip : ContentControl
{
    public static readonly StyledProperty<IBrush?> EventBrushProperty =
        AvaloniaProperty.Register<RiceSchedulerEventChip, IBrush?>(nameof(EventBrush));

    public static readonly StyledProperty<string?> TimeTextProperty =
        AvaloniaProperty.Register<RiceSchedulerEventChip, string?>(nameof(TimeText));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<RiceSchedulerEventChip, bool>(nameof(IsSelected));

    private static readonly (SchedulerChipKind Kind, string PseudoClass)[] KindClasses =
        [(SchedulerChipKind.Bar, ":bar"), (SchedulerChipKind.Dot, ":dot"), (SchedulerChipKind.Block, ":block")];

    internal RiceSchedulerEventChip(RiceCalendarEvent calendarEvent, SchedulerChipKind kind)
    {
        Event = calendarEvent;
        Kind = kind;
        Content = calendarEvent.Title;
        if (calendarEvent.Color is { } color)
            EventBrush = color;
        foreach (var (k, pseudoClass) in KindClasses)
            PseudoClasses.Set(pseudoClass, k == kind);
        PseudoClasses.Set(":past", calendarEvent.End < DateTime.Now);
        ToolTip.SetTip(this, calendarEvent.Description is { Length: > 0 } description
            ? $"{calendarEvent.Title}\n{description}"
            : calendarEvent.Title);
    }

    public RiceCalendarEvent Event { get; }

    public SchedulerChipKind Kind { get; }

    /// <summary>The event's <see cref="RiceCalendarEvent.Color"/>, else the accent (set by the theme).</summary>
    public IBrush? EventBrush
    {
        get => GetValue(EventBrushProperty);
        set => SetValue(EventBrushProperty, value);
    }

    public string? TimeText
    {
        get => GetValue(TimeTextProperty);
        set => SetValue(TimeTextProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceSchedulerEventChip);

    internal void SetContinues(bool before, bool after)
    {
        PseudoClasses.Set(":continues-before", before);
        PseudoClasses.Set(":continues-after", after);
    }

    internal void SetDragging(bool dragging) => PseudoClasses.Set(":dragging", dragging);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
            PseudoClasses.Set(":selected", IsSelected);
    }
}
