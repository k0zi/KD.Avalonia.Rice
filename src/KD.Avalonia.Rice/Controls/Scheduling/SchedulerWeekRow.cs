using System.Globalization;
using Avalonia;
using Avalonia.Controls;

namespace KD.Avalonia.Rice.Controls.Scheduling;

/// <summary>
/// A row of days of a <see cref="RiceScheduler"/>: a week of the month view, or the all day strip of the week / day
/// view. The day cells fill the columns; the events are bars in lanes below the day numbers. When a day has more events
/// than fit (month view), its last visible lane shows a "+n more" button instead.
/// </summary>
internal sealed class SchedulerWeekRow : Panel
{
    private const double LaneHeight = 22;
    private const double LaneGap = 2;

    private readonly List<RiceSchedulerDayCell> _cells;
    private readonly List<(RiceSchedulerEventChip Chip, SpanPlacement Place)> _chips;
    private readonly Button[] _moreButtons;
    private readonly bool _fitToHeight;
    private int[] _visibleLanes;

    // fitToHeight: month view, the row has a given height and overflowing events turn into "+n more";
    // otherwise (the all day strip) the row grows with its lanes.
    public SchedulerWeekRow(List<RiceSchedulerDayCell> cells, List<(RiceSchedulerEventChip, SpanPlacement)> chips,
        Func<DateTime, Button> moreButton, bool fitToHeight, double headerHeight)
    {
        _cells = cells;
        _chips = chips;
        _fitToHeight = fitToHeight;
        HeaderHeight = headerHeight;
        _visibleLanes = new int[cells.Count];
        Children.AddRange(cells);
        Children.AddRange(chips.Select(c => c.Item1));
        _moreButtons = cells.Select(c => moreButton(c.Date)).ToArray();
        Children.AddRange(_moreButtons);
    }

    public double HeaderHeight { get; }

    public IReadOnlyList<RiceSchedulerDayCell> Cells => _cells;

    private int Days => _cells.Count;

    private int LaneCount => _chips.Count == 0 ? 0 : _chips.Max(c => c.Place.Lane) + 1;

    protected override Size MeasureOverride(Size availableSize)
    {
        var columnWidth = double.IsInfinity(availableSize.Width) ? 120 : availableSize.Width / Math.Max(1, Days);
        foreach (var cell in _cells)
            cell.Measure(new Size(columnWidth, availableSize.Height));

        var lanesFitting = _fitToHeight && !double.IsInfinity(availableSize.Height)
            ? Math.Max(1, (int)Math.Floor((availableSize.Height - HeaderHeight - LaneGap) / LaneHeight))
            : int.MaxValue;
        _visibleLanes = VisibleLanes(_chips.Select(c => c.Place).ToList(), Days, lanesFitting);
        for (var day = 0; day < Days; day++)
        {
            var hidden = _chips.Count(c => Covers(c.Place, day) && !IsShown(c.Place));
            var button = _moreButtons[day];
            button.Content = hidden > 0 ? string.Format(CultureInfo.CurrentCulture, RiceStrings.SchedulerMore, hidden) : null;
            button.Measure(new Size(columnWidth, LaneHeight));
        }
        foreach (var (chip, place) in _chips)
            chip.Measure(new Size(place.Span * columnWidth, LaneHeight));

        var height = _fitToHeight ? 0 : HeaderHeight + Math.Max(1, LaneCount) * LaneHeight + LaneGap;
        return new Size(0, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columnWidth = finalSize.Width / Math.Max(1, Days);
        for (var day = 0; day < Days; day++)
            _cells[day].Arrange(new Rect(day * columnWidth, 0, columnWidth, finalSize.Height));
        foreach (var (chip, place) in _chips)
        {
            chip.Arrange(IsShown(place)
                ? new Rect(place.Column * columnWidth + 2, HeaderHeight + place.Lane * LaneHeight, place.Span * columnWidth - 4, LaneHeight - LaneGap)
                : default);
        }
        for (var day = 0; day < Days; day++)
        {
            var button = _moreButtons[day];
            button.Arrange(button.Content is null
                ? default
                : new Rect(day * columnWidth + 2, HeaderHeight + _visibleLanes[day] * LaneHeight, columnWidth - 4, LaneHeight - LaneGap));
        }
        return finalSize;
    }

    private bool IsShown(SpanPlacement place)
    {
        for (var day = place.Column; day < place.Column + place.Span; day++)
        {
            if (place.Lane >= _visibleLanes[day])
                return false;
        }
        return true;
    }

    private static bool Covers(SpanPlacement place, int day) => day >= place.Column && day < place.Column + place.Span;

    /// <summary>
    /// How many lanes each day shows: all of them when they fit, else one less than fit, leaving the last for the
    /// "+n more" button.
    /// </summary>
    internal static int[] VisibleLanes(IReadOnlyList<SpanPlacement> places, int days, int lanesFitting)
    {
        var result = new int[days];
        for (var day = 0; day < days; day++)
        {
            var used = places.Where(p => Covers(p, day)).Select(p => p.Lane + 1).DefaultIfEmpty(0).Max();
            result[day] = used <= lanesFitting ? lanesFitting : Math.Max(0, lanesFitting - 1);
        }
        // A bar hidden because of a crowded neighbour is hidden on all of its days: those need the "+n more" lane too.
        bool changed;
        do
        {
            changed = false;
            foreach (var place in places)
            {
                var shown = Enumerable.Range(place.Column, place.Span).All(d => place.Lane < result[d]);
                if (shown)
                    continue;
                for (var day = place.Column; day < place.Column + place.Span; day++)
                {
                    if (result[day] == lanesFitting && lanesFitting > 0)
                    {
                        result[day] = lanesFitting - 1;
                        changed = true;
                    }
                }
            }
        }
        while (changed);
        return result;
    }
}
