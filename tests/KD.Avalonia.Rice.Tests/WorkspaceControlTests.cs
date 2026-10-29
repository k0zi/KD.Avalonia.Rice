using System.Text.Json;
using KD.Avalonia.Rice.Controls;

namespace KD.Avalonia.Rice.Tests;

public class WorkspaceControlTests
{
    [Fact]
    public void Timeline_puts_a_header_above_the_first_item_of_every_day()
    {
        var day1 = new DateTime(2026, 10, 29);
        var day2 = new DateTime(2026, 10, 28);
        var slots = RiceTimeline.LayoutSlots([day1, day1, null, day2], groupByDay: true);

        Assert.Equal([day1, null, null, day2], slots.Select(s => s.Day));
        // The line breaks between the days; the item without a time stays with the day before it.
        Assert.Equal([true, false, false, true], slots.Select(s => s.IsFirst));
        Assert.Equal([false, false, true, true], slots.Select(s => s.IsLast));
    }

    [Fact]
    public void Ungrouped_timeline_is_one_strand_without_headers()
    {
        var slots = RiceTimeline.LayoutSlots([new DateTime(2026, 1, 1), new DateTime(2026, 1, 2)], groupByDay: false);

        Assert.All(slots, s => Assert.Null(s.Day));
        Assert.Equal([true, false], slots.Select(s => s.IsFirst));
        Assert.Equal([false, true], slots.Select(s => s.IsLast));
    }

    [Fact]
    public void Day_headers_name_today_and_yesterday()
    {
        var today = new DateTime(2026, 10, 29, 15, 0, 0);

        Assert.Equal(RiceStrings.TimelineToday, RiceTimeline.DayHeader(today.Date.AddHours(1), today));
        Assert.Equal(RiceStrings.TimelineYesterday, RiceTimeline.DayHeader(today.AddDays(-1), today));
        Assert.NotEqual(RiceStrings.TimelineYesterday, RiceTimeline.DayHeader(today.AddDays(-2), today));
    }

    [Theory]
    [InlineData(false, 2, 0, 0)]
    [InlineData(true, 2, 0, 0)]
    [InlineData(true, 0, 3, 2)]
    [InlineData(true, 2, 2, -1)]
    [InlineData(true, 2, 3, -1)]
    public void Kanban_drop_index_accounts_for_the_removed_card(bool sameColumn, int source, int target, int expected)
    {
        Assert.Equal(expected, RiceKanbanBoard.FinalIndex(sameColumn, source, target));
    }

    [Theory]
    [InlineData(3, 3, true, true)]
    [InlineData(2, 3, true, false)]
    [InlineData(3, 3, false, false)]
    [InlineData(9, 0, true, false)]
    public void Kanban_column_is_full_only_with_an_enforced_limit(int count, int limit, bool enforce, bool full)
    {
        Assert.Equal(full, RiceKanbanBoard.IsFull(count, limit, enforce));
    }

    [Theory]
    [InlineData(240, 100, double.PositiveInfinity, 1000, 240)]
    [InlineData(50, 100, double.PositiveInfinity, 1000, 100)]
    [InlineData(900, 100, 400, 1000, 400)]
    [InlineData(240, 100, double.PositiveInfinity, 300, 180)]
    [InlineData(240, 100, double.PositiveInfinity, 50, 0)]
    public void Dock_pane_takes_its_size_but_leaves_the_content_its_minimum(double size, double min, double max, double available, double expected)
    {
        Assert.Equal(expected, RiceDockLayout.PaneExtent(size, min, max, available, contentMin: 120));
    }

    [Fact]
    public void Dock_layout_state_survives_json()
    {
        var state = new RiceDockLayoutState
        {
            Panes = { ["explorer"] = new RiceDockPaneState(260, false), ["output"] = new RiceDockPaneState(140, true) },
        };

        var json = JsonSerializer.Serialize(state, DockLayoutContext.Default.RiceDockLayoutState);
        var back = JsonSerializer.Deserialize(json, DockLayoutContext.Default.RiceDockLayoutState)!;

        Assert.Contains("\"isCollapsed\": true", json);
        Assert.Equal(state.Panes, back.Panes);
    }
}
