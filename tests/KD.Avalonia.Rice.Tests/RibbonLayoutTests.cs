using Avalonia;
using KD.Avalonia.Rice.Controls;

namespace KD.Avalonia.Rice.Tests;

public class RibbonLayoutTests
{
    [Fact]
    public void Groups_collapse_from_the_right_until_they_fit()
    {
        var collapse = RiceRibbonGroupsPanel.ChooseCollapsed([200, 200, 200, 200], [60, 60, 60, 60], [true, true, true, true], 600);

        // 800 -> 660 (last collapsed) -> 520 (third collapsed): fits.
        Assert.Equal([false, false, true, true], collapse);
    }

    [Fact]
    public void Nothing_collapses_when_there_is_room_or_no_limit()
    {
        Assert.All(RiceRibbonGroupsPanel.ChooseCollapsed([200, 200], [60, 60], [true, true], 400), Assert.False);
        Assert.All(RiceRibbonGroupsPanel.ChooseCollapsed([200, 200], [60, 60], [true, true], double.PositiveInfinity), Assert.False);
    }

    [Fact]
    public void Groups_that_cannot_collapse_or_would_not_get_narrower_are_skipped()
    {
        var collapse = RiceRibbonGroupsPanel.ChooseCollapsed([200, 200, 50, 200], [60, 60, 60, 60], [true, true, true, false], 520);

        // 650: the last cannot collapse, the third is narrower expanded, so the second collapses (510).
        Assert.Equal([false, true, false, false], collapse);
    }

    [Fact]
    public void Large_items_get_a_column_and_the_others_stack_by_three()
    {
        var items = new List<(bool, Size)>
        {
            (true, new Size(48, 70)),
            (false, new Size(60, 24)),
            (false, new Size(80, 24)),
            (false, new Size(70, 24)),
            (false, new Size(50, 24)),
            (true, new Size(40, 70)),
            (false, new Size(30, 24)),
        };

        var columns = RiceRibbonGroupPanel.BuildColumns(items, 78);

        Assert.Equal(5, columns.Count);
        Assert.Equal((0, 1, true, 48.0), (columns[0].First, columns[0].Count, columns[0].IsLarge, columns[0].Width));
        Assert.Equal((1, 3, false, 80.0), (columns[1].First, columns[1].Count, columns[1].IsLarge, columns[1].Width));
        Assert.Equal((4, 1, false), (columns[2].First, columns[2].Count, columns[2].IsLarge));
        Assert.Equal((5, 1, true), (columns[3].First, columns[3].Count, columns[3].IsLarge));
        Assert.Equal((6, 1, false), (columns[4].First, columns[4].Count, columns[4].IsLarge));
    }

    [Fact]
    public void Tall_items_start_a_new_column_when_the_height_is_used_up()
    {
        // Three 32 px check boxes do not fit into 78 px.
        var items = Enumerable.Repeat((false, new Size(90, 32)), 3).ToList();

        var columns = RiceRibbonGroupPanel.BuildColumns(items, 78);

        Assert.Equal([2, 1], columns.Select(c => c.Count));
    }
}
