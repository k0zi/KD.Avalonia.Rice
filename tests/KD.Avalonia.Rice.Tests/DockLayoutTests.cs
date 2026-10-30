using Avalonia;
using Avalonia.Layout;
using KD.Avalonia.Rice.Controls;

namespace KD.Avalonia.Rice.Tests;

public class DockLayoutTests
{
    private static RiceDockPane Pane(string name, DockSide dock, double size = 200, string? group = null) =>
        new() { Name = name, Dock = dock, Size = size, Group = group };

    // A compact picture of a tree: H[...] / V[...] for splits, (a,b) for tool groups, {a,b} for document groups.
    private static string Shape(DockNode node) => node switch
    {
        DockSplitNode s => (s.Orientation == Orientation.Horizontal ? "H[" : "V[") + string.Join(" ", s.Children.Select(Shape)) + "]",
        DockGroupNode { IsDocument: true } g => "{" + string.Join(",", g.Panes.Select(p => p.Name)) + "}",
        DockGroupNode g => "(" + string.Join(",", g.Panes.Select(p => p.Name)) + ")",
        _ => "?",
    };

    [Fact]
    public void Declared_panes_are_laid_out_like_a_dock_panel()
    {
        var model = DockModel.Build([
            Pane("explorer", DockSide.Left), Pane("output", DockSide.Bottom), Pane("props", DockSide.Right),
            Pane("doc", DockSide.Document)]);

        Assert.Equal("H[(explorer) V[H[{doc} (props)] (output)]]", Shape(model.Root));
        var root = (DockSplitNode)model.Root;
        Assert.Equal(DockLength.Pixel(200), root.Children[0].Size);
        Assert.True(root.Children[1].Size.IsStar);
    }

    [Fact]
    public void Panes_of_a_group_share_tabs_and_an_empty_document_area_is_kept()
    {
        var model = DockModel.Build([
            Pane("output", DockSide.Bottom, group: "b"), Pane("errors", DockSide.Bottom, group: "b"),
            Pane("props", DockSide.Right, group: "r"), Pane("outline", DockSide.Right, group: "r")]);

        Assert.Equal("V[H[{} (props,outline)] (output,errors)]", Shape(model.Root));
    }

    [Fact]
    public void A_group_left_empty_goes_away_and_its_split_dissolves()
    {
        var explorer = Pane("explorer", DockSide.Left);
        var model = DockModel.Build([explorer, Pane("doc", DockSide.Document)]);

        model.Remove(explorer);

        Assert.Equal("{doc}", Shape(model.Root));
        Assert.Null(model.Root.Parent);
    }

    [Fact]
    public void The_last_document_group_stays_when_its_last_document_closes()
    {
        var doc = Pane("doc", DockSide.Document);
        var model = DockModel.Build([Pane("explorer", DockSide.Left), doc]);

        model.Remove(doc);

        Assert.Equal("H[(explorer) {}]", Shape(model.Root));
    }

    [Fact]
    public void Docking_at_an_edge_joins_a_split_of_the_same_direction()
    {
        var model = DockModel.Build([Pane("explorer", DockSide.Left), Pane("doc", DockSide.Document)]);

        model.DockToEdge([Pane("props", DockSide.Right)], DockSide.Right, 300);
        model.DockToEdge([Pane("output", DockSide.Bottom)], DockSide.Bottom, 150);

        Assert.Equal("V[H[(explorer) {doc} (props)] (output)]", Shape(model.Root));
        Assert.Equal(DockLength.Pixel(150), ((DockSplitNode)model.Root).Children[1].Size);
    }

    [Fact]
    public void Docking_next_to_a_group_halves_it_or_splits_it_across()
    {
        var output = Pane("output", DockSide.Bottom, 160);
        var model = DockModel.Build([output, Pane("doc", DockSide.Document)]);
        var outputGroup = model.GroupOf(output)!;

        // Beside it in a vertical split: a new horizontal split in its place, keeping its size.
        model.DockNextTo([Pane("errors", DockSide.Bottom)], outputGroup, DockPosition.Right, false, 200);
        Assert.Equal("V[{doc} H[(output) (errors)]]", Shape(model.Root));
        Assert.Equal(DockLength.Pixel(160), ((DockSplitNode)model.Root).Children[1].Size);

        // Next to it in the same direction: the two share its room.
        model.DockNextTo([Pane("log", DockSide.Bottom)], outputGroup, DockPosition.Left, false, 200);
        Assert.Equal("V[{doc} H[(log) (output) (errors)]]", Shape(model.Root));
        Assert.Equal(DockLength.Star(0.5), outputGroup.Size);
    }

    [Fact]
    public void A_tool_window_beside_the_document_area_gets_its_own_size()
    {
        var model = DockModel.Build([Pane("doc1", DockSide.Document)]);
        model.DockNextTo([Pane("doc2", DockSide.Document)], model.MainDocumentGroup, DockPosition.Right, true, 0);

        var group = model.DockNextTo([Pane("tool", DockSide.Left)], model.DocumentRoot, DockPosition.Left, false, 250);

        // The document area stays one node, so its own split keeps its proportions.
        Assert.Equal("H[(tool) H[{doc1} {doc2}]]", Shape(model.Root));
        Assert.Equal(DockLength.Pixel(250), group.Size);
        Assert.Equal("H[{doc1} {doc2}]", Shape(model.DocumentRoot));
    }

    [Fact]
    public void Tabs_join_the_group()
    {
        var output = Pane("output", DockSide.Bottom);
        var model = DockModel.Build([output]);
        var errors = Pane("errors", DockSide.Bottom);

        model.DockNextTo([errors], model.GroupOf(output)!, DockPosition.Tab, false, 0);

        Assert.Equal([output, errors], model.GroupOf(output)!.Panes);
        Assert.Equal(errors, model.GroupOf(output)!.Selected);
    }

    [Theory]
    [InlineData(1000, new[] { 200.0, 795.0 })]
    [InlineData(300, new[] { 175.0, 120.0 })]
    public void Pixels_come_first_and_shrink_to_leave_the_stars_their_minimum(double total, double[] expected)
    {
        var lengths = DockModel.Lengths([DockLength.Pixel(200), DockLength.Star()], total - 5, 120);

        Assert.Equal(expected, lengths.Select(l => Math.Round(l)).ToArray());
    }

    [Fact]
    public void Without_stars_the_pixels_are_scaled_to_fill()
    {
        Assert.Equal([100.0, 300.0], DockModel.Lengths([DockLength.Pixel(50), DockLength.Pixel(150)], 400, 120));
    }

    [Fact]
    public void A_saved_tree_comes_back_without_the_panes_it_does_not_know()
    {
        var explorer = Pane("explorer", DockSide.Left);
        var output = Pane("output", DockSide.Bottom, group: "b");
        var errors = Pane("errors", DockSide.Bottom, group: "b");
        var doc = Pane("doc", DockSide.Document);
        var model = DockModel.Build([explorer, output, errors, doc]);
        var state = DockStateConverter.ToState(model.Root);

        // "explorer" is gone in this version: its group and split go too.
        var known = new[] { output, errors, doc };
        var used = new HashSet<RiceDockPane>();
        var root = DockStateConverter.FromState(state, id => known.FirstOrDefault(p => p.Name == id), used);

        Assert.Equal("V[{doc} (output,errors)]", Shape(root!));
        Assert.Equal(3, used.Count);
    }

    [Theory]
    [InlineData(0, 0, 200, 600, DockSide.Left)]
    [InlineData(0, 450, 1000, 150, DockSide.Bottom)]
    [InlineData(800, 0, 200, 450, DockSide.Right)]
    [InlineData(0, 0, 1000, 120, DockSide.Top)]
    public void Auto_hide_goes_to_the_edge_the_group_touches(double x, double y, double width, double height, DockSide side)
    {
        Assert.Equal(side, RiceDockLayout.NearestSide(new Rect(x, y, width, height), new Size(1000, 600)));
    }

    [Fact]
    public void Guides_have_a_compass_over_the_group_and_outer_arrows_for_documents()
    {
        var layout = new Rect(0, 0, 1000, 600);
        var group = new Rect(200, 0, 600, 400);

        Assert.Equal(4, DockGuides.Place(layout, null, false, null).Count);
        Assert.Equal(9, DockGuides.Place(layout, group, false, null).Count);
        var guides = DockGuides.Place(layout, group, true, group);
        Assert.Equal(13, guides.Count);

        var tab = DockGuides.HitTest(guides, group.Center);
        Assert.Equal((DockGuideKind.Group, DockPosition.Tab), (tab!.Value.Kind, tab.Value.Position));
        var left = guides.First(g => g is { Kind: DockGuideKind.Group, Position: DockPosition.Left });
        Assert.Equal(new Rect(200, 0, 300, 400), DockGuides.Preview(left, layout, group, null, 250));
        var edge = guides.First(g => g is { Kind: DockGuideKind.Edge, Position: DockPosition.Bottom });
        Assert.Equal(new Rect(0, 400, 1000, 200), DockGuides.Preview(edge, layout, group, null, 250));
    }
}
