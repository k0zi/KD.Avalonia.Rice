using Avalonia;
using KD.Avalonia.Rice.Controls.Uml;
using KD.Avalonia.Rice.Demo;

namespace KD.Avalonia.Rice.Tests;

public class UmlLayoutTests
{
    /// <summary>Text size estimate, so the layout runs without a font backend.</summary>
    private sealed class FakeMeasurer : IUmlTextMeasurer
    {
        public Size Measure(string text, double fontSize, bool bold = false, bool italic = false)
        {
            var lines = text.Split('\n');
            return new Size(lines.Max(l => l.Length) * fontSize * (bold ? 0.6 : 0.55), lines.Length * fontSize * 1.3);
        }
    }

    private static readonly UmlStyle Style = new(new FakeMeasurer(), 13);

    public static TheoryData<string> SampleNames => new(UmlSamples.All.Select(s => s.Name));

    private static UmlDiagram Diagram(string name) =>
        UmlDocument.Parse(UmlSamples.All.Single(s => s.Name == name).Source).Diagrams[0];

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void Every_sample_is_laid_out_and_selectable(string name)
    {
        var scene = UmlSceneBuilder.Build(Diagram(name), Style);

        Assert.True(scene.Bounds.Width > 50 && scene.Bounds.Height > 30);
        Assert.NotEmpty(scene.Prims);
        Assert.NotEmpty(scene.Items);
        Assert.All(scene.Prims, p => Assert.True(p.Item < scene.Items.Count));
    }

    [Theory]
    [InlineData("Class")]
    [InlineData("Object")]
    [InlineData("Use case")]
    [InlineData("Deployment")]
    [InlineData("State")]
    [InlineData("Package")]
    public void Elements_of_one_level_do_not_overlap(string name)
    {
        var scene = UmlSceneBuilder.Build(Diagram(name), Style);
        var diagram = (UmlGraphDiagram)Diagram(name);
        var elements = scene.Items.Where(i => i.Model is UmlElement { IsPort: false }).ToList();
        var parents = new Dictionary<string, string?>();
        void Index(UmlElement e, string? parent)
        {
            parents[e.Id] = parent;
            foreach (var c in e.Children)
                Index(c, e.Id);
        }
        foreach (var e in diagram.Elements)
            Index(e, null);

        foreach (var a in elements)
            foreach (var b in elements)
            {
                var ea = (UmlElement)a.Model;
                var eb = (UmlElement)b.Model;
                if (a == b || parents[ea.Id] != parents[eb.Id])
                    continue;
                Assert.False(a.Bounds.Deflate(1).Intersects(b.Bounds.Deflate(1)), $"{ea.Id} overlaps {eb.Id}");
            }
    }

    [Fact]
    public void Children_are_inside_their_container()
    {
        var scene = UmlSceneBuilder.Build(Diagram("Deployment"), Style);
        Rect Of(string id) => scene.Items.First(i => i.Model is UmlElement e && e.Id == id).Bounds;

        Assert.True(Of("web").Contains(Of("war")));
        Assert.True(Of("app").Contains(Of("os")));
    }

    [Fact]
    public void Parents_of_generalizations_are_above_their_children()
    {
        var scene = UmlSceneBuilder.Build(Diagram("Class"), Style);
        Rect Of(string id) => scene.Items.First(i => i.Model is UmlElement e && e.Id == id).Bounds;

        Assert.True(Of("Product").Bottom < Of("Book").Top);
        Assert.True(Of("IShippable").Bottom < Of("Book").Top);
    }

    [Fact]
    public void Fixed_positions_are_kept()
    {
        var diagram = new UmlGraphDiagram();
        diagram.Add(new UmlElement(UmlElementKind.Class, "A"));
        diagram.Add(new UmlElement(UmlElementKind.Class, "B") { Position = new Point(400, 300) });
        diagram.Connect(UmlRelationKind.Association, "A", "B");

        var scene = UmlSceneBuilder.Build(diagram, Style);
        var b = scene.Items.First(i => i.Model is UmlElement { Id: "B" }).Bounds;

        Assert.Equal(new Point(400, 300), b.TopLeft);
    }

    [Fact]
    public void Edge_ends_touch_their_elements()
    {
        var scene = UmlSceneBuilder.Build(Diagram("Object"), Style);
        foreach (var edge in scene.Items.Where(i => i.IsEdge))
        {
            var from = scene.Items.First(i => ReferenceEquals(i.Model, edge.From)).Bounds.Inflate(2);
            var to = scene.Items.First(i => ReferenceEquals(i.Model, edge.To)).Bounds.Inflate(2);
            Assert.True(from.Contains(edge.Path![0]));
            Assert.True(to.Contains(edge.Path[^1]));
        }
    }

    [Fact]
    public void Sequence_messages_run_between_their_lifelines()
    {
        var scene = UmlSceneBuilder.Build(Diagram("Sequence"), Style);
        var header = scene.Items.Where(i => i.Model is UmlParticipant).GroupBy(i => (UmlParticipant)i.Model)
            .ToDictionary(g => g.Key.Id, g => g.First().Bounds.Center.X);

        Assert.True(header["User"] < header["Web"] && header["Web"] < header["Orders"] && header["Orders"] < header["DB"]);
        var messages = scene.Items.Where(i => i.Model is UmlMessage).ToList();
        Assert.Equal(8, messages.Count);
        // From top to bottom in the order of the events.
        Assert.True(messages.Zip(messages.Skip(1)).All(p => p.First.Path![0].Y < p.Second.Path![0].Y));
    }

    [Fact]
    public void Swimlanes_hold_their_steps()
    {
        var scene = UmlSceneBuilder.Build(Diagram("Activity"), Style);
        var steps = scene.Items.Where(i => i.Model is UmlActivityAction).ToDictionary(i => ((UmlActivityAction)i.Model).Text, i => i.Bounds);

        // Customer is the first (left) lane, Shop the second.
        Assert.True(steps["Check out"].Right <= steps["Reserve items"].Left);
        Assert.True(steps["Receive parcel"].Right <= steps["Ship order"].Left);
    }

    [Fact]
    public void Left_to_right_flows_horizontally()
    {
        var diagram = new UmlGraphDiagram { Direction = UmlDirection.LeftToRight };
        diagram.Add(new UmlElement(UmlElementKind.Class, "A"));
        diagram.Add(new UmlElement(UmlElementKind.Class, "B"));
        diagram.Connect(UmlRelationKind.DirectedAssociation, "A", "B");

        var scene = UmlSceneBuilder.Build(diagram, Style);
        Rect Of(string id) => scene.Items.First(i => i.Model is UmlElement e && e.Id == id).Bounds;

        Assert.True(Of("A").Right < Of("B").Left);
    }

    [Fact]
    public void Cycles_are_laid_out()
    {
        var diagram = (UmlGraphDiagram)UmlDocument.Parse("@startuml\nclass A\nA --> B\nB --> C\nC --> A\nA --> A\n@enduml").Diagrams[0];

        var scene = UmlSceneBuilder.Build(diagram, Style);

        Assert.Equal(4, scene.Items.Count(i => i.IsEdge));
    }
}
