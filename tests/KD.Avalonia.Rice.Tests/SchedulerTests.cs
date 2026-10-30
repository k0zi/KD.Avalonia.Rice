using KD.Avalonia.Rice.Controls.Scheduling;

namespace KD.Avalonia.Rice.Tests;

public class SchedulerTests
{
    private static RiceCalendarEvent Event(string title, DateTime start, DateTime end, bool allDay = false) =>
        new() { Title = title, Start = start, End = end, IsAllDay = allDay };

    [Fact]
    public void An_end_at_midnight_belongs_to_the_day_before()
    {
        var day = new DateTime(2026, 10, 29);

        Assert.Equal(day, Event("a", day, day.AddDays(1), allDay: true).LastDay);
        Assert.Equal(day.AddDays(1), Event("b", day.AddHours(22), day.AddDays(1).AddHours(1)).LastDay);
        Assert.False(Event("c", day.AddHours(9), day.AddHours(10)).IsMultiDay);
        Assert.True(Event("d", day, day, allDay: true).IsMultiDay);
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, 26)]
    [InlineData(DayOfWeek.Sunday, 25)]
    public void Weeks_start_on_the_chosen_day(DayOfWeek first, int expectedDay)
    {
        Assert.Equal(new DateTime(2026, 10, expectedDay), SchedulerLayout.StartOfWeek(new DateTime(2026, 10, 29, 15, 0, 0), first));
    }

    [Fact]
    public void The_month_view_shows_six_weeks_from_the_week_of_the_first()
    {
        var (start, days) = SchedulerLayout.VisibleRange(SchedulerViewMode.Month, new DateTime(2026, 10, 29), DayOfWeek.Monday);

        Assert.Equal(new DateTime(2026, 9, 28), start);
        Assert.Equal(42, days);
        Assert.Equal(new DateTime(2026, 11, 29), SchedulerLayout.Step(SchedulerViewMode.Month, new DateTime(2026, 10, 29), 1));
        Assert.Equal(new DateTime(2026, 10, 22), SchedulerLayout.Step(SchedulerViewMode.Week, new DateTime(2026, 10, 29), -1));
    }

    [Fact]
    public void Bars_take_the_first_lane_free_on_all_their_days()
    {
        var monday = new DateTime(2026, 10, 26);
        var long1 = Event("long", monday, monday.AddDays(4), allDay: true);
        var mid = Event("mid", monday.AddDays(1).AddHours(9), monday.AddDays(1).AddHours(10));
        var later = Event("later", monday.AddDays(5), monday.AddDays(6), allDay: true);
        var spill = Event("spill", monday.AddDays(-2), monday.AddDays(2), allDay: true);

        var places = SchedulerLayout.PlaceSpans([mid, long1, later, spill], monday, 7).ToDictionary(p => p.Event.Title);

        Assert.Equal((0, 2, 0), (places["spill"].Column, places["spill"].Span, places["spill"].Lane));
        Assert.True(places["spill"].ContinuesBefore);
        Assert.Equal((0, 4, 1), (places["long"].Column, places["long"].Span, places["long"].Lane));
        Assert.Equal(2, places["mid"].Lane);
        Assert.Equal(0, places["later"].Lane);
    }

    [Fact]
    public void Overlapping_timed_events_stand_side_by_side()
    {
        var day = new DateTime(2026, 10, 29);
        var a = Event("a", day.AddHours(9), day.AddHours(11));
        var b = Event("b", day.AddHours(10), day.AddHours(12));
        var c = Event("c", day.AddHours(11), day.AddHours(12));
        var d = Event("d", day.AddHours(14), day.AddHours(15));

        var places = SchedulerLayout.PlaceTimed([a, b, c, d], day).ToDictionary(p => p.Event.Title);

        Assert.Equal((0, 2), (places["a"].Slot, places["a"].SlotCount));
        Assert.Equal((1, 2), (places["b"].Slot, places["b"].SlotCount));
        // c starts when a ends: it takes a's column.
        Assert.Equal((0, 2), (places["c"].Slot, places["c"].SlotCount));
        Assert.Equal((0, 1), (places["d"].Slot, places["d"].SlotCount));
        Assert.Equal(9 * 60, places["a"].StartMinute);
    }

    [Theory]
    [InlineData(52, 15, 45)]
    [InlineData(53, 15, 60)]
    [InlineData(53, 1, 53)]
    public void Times_snap_to_the_step(double minutes, int step, double expected)
    {
        Assert.Equal(expected, SchedulerLayout.Snap(minutes, step));
    }

    [Fact]
    public void Crowded_days_keep_a_lane_for_more_and_hide_bars_crossing_them()
    {
        // Day 0 has three lanes of events, two fit: it shows one lane and "+n more". The bar in lane 1 from day 0 to 1
        // is hidden, so day 1 needs the "+n more" lane as well.
        var e = Event("x", DateTime.Today, DateTime.Today.AddHours(1));
        SpanPlacement[] places = [new(e, 0, 1, 0, false, false), new(e, 0, 2, 1, false, false), new(e, 0, 1, 2, false, false)];

        Assert.Equal([1, 1, 2], SchedulerWeekRow.VisibleLanes(places, 3, 2));
    }
}
