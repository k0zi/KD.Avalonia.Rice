using KD.Avalonia.Rice.Controls.Uml;
using KD.Avalonia.Rice.Demo;

namespace KD.Avalonia.Rice.Tests;

public class PlantUmlTests
{
    public static TheoryData<string> SampleNames => new(UmlSamples.All.Select(s => s.Name));

    private static string Source(string name) => UmlSamples.All.Single(s => s.Name == name).Source;

    [Theory]
    [InlineData("Class", UmlDiagramKind.Class)]
    [InlineData("Object", UmlDiagramKind.Object)]
    [InlineData("Package", UmlDiagramKind.Package)]
    [InlineData("Component", UmlDiagramKind.Component)]
    [InlineData("Composite structure", UmlDiagramKind.CompositeStructure)]
    [InlineData("Deployment", UmlDiagramKind.Deployment)]
    [InlineData("Profile", UmlDiagramKind.Profile)]
    [InlineData("Use case", UmlDiagramKind.UseCase)]
    [InlineData("Activity", UmlDiagramKind.Activity)]
    [InlineData("State", UmlDiagramKind.State)]
    [InlineData("Sequence", UmlDiagramKind.Sequence)]
    [InlineData("Communication", UmlDiagramKind.Communication)]
    [InlineData("Timing", UmlDiagramKind.Timing)]
    [InlineData("Interaction overview", UmlDiagramKind.InteractionOverview)]
    public void Samples_are_recognized_without_problems(string name, UmlDiagramKind kind)
    {
        var document = UmlDocument.Parse(Source(name));

        Assert.DoesNotContain(document.Diagnostics, d => d.Severity != UmlDiagnosticSeverity.Info);
        var diagram = Assert.Single(document.Diagrams);
        Assert.Equal(kind, diagram.Kind);
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void Writing_and_reading_back_keeps_the_diagram(string name)
    {
        var first = UmlDocument.Parse(Source(name)).Diagrams[0];
        var written = first.ToPlantUml();
        var reread = UmlDocument.Parse(written);

        Assert.DoesNotContain(reread.Diagnostics, d => d.Severity != UmlDiagnosticSeverity.Info);
        var second = Assert.Single(reread.Diagrams);
        Assert.Equal(first.Kind, second.Kind);
        Assert.Equal(written, second.ToPlantUml());
    }

    [Fact]
    public void Class_diagram_reads_members_relations_and_notes()
    {
        var diagram = Assert.IsType<UmlGraphDiagram>(UmlDocument.Parse(Source("Class")).Diagrams[0]);

        Assert.Equal("Online shop", diagram.Title);
        var product = diagram.Find("Product")!;
        Assert.Equal(UmlElementKind.AbstractClass, product.Kind);
        Assert.Equal(3, product.Members.Count);
        Assert.Equal(UmlVisibility.Private, product.Members[0].Visibility);
        Assert.Equal(UmlVisibility.Protected, product.Members[1].Visibility);
        Assert.True(product.Members[2].IsAbstract);
        Assert.True(product.Members[2].IsMethod);
        Assert.True(diagram.Find("Order")!.Members[2].IsStatic);
        Assert.Equal(UmlElementKind.Interface, diagram.Find("IShippable")!.Kind);
        Assert.Equal(UmlElementKind.Enum, diagram.Find("OrderState")!.Kind);

        var inheritance = diagram.Relations.Single(r => r.From == "Product" && r.To == "Book");
        Assert.Equal(UmlRelationKind.Generalization, inheritance.Kind);
        Assert.Equal(UmlRelationKind.Realization, diagram.Relations.Single(r => r.From == "IShippable").Kind);

        var composition = diagram.Relations.Single(r => r.From == "Order" && r.To == "OrderLine");
        Assert.Equal(UmlRelationKind.Composition, composition.Kind);
        Assert.Equal("1", composition.FromLabel);
        Assert.Equal("1..*", composition.ToLabel);
        Assert.Equal("contains", composition.Label);

        var places = diagram.Relations.Single(r => r.From == "Customer");
        Assert.Equal(UmlRelationKind.Aggregation, places.Kind);
        Assert.Equal(UmlLabelDirection.Forward, places.LabelDirection);
        Assert.Equal("places", places.Label);

        var note = diagram.Elements.Single(e => e.Kind == UmlElementKind.Note);
        Assert.Equal("Totals are cached\nuntil a line changes.", note.Text);
        var link = diagram.Relations.Single(r => r.To == note.Id);
        Assert.Equal(("Order", UmlLinkDirection.Right), (link.From, link.Direction));
    }

    [Fact]
    public void Containers_hold_their_content()
    {
        var diagram = Assert.IsType<UmlGraphDiagram>(UmlDocument.Parse(Source("Deployment")).Diagrams[0]);

        var web = diagram.Find("web")!;
        Assert.Equal(UmlElementKind.Node, web.Kind);
        Assert.Equal("Web server", web.Name);
        Assert.Contains("device", web.Stereotypes);
        Assert.Equal(UmlElementKind.Artifact, Assert.Single(web.Children).Kind);
        Assert.Equal(UmlElementKind.Database, diagram.Find("db")!.Kind);
    }

    [Fact]
    public void Ports_and_lollipops_in_component_diagrams()
    {
        var diagram = Assert.IsType<UmlGraphDiagram>(UmlDocument.Parse(Source("Component")).Diagrams[0]);

        Assert.Equal(UmlElementKind.InterfaceCircle, diagram.Find("REST")!.Kind);
        var gateway = diagram.Find("GW")!;
        Assert.Equal([UmlElementKind.PortIn, UmlElementKind.PortOut], gateway.Children.Select(c => c.Kind));
        Assert.Equal(1, diagram.Relations.Single(r => r.From == "REST").Length);
    }

    [Fact]
    public void State_diagram_has_scoped_pseudo_states()
    {
        var diagram = Assert.IsType<UmlGraphDiagram>(UmlDocument.Parse(Source("State")).Diagrams[0]);

        var processing = diagram.Find("Processing")!;
        Assert.Contains(processing.Children, c => c.Kind == UmlElementKind.InitialState);
        Assert.Contains(processing.Children, c => c.Kind == UmlElementKind.FinalState);
        Assert.Contains(diagram.Elements, e => e.Kind == UmlElementKind.InitialState);
        Assert.Equal("entry / reset timer", Assert.Single(diagram.Find("Idle")!.Members).Text);
        Assert.Equal("error [retries > 3]", diagram.Relations.Single(r => r.To == "Cancelled").Label);
    }

    [Fact]
    public void Sequence_diagram_reads_participants_activations_and_fragments()
    {
        var diagram = Assert.IsType<UmlSequenceDiagram>(UmlDocument.Parse(Source("Sequence")).Diagrams[0]);

        Assert.True(diagram.AutoNumber);
        Assert.Equal(["User", "Web", "Orders", "DB"], diagram.Participants.Select(p => p.Id));
        Assert.Equal(UmlParticipantKind.Actor, diagram.Participants[0].Kind);
        Assert.Equal(UmlParticipantKind.Database, diagram.Participants[3].Kind);

        var messages = diagram.Events.OfType<UmlMessage>().ToList();
        Assert.True(messages[1].ActivateTarget);
        Assert.Equal(UmlMessageKind.Reply, messages[3].Kind);
        Assert.True(messages[4].IsSelf);
        Assert.Single(diagram.Events.OfType<UmlFragmentStart>());
        Assert.Single(diagram.Events.OfType<UmlFragmentElse>());
        var note = Assert.Single(diagram.Events.OfType<UmlSequenceNote>());
        Assert.Equal(["User", "Web"], note.Participants);
    }

    [Fact]
    public void Return_answers_the_last_activated_call()
    {
        var diagram = Assert.IsType<UmlSequenceDiagram>(UmlDocument.Parse("""
            @startuml
            A -> B ++ : request
            return result
            @enduml
            """).Diagrams[0]);

        var reply = diagram.Events.OfType<UmlMessage>().Last();
        Assert.Equal(("B", "A", "result", UmlMessageKind.Reply), (reply.From, reply.To, reply.Text, reply.Kind));
        Assert.True(reply.DeactivateSource);
    }

    [Fact]
    public void Activity_diagram_keeps_its_structure()
    {
        var diagram = Assert.IsType<UmlActivityDiagram>(UmlDocument.Parse(Source("Activity")).Diagrams[0]);

        var conditional = Assert.Single(diagram.Statements.OfType<UmlActivityIf>());
        Assert.Equal(2, conditional.Branches.Count);
        Assert.Equal("Items in stock?", conditional.Branches[0].Condition);
        Assert.Equal("yes", conditional.Branches[0].Label);
        Assert.Null(conditional.Branches[1].Condition);
        var fork = Assert.Single(conditional.Branches[0].Statements.OfType<UmlActivityFork>());
        Assert.Equal(2, fork.Branches.Count);
        Assert.Equal(["Customer", "Shop", "Customer"], diagram.Statements.OfType<UmlActivityLane>().Select(l => l.Name));
    }

    [Fact]
    public void Interaction_overview_reads_references()
    {
        var diagram = Assert.IsType<UmlActivityDiagram>(UmlDocument.Parse(Source("Interaction overview")).Diagrams[0]);

        Assert.Equal("Authenticate", Assert.IsType<UmlActivityReference>(diagram.Statements[1]).Name);
    }

    [Fact]
    public void Communication_messages_have_directions()
    {
        var diagram = Assert.IsType<UmlGraphDiagram>(UmlDocument.Parse(Source("Communication")).Diagrams[0]);

        var link = diagram.Relations.Single(r => r.To == "s");
        Assert.Equal([new UmlLinkMessage("2: reserve()"), new UmlLinkMessage("3: ok", UmlLabelDirection.Backward)], link.Messages);
        Assert.Null(link.Label);
    }

    [Fact]
    public void Timing_diagram_reads_lifelines_changes_and_messages()
    {
        var diagram = Assert.IsType<UmlTimingDiagram>(UmlDocument.Parse(Source("Timing")).Diagrams[0]);

        Assert.Equal([UmlTimingLifelineKind.Robust, UmlTimingLifelineKind.Concise, UmlTimingLifelineKind.Clock, UmlTimingLifelineKind.Binary],
            diagram.Lifelines.Select(l => l.Kind));
        Assert.Equal(50, diagram.Find("clk")!.ClockPeriod);
        Assert.Equal(["Idle", "Waiting", "Processing"], diagram.Find("WB")!.States);
        Assert.Contains(new UmlTimingChange("WB", 300, "Waiting"), diagram.Changes);
        Assert.Equal(new UmlTimingMessage("WB", 100, "U", 300, "response"), Assert.Single(diagram.Messages));
    }

    [Fact]
    public void Multiple_blocks_give_multiple_diagrams()
    {
        var document = UmlDocument.Parse("@startuml\nclass A\n@enduml\n@startuml second\nA -> B\n@enduml");

        Assert.Equal(2, document.Diagrams.Count);
        Assert.Equal("second", document.Diagrams[1].Name);
        Assert.Equal(UmlDiagramKind.Sequence, document.Diagrams[1].Kind);
    }

    [Theory]
    [InlineData("@startuml\nclass A {\n+x\n@enduml")]
    [InlineData("@startuml\nthis is not plantuml at all\n@enduml")]
    [InlineData("@startuml\nalt\nA -> B\n@enduml")]
    [InlineData("@startuml\nstart\nif (x) then\n:a;\n@enduml")]
    [InlineData("@startmindmap\n* root\n@endmindmap")]
    [InlineData("@startuml\n!include foo.puml\nclass A\n@enduml")]
    [InlineData("")]
    public void Bad_input_gives_diagnostics_instead_of_exceptions(string text)
    {
        var document = UmlDocument.Parse(text);

        if (text.Length > 0)
            Assert.NotEmpty(document.Diagnostics);
    }

    [Fact]
    public void Diagnostics_carry_line_numbers()
    {
        var document = UmlDocument.Parse("@startuml\nclass A\nfoo bar baz\n@enduml");

        var warning = Assert.Single(document.Diagnostics);
        Assert.Equal(3, warning.Line);
    }

    [Fact]
    public void Kind_comment_is_written_only_when_needed()
    {
        Assert.DoesNotContain("rice:kind", UmlDocument.Parse(Source("Class")).Diagrams[0].ToPlantUml());
        Assert.Contains("' rice:kind=communication", UmlDocument.Parse(Source("Communication")).Diagrams[0].ToPlantUml());
    }

    [Fact]
    public void Diagrams_built_in_code_are_written_as_plantuml()
    {
        var diagram = new UmlGraphDiagram();
        diagram.Add(new UmlElement(UmlElementKind.Class, "Animal")).AddMember("name : string", UmlVisibility.Public);
        diagram.Add(new UmlElement(UmlElementKind.Class, "Dog"));
        diagram.Connect(UmlRelationKind.Generalization, "Dog", "Animal");

        var text = diagram.ToPlantUml();

        Assert.Contains("class Animal {", text);
        Assert.Contains("+name : string", text);
        Assert.Contains("Dog --|> Animal", text);
        var reread = Assert.IsType<UmlGraphDiagram>(UmlDocument.Parse(text).Diagrams[0]);
        Assert.Equal(UmlRelationKind.Generalization, Assert.Single(reread.Relations).Kind);
    }
}
