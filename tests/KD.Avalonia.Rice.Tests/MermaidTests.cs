using System.Text.RegularExpressions;
using Avalonia;
using KD.Avalonia.Rice.Controls.Markdowns;
using KD.Avalonia.Rice.Controls.Uml;
using KD.Avalonia.Rice.Demo;

namespace KD.Avalonia.Rice.Tests;

public class MermaidTests
{
    private sealed class FakeMeasurer : IUmlTextMeasurer
    {
        public Size Measure(string text, double fontSize, bool bold = false, bool italic = false)
        {
            var lines = text.Split('\n');
            return new Size(lines.Max(l => l.Length) * fontSize * (bold ? 0.6 : 0.55), lines.Length * fontSize * 1.3);
        }
    }

    private static readonly UmlStyle Style = new(new FakeMeasurer(), 13);

    /// <summary>The mermaid blocks of the demo's gallery.</summary>
    public static TheoryData<string> GalleryBlocks => new(
        Regex.Matches(MarkdownSamples.MermaidGallery, "```mermaid\n(.*?)```", RegexOptions.Singleline).Select(m => m.Groups[1].Value));

    private static UmlScene Scene(object? diagram) => diagram switch
    {
        UmlDiagram uml => UmlSceneBuilder.Build(uml, Style),
        IUmlSceneSource source => source.Build(Style),
        _ => throw new InvalidOperationException("No diagram"),
    };

    [Theory]
    [MemberData(nameof(GalleryBlocks))]
    public void Gallery_diagrams_are_read_without_problems_and_laid_out(string source)
    {
        var document = MermaidDocument.Parse(source);

        Assert.Empty(document.Diagnostics);
        var scene = Scene(document.Diagram);
        Assert.True(scene.Bounds.Width > 50 && scene.Bounds.Height > 30);
        Assert.NotEmpty(scene.Items);
        Assert.All(scene.Prims, p => Assert.True(p.Item < scene.Items.Count));
    }

    [Theory]
    [InlineData("graph TD\nA-->B", MermaidDiagramKind.Flowchart)]
    [InlineData("flowchart LR\nA-->B", MermaidDiagramKind.Flowchart)]
    [InlineData("sequenceDiagram\nA->>B: hi", MermaidDiagramKind.Sequence)]
    [InlineData("classDiagram\nA <|-- B", MermaidDiagramKind.Class)]
    [InlineData("stateDiagram-v2\n[*] --> A", MermaidDiagramKind.State)]
    [InlineData("erDiagram\nA ||--o{ B : has", MermaidDiagramKind.EntityRelationship)]
    [InlineData("pie\n\"a\" : 1", MermaidDiagramKind.Pie)]
    [InlineData("gantt\nTask : 2026-01-01, 3d", MermaidDiagramKind.Gantt)]
    [InlineData("mindmap\n  root\n    child", MermaidDiagramKind.Mindmap)]
    [InlineData("%% comment\n---\ntitle: x\n---\ngraph TD\nA", MermaidDiagramKind.Unknown)]
    public void Diagram_type_is_detected(string source, MermaidDiagramKind kind)
    {
        // Front matter must come first; after a comment it is not front matter.
        Assert.Equal(kind, MermaidDocument.Parse(source).Kind);
    }

    [Fact]
    public void Front_matter_title_is_the_diagram_title()
    {
        var document = MermaidDocument.Parse("---\ntitle: My flow\n---\nflowchart TD\n  A --> B");

        Assert.Equal("My flow", Assert.IsType<MermaidFlowchart>(document.Diagram).Title);
    }

    [Fact]
    public void Flowchart_shapes_edges_and_subgraphs_are_read()
    {
        var chart = Assert.IsType<MermaidFlowchart>(MermaidDocument.Parse("""
            flowchart RL
                A[Box] --> B(Round) & C([Stadium])
                B -- yes --> D{Choice?}
                C -.->|maybe| E[(Data)]
                D ==> F((Circle)) --- G{{Hex}}
                H[/Lean/] --o I[\Back\] --x J[/Trap\]
                K>Flag] <--> L[["Sub"]]
                M ---> N
                subgraph S1 [Group one]
                    direction TB
                    A
                    subgraph inner
                        B
                    end
                end
                classDef red fill:#f00
                class A red
            """).Diagram);

        Assert.Equal(MermaidDirection.RightToLeft, chart.Direction);
        Assert.Equal(MermaidNodeShape.Stadium, chart.Find("C")!.Shape);
        Assert.Equal(MermaidNodeShape.Diamond, chart.Find("D")!.Shape);
        Assert.Equal("Choice?", chart.Find("D")!.Text);
        Assert.Equal(MermaidNodeShape.Cylinder, chart.Find("E")!.Shape);
        Assert.Equal(MermaidNodeShape.Circle, chart.Find("F")!.Shape);
        Assert.Equal(MermaidNodeShape.Hexagon, chart.Find("G")!.Shape);
        Assert.Equal(MermaidNodeShape.Parallelogram, chart.Find("H")!.Shape);
        Assert.Equal(MermaidNodeShape.ParallelogramAlt, chart.Find("I")!.Shape);
        Assert.Equal(MermaidNodeShape.Trapezoid, chart.Find("J")!.Shape);
        Assert.Equal(MermaidNodeShape.Asymmetric, chart.Find("K")!.Shape);
        Assert.Equal(MermaidNodeShape.Subroutine, chart.Find("L")!.Shape);
        Assert.Equal("Sub", chart.Find("L")!.Text);

        Assert.Equal(2, chart.Edges.Count(e => e.From == "A"));
        var yes = chart.Edges.Single(e => e.From == "B" && e.To == "D");
        Assert.Equal("yes", yes.Text);
        var maybe = chart.Edges.Single(e => e.From == "C");
        Assert.Equal(("maybe", MermaidLineStyle.Dotted), (maybe.Text, maybe.Line));
        Assert.Equal(MermaidLineStyle.Thick, chart.Edges.Single(e => e.From == "D").Line);
        Assert.Equal(MermaidEdgeEnd.None, chart.Edges.Single(e => e.From == "F").ToEnd);
        Assert.Equal(MermaidEdgeEnd.Circle, chart.Edges.Single(e => e.From == "H").ToEnd);
        Assert.Equal(MermaidEdgeEnd.Cross, chart.Edges.Single(e => e.From == "I").ToEnd);
        var both = chart.Edges.Single(e => e.From == "K");
        Assert.Equal((MermaidEdgeEnd.Arrow, MermaidEdgeEnd.Arrow), (both.FromEnd, both.ToEnd));
        Assert.Equal(2, chart.Edges.Single(e => e.From == "M").Length);

        var group = Assert.Single(chart.Subgraphs);
        Assert.Equal(("S1", "Group one", MermaidDirection.TopToBottom), (group.Id, group.Title, group.Direction));
        Assert.Equal(["A"], group.Nodes);
        Assert.Equal(["B"], Assert.Single(group.Subgraphs).Nodes);
    }

    [Fact]
    public void Sequence_messages_activations_fragments_and_notes()
    {
        var diagram = Assert.IsType<UmlSequenceDiagram>(MermaidDocument.Parse("""
            sequenceDiagram
                autonumber
                actor U as User
                U->>+S: request
                S-->>-U: response
                S-)Q: event
                loop every minute
                    S->>S: tick
                end
                rect rgb(0, 0, 0)
                    Note right of S: inside a rect
                end
                destroy Q
            """).Diagram);

        Assert.True(diagram.AutoNumber);
        Assert.Equal(("User", UmlParticipantKind.Actor), (diagram.Find("U")!.Name, diagram.Find("U")!.Kind));
        var messages = diagram.Events.OfType<UmlMessage>().ToList();
        Assert.True(messages[0].ActivateTarget);
        Assert.Equal(UmlMessageKind.Reply, messages[1].Kind);
        Assert.True(messages[1].DeactivateSource);
        Assert.Equal(UmlMessageKind.Asynchronous, messages[2].Kind);
        Assert.Single(diagram.Events.OfType<UmlFragmentStart>());
        Assert.Single(diagram.Events.OfType<UmlFragmentEnd>());
        Assert.Equal(UmlNotePosition.Right, Assert.Single(diagram.Events.OfType<UmlSequenceNote>()).Position);
        Assert.Equal(UmlActivationKind.Destroy, diagram.Events.OfType<UmlActivation>().Last().Kind);
    }

    [Fact]
    public void Class_members_annotations_and_relations()
    {
        var diagram = Assert.IsType<UmlGraphDiagram>(MermaidDocument.Parse("""
            classDiagram
                class Shape {
                    <<interface>>
                    +Area()$ double
                    -List~int~ points
                }
                class Box~T~
                Shape <|.. Circle
                Circle "1" *-- "many" Point : has
                Circle : +Radius double
                <<abstract>> Base
            """).Diagram);

        var shape = diagram.Find("Shape")!;
        Assert.Equal(UmlElementKind.Interface, shape.Kind);
        Assert.Equal("Area() : double", shape.Members[0].Text);
        Assert.True(shape.Members[0].IsStatic);
        Assert.Equal((UmlVisibility.Private, "List<int> points"), (shape.Members[1].Visibility, shape.Members[1].Text));
        Assert.Equal("Box<T>", diagram.Find("Box")!.Name);
        Assert.Equal(UmlElementKind.AbstractClass, diagram.Find("Base")!.Kind);
        var realization = diagram.Relations.Single(r => r.To == "Circle" && r.From == "Shape");
        Assert.Equal((UmlArrowHead.Triangle, UmlLineStyle.Dashed), (realization.FromHead, realization.LineStyle));
        var composition = diagram.Relations.Single(r => r.To == "Point");
        Assert.Equal(("1", "many", "has", UmlArrowHead.FilledDiamond), (composition.FromLabel, composition.ToLabel, composition.Label, composition.FromHead));
        Assert.Equal("Radius double", Assert.Single(diagram.Find("Circle")!.Members).Text);
    }

    [Fact]
    public void State_pseudo_states_are_per_composite_state()
    {
        var diagram = Assert.IsType<UmlGraphDiagram>(MermaidDocument.Parse("""
            stateDiagram-v2
                [*] --> Active
                state Active {
                    [*] --> Inner
                    Inner --> [*]
                }
                state check <<choice>>
                Active --> check : done
                check --> [*]
                note right of Active : a note
            """).Diagram);

        Assert.Equal(2, diagram.AllElements().Count(e => e.Kind == UmlElementKind.InitialState));
        Assert.Equal(2, diagram.AllElements().Count(e => e.Kind == UmlElementKind.FinalState));
        Assert.Equal(2, diagram.Find("Active")!.Children.Count(c => c.IsPseudoState) + 0);
        Assert.Equal(UmlElementKind.Choice, diagram.Find("check")!.Kind);
        Assert.Single(diagram.AllElements(), e => e.Kind == UmlElementKind.Note);
        // Transitions are solid lines.
        Assert.All(diagram.Relations.Where(r => r.ToHead == UmlArrowHead.Arrow), r => Assert.Equal(UmlLineStyle.Solid, r.LineStyle));
        var scene = UmlSceneBuilder.Build(diagram, Style);
        Assert.DoesNotContain(scene.Prims.OfType<LinePrim>(), l => l.Style == UmlStroke.Dashed && l.Brush == UmlBrush.Line && l.Thickness > 1);
    }

    [Fact]
    public void Er_entities_attributes_and_cardinalities()
    {
        var diagram = Assert.IsType<MermaidErDiagram>(MermaidDocument.Parse("""
            erDiagram
                CUSTOMER ||--o{ ORDER : places
                ORDER }|..|{ ITEM : "has many"
                CUSTOMER {
                    string id PK, FK "the key"
                    varchar(32) name
                }
            """).Diagram);

        var places = diagram.Relationships[0];
        Assert.Equal((MermaidCardinality.ExactlyOne, MermaidCardinality.ZeroOrMore, true), (places.FromCardinality, places.ToCardinality, places.IsIdentifying));
        var has = diagram.Relationships[1];
        Assert.Equal((MermaidCardinality.OneOrMore, MermaidCardinality.OneOrMore, false, "has many"), (has.FromCardinality, has.ToCardinality, has.IsIdentifying, has.Label));
        var id = diagram.Entities[0].Attributes[0];
        Assert.Equal(("string", "id", "the key"), (id.Type, id.Name, id.Comment));
        Assert.Equal(["PK", "FK"], id.Keys);
        Assert.Equal("varchar(32)", diagram.Entities[0].Attributes[1].Type);
    }

    [Fact]
    public void Gantt_dates_durations_and_dependencies()
    {
        var gantt = Assert.IsType<MermaidGantt>(MermaidDocument.Parse("""
            gantt
                dateFormat DD.MM.YYYY
                axisFormat %d %b
                section A
                First  :done, a1, 05.01.2026, 3d
                Second :crit, a2, after a1, 1w
                Third  :24h
                Gate   :milestone, until a2
            """).Diagram);

        var tasks = gantt.Sections.Single().Tasks;
        Assert.Equal(new DateTime(2026, 1, 5), tasks[0].Start);
        Assert.Equal(new DateTime(2026, 1, 8), tasks[0].End);
        Assert.True(tasks[0].IsDone);
        Assert.Equal((new DateTime(2026, 1, 8), new DateTime(2026, 1, 15), true), (tasks[1].Start, tasks[1].End, tasks[1].IsCritical));
        Assert.Equal((new DateTime(2026, 1, 15), new DateTime(2026, 1, 16)), (tasks[2].Start, tasks[2].End));
        Assert.True(tasks[3].IsMilestone);
        Assert.Equal("dd MMM", gantt.AxisFormat);
    }

    [Fact]
    public void Pie_and_mindmap()
    {
        var pie = Assert.IsType<MermaidPie>(MermaidDocument.Parse("pie showData title Pets\n\"Dogs\" : 386\n\"Cats\" : 85.5").Diagram);
        Assert.Equal(("Pets", true, 2), (pie.Title, pie.ShowData, pie.Slices.Count));
        Assert.Equal(85.5, pie.Slices[1].Value);

        var map = Assert.IsType<MermaidMindmap>(MermaidDocument.Parse("mindmap\n  root((Center))\n    A[Square]\n      A1\n    B)Cloud(\n    C{{Hex}}").Diagram);
        Assert.Equal(("Center", MermaidMindmapShape.Circle), (map.Root!.Text, map.Root.Shape));
        Assert.Equal([MermaidMindmapShape.Square, MermaidMindmapShape.Cloud, MermaidMindmapShape.Hexagon], map.Root.Children.Select(c => c.Shape));
        Assert.Equal("A1", Assert.Single(map.Root.Children[0].Children).Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("flowchart")]
    [InlineData("flowchart TD\nA -->\nsubgraph\nend\nend\n-->")]
    [InlineData("sequenceDiagram\nend\nNote over : x\n->>")]
    [InlineData("classDiagram\nclass {\n}")]
    [InlineData("stateDiagram\n}\n[*]")]
    [InlineData("erDiagram\nA {\nbad attribute line here\n")]
    [InlineData("gantt\nx : after nothing, until nobody\n:")]
    [InlineData("pie\n\"a\" : -1\nbad")]
    [InlineData("mindmap")]
    public void Bad_input_gives_diagnostics_and_never_throws(string source)
    {
        var document = MermaidDocument.Parse(source);

        if (document.Diagram is not null)
            Scene(document.Diagram);
        Assert.NotNull(document.Diagnostics);
    }
}
