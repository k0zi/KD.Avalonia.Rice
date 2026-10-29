using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls;

/// <summary>Color of the dot of a <see cref="RiceTimelineItem"/>.</summary>
public enum TimelineItemKind
{
    /// <summary>A neutral dot.</summary>
    Default,

    /// <summary>The accent color of the theme.</summary>
    Info,

    /// <summary>Green.</summary>
    Success,

    /// <summary>Amber.</summary>
    Warning,

    /// <summary>The danger color of the theme (red).</summary>
    Error,
}

/// <summary>
/// One event of a <see cref="RiceTimeline"/>: <c>Header</c> is the title, <c>Content</c> the details below it,
/// <see cref="Timestamp"/> the time (also groups the items by day), <see cref="Icon"/> replaces the dot.
/// Pseudo classes: <c>:first</c> and <c>:last</c> (the line starts / ends at the dot), <c>:has-icon</c>, and the kind:
/// <c>:info</c>, <c>:success</c>, <c>:warning</c>, <c>:error</c>.
/// </summary>
public class RiceTimelineItem : HeaderedContentControl
{
    public static readonly StyledProperty<DateTime?> TimestampProperty =
        AvaloniaProperty.Register<RiceTimelineItem, DateTime?>(nameof(Timestamp));

    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<RiceTimelineItem, object?>(nameof(Icon));

    public static readonly StyledProperty<TimelineItemKind> KindProperty =
        AvaloniaProperty.Register<RiceTimelineItem, TimelineItemKind>(nameof(Kind));

    /// <summary>Color of the dot / icon; set by the theme from <see cref="Kind"/>.</summary>
    public static readonly StyledProperty<IBrush?> KindBrushProperty =
        AvaloniaProperty.Register<RiceTimelineItem, IBrush?>(nameof(KindBrush));

    /// <summary>Content at the right of the title, e.g. a button or a badge.</summary>
    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<RiceTimelineItem, object?>(nameof(Actions));

    public static readonly DirectProperty<RiceTimelineItem, string?> DayHeaderProperty =
        AvaloniaProperty.RegisterDirect<RiceTimelineItem, string?>(nameof(DayHeader), o => o.DayHeader);

    public static readonly DirectProperty<RiceTimelineItem, string?> TimestampTextProperty =
        AvaloniaProperty.RegisterDirect<RiceTimelineItem, string?>(nameof(TimestampText), o => o.TimestampText);

    private static readonly (TimelineItemKind Kind, string PseudoClass)[] KindClasses =
    [
        (TimelineItemKind.Info, ":info"), (TimelineItemKind.Success, ":success"),
        (TimelineItemKind.Warning, ":warning"), (TimelineItemKind.Error, ":error"),
    ];

    private string? _dayHeader;
    private string? _timestampText;

    public RiceTimelineItem()
    {
        PseudoClasses.Set(":first", true);
        PseudoClasses.Set(":last", true);
    }

    /// <summary>When the event happened (local time); shown with the timeline's <c>TimestampFormat</c>.</summary>
    public DateTime? Timestamp
    {
        get => GetValue(TimestampProperty);
        set => SetValue(TimestampProperty, value);
    }

    /// <summary>An <c>IImage</c>, a <c>MaterialIconKind</c>, a string (e.g. an emoji) or any control, in a circle instead of the dot.</summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public TimelineItemKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public IBrush? KindBrush
    {
        get => GetValue(KindBrushProperty);
        set => SetValue(KindBrushProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <summary>The day header above this item (set by the timeline), null when it continues the day of the item before.</summary>
    public string? DayHeader
    {
        get => _dayHeader;
        private set => SetAndRaise(DayHeaderProperty, ref _dayHeader, value);
    }

    /// <summary>The formatted <see cref="Timestamp"/>.</summary>
    public string? TimestampText
    {
        get => _timestampText;
        private set => SetAndRaise(TimestampTextProperty, ref _timestampText, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceTimelineItem);

    internal void Update(string? dayHeader, bool isFirst, bool isLast, string? timestampText)
    {
        DayHeader = dayHeader;
        TimestampText = timestampText;
        PseudoClasses.Set(":first", isFirst);
        PseudoClasses.Set(":last", isLast);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TimestampProperty)
        {
            // Outside a timeline it keeps this text; in one the timeline formats it (and may move the day headers).
            TimestampText = Timestamp?.ToString("g", CultureInfo.CurrentCulture);
            (ItemsControl.ItemsControlFromItemContainer(this) as RiceTimeline)?.PostUpdate();
        }
        else if (change.Property == KindProperty)
        {
            foreach (var (kind, pseudoClass) in KindClasses)
                PseudoClasses.Set(pseudoClass, Kind == kind);
        }
        else if (change.Property == IconProperty)
            PseudoClasses.Set(":has-icon", Icon is not null);
    }
}
