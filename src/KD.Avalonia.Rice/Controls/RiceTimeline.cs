using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Vertical list of events along a line: every <see cref="RiceTimelineItem"/> has a dot (or an icon) on the line, a
/// title, a time and optional details. With <see cref="GroupByDay"/> the items are grouped under day headers
/// ("Today", "Yesterday", the date) by their <see cref="RiceTimelineItem.Timestamp"/>; the items are shown in the given
/// order, so sort them (newest or oldest first) before.
/// From data: the items get <see cref="RiceTimelineItem"/> containers, set <c>Timestamp</c>, <c>Kind</c>, … with a style.
/// </summary>
public class RiceTimeline : ItemsControl
{
    public static readonly StyledProperty<bool> GroupByDayProperty =
        AvaloniaProperty.Register<RiceTimeline, bool>(nameof(GroupByDay), true);

    /// <summary>Format of the times (inherited); null: <c>t</c> (time only) when grouped by day, else <c>g</c>.</summary>
    public static readonly StyledProperty<string?> TimestampFormatProperty =
        AvaloniaProperty.Register<RiceTimeline, string?>(nameof(TimestampFormat), inherits: true);

    private bool _updatePosted;

    public RiceTimeline()
    {
        Items.CollectionChanged += (_, _) => PostUpdate();
    }

    /// <summary>Puts a day header above the first item of every day. Default true.</summary>
    public bool GroupByDay
    {
        get => GetValue(GroupByDayProperty);
        set => SetValue(GroupByDayProperty, value);
    }

    public string? TimestampFormat
    {
        get => GetValue(TimestampFormatProperty);
        set => SetValue(TimestampFormatProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceTimeline);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new RiceTimelineItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<RiceTimelineItem>(item, out recycleKey);

    protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
    {
        base.ContainerIndexChangedOverride(container, oldIndex, newIndex);
        PostUpdate();
    }

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        PostUpdate();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GroupByDayProperty || change.Property == TimestampFormatProperty)
            PostUpdate();
        else if (change.Property == ItemsSourceProperty)
        {
            if (change.OldValue is INotifyCollectionChanged oldSource)
                oldSource.CollectionChanged -= OnSourceChanged;
            if (change.NewValue is INotifyCollectionChanged newSource)
                newSource.CollectionChanged += OnSourceChanged;
            PostUpdate();
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => PostUpdate();

    // The headers and line ends depend on the neighbours, so they are set for every item at once, after the
    // containers got their (style bound) timestamps.
    internal void PostUpdate()
    {
        if (_updatePosted)
            return;
        _updatePosted = true;
        Dispatcher.UIThread.Post(UpdateItems, DispatcherPriority.Loaded);
    }

    private void UpdateItems()
    {
        _updatePosted = false;
        var containers = Enumerable.Range(0, ItemCount).Select(ContainerFromIndex).OfType<RiceTimelineItem>().ToList();
        var slots = LayoutSlots(containers.Select(c => c.Timestamp?.Date).ToList(), GroupByDay);
        var today = DateTime.Today;
        var format = TimestampFormat ?? (GroupByDay ? "t" : "g");
        for (var i = 0; i < containers.Count; i++)
        {
            var item = containers[i];
            var slot = slots[i];
            item.Update(slot.Day is { } day ? DayHeader(day, today) : null, slot.IsFirst, slot.IsLast,
                item.Timestamp?.ToString(format, CultureInfo.CurrentCulture));
        }
    }

    /// <summary>
    /// For every item: the day whose header goes above it (null: no header), and whether the line starts or ends at
    /// its dot (the first and last item of a day, or of the whole timeline when not grouped).
    /// Items without a timestamp stay in the group of the item before them.
    /// </summary>
    internal static IReadOnlyList<TimelineSlot> LayoutSlots(IReadOnlyList<DateTime?> days, bool groupByDay)
    {
        var starts = new bool[days.Count];
        var headers = new DateTime?[days.Count];
        DateTime? current = null;
        for (var i = 0; i < days.Count; i++)
        {
            if (i == 0)
                starts[i] = true;
            if (!groupByDay || days[i] is not { } day || day == current)
                continue;
            current = day;
            starts[i] = true;
            headers[i] = day;
        }
        return Enumerable.Range(0, days.Count)
            .Select(i => new TimelineSlot(headers[i], starts[i], i == days.Count - 1 || starts[i + 1]))
            .ToList();
    }

    /// <summary>The header of a day: "Today", "Yesterday" (<see cref="RiceStrings"/>), else the long date.</summary>
    public static string DayHeader(DateTime day, DateTime today)
    {
        if (day.Date == today.Date)
            return RiceStrings.TimelineToday;
        if (day.Date == today.Date.AddDays(-1))
            return RiceStrings.TimelineYesterday;
        return day.ToString("D", CultureInfo.CurrentCulture);
    }
}

internal readonly record struct TimelineSlot(DateTime? Day, bool IsFirst, bool IsLast);
