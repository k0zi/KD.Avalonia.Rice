namespace KD.Avalonia.Rice.Controls.Scheduling;

public enum SchedulerViewMode
{
    Month,
    Week,
    Day,
}

/// <summary>Where a bar over days goes in a week row: from <see cref="Column"/>, <see cref="Span"/> days wide, in <see cref="Lane"/>.</summary>
internal readonly record struct SpanPlacement(RiceCalendarEvent Event, int Column, int Span, int Lane, bool ContinuesBefore, bool ContinuesAfter);

/// <summary>Where a timed event goes in its day: <see cref="Slot"/> of <see cref="SlotCount"/> side by side columns.</summary>
internal readonly record struct TimedPlacement(RiceCalendarEvent Event, double StartMinute, double EndMinute, int Slot, int SlotCount);

/// <summary>The date arithmetic and the placement of events of <see cref="RiceScheduler"/>, without UI.</summary>
internal static class SchedulerLayout
{
    public const int MonthWeeks = 6;

    public static DateTime LastDay(DateTime start, DateTime end)
    {
        if (end <= start)
            return start.Date;
        return end.TimeOfDay == TimeSpan.Zero ? end.Date.AddDays(-1) : end.Date;
    }

    public static DateTime StartOfWeek(DateTime date, DayOfWeek firstDay)
    {
        var diff = ((int)date.DayOfWeek - (int)firstDay + 7) % 7;
        return date.Date.AddDays(-diff);
    }

    /// <summary>The first day shown and the number of days of a view around <paramref name="date"/>.</summary>
    public static (DateTime Start, int Days) VisibleRange(SchedulerViewMode mode, DateTime date, DayOfWeek firstDay) => mode switch
    {
        SchedulerViewMode.Month => (StartOfWeek(new DateTime(date.Year, date.Month, 1), firstDay), MonthWeeks * 7),
        SchedulerViewMode.Week => (StartOfWeek(date, firstDay), 7),
        _ => (date.Date, 1),
    };

    /// <summary>The date one page before / after (<paramref name="direction"/> -1 / +1).</summary>
    public static DateTime Step(SchedulerViewMode mode, DateTime date, int direction) => mode switch
    {
        SchedulerViewMode.Month => date.AddMonths(direction),
        SchedulerViewMode.Week => date.AddDays(7 * direction),
        _ => date.AddDays(direction),
    };

    /// <summary>
    /// Lanes for the events over the days of a row (<paramref name="days"/> days from <paramref name="rowStart"/>): the
    /// longer and earlier events get the upper lanes, an event takes the first lane free on all of its days.
    /// </summary>
    public static IReadOnlyList<SpanPlacement> PlaceSpans(IEnumerable<RiceCalendarEvent> events, DateTime rowStart, int days)
    {
        var rowEnd = rowStart.AddDays(days - 1);
        var inRow = events
            .Where(e => e.FirstDay <= rowEnd && e.LastDay >= rowStart)
            .OrderBy(e => e.FirstDay)
            .ThenByDescending(e => (e.LastDay - e.FirstDay).Days)
            .ThenBy(e => e.IsMultiDay ? 0 : 1)
            .ThenBy(e => e.Start)
            .ToList();
        var lanes = new List<bool[]>();
        var result = new List<SpanPlacement>();
        foreach (var e in inRow)
        {
            var first = Math.Max(0, (e.FirstDay - rowStart).Days);
            var last = Math.Min(days - 1, (e.LastDay - rowStart).Days);
            var lane = lanes.FindIndex(l => !l.AsSpan(first, last - first + 1).Contains(true));
            if (lane < 0)
            {
                lane = lanes.Count;
                lanes.Add(new bool[days]);
            }
            lanes[lane].AsSpan(first, last - first + 1).Fill(true);
            result.Add(new SpanPlacement(e, first, last - first + 1, lane, e.FirstDay < rowStart, e.LastDay > rowEnd));
        }
        return result;
    }

    /// <summary>
    /// Side by side columns for the timed events of one day: overlapping events form a group, each takes the first
    /// column free at its start, and the group is divided into as many columns as it needed.
    /// </summary>
    public static IReadOnlyList<TimedPlacement> PlaceTimed(IEnumerable<RiceCalendarEvent> events, DateTime day)
    {
        var items = events
            .Select(e => (Event: e, Start: Math.Max(0, (e.Start - day).TotalMinutes), End: Math.Min(24 * 60, (e.End - day).TotalMinutes)))
            .Select(x => x with { End = Math.Max(x.End, x.Start + 15) })
            .OrderBy(x => x.Start)
            .ThenByDescending(x => x.End - x.Start)
            .ToList();
        var result = new List<TimedPlacement>();
        var group = new List<(RiceCalendarEvent Event, double Start, double End, int Slot)>();
        var columnEnds = new List<double>();
        double groupEnd = double.MinValue;

        void Flush()
        {
            result.AddRange(group.Select(g => new TimedPlacement(g.Event, g.Start, g.End, g.Slot, columnEnds.Count)));
            group.Clear();
            columnEnds.Clear();
        }

        foreach (var item in items)
        {
            if (item.Start >= groupEnd)
                Flush();
            var slot = columnEnds.FindIndex(end => end <= item.Start);
            if (slot < 0)
            {
                slot = columnEnds.Count;
                columnEnds.Add(item.End);
            }
            else
                columnEnds[slot] = item.End;
            group.Add((item.Event, item.Start, item.End, slot));
            groupEnd = Math.Max(groupEnd, item.End);
        }
        Flush();
        return result;
    }

    /// <summary>Rounds minutes to the nearest multiple of <paramref name="step"/>.</summary>
    public static double Snap(double minutes, int step) => step <= 1 ? Math.Round(minutes) : Math.Round(minutes / step) * step;
}
