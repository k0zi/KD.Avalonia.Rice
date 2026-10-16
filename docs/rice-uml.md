# UML diagrams (UmlDiagramView)

`UmlDiagramView` draws all 14 UML diagram types in the colors of the current Rice theme: rounded boxes with soft
shadows, accent tinted headers, smooth lines. The elements are laid out automatically (no external layout engine);
pan by dragging or scrolling, zoom with Ctrl + wheel or the buttons, click an element to select it.

The diagrams are read from and written to **PlantUML** text (`.puml`), the de facto plain text format of UML, which
IDEs, editors, wikis and the PlantUML tool itself understand. A file saved by your app opens in any PlantUML tool, and
the other way round.

The controls and the model live in their own namespace:

```xml
xmlns:uml="clr-namespace:KD.Avalonia.Rice.Controls.Uml;assembly=KD.Avalonia.Rice"
```

```xml
<!-- from PlantUML text, e.g. the content of a .puml file -->
<uml:UmlDiagramView Source="{Binding PlantUml}" SelectedElement="{Binding Selected}" />
```

```csharp
// from a file
var document = UmlDocument.Load("my_app_class.puml");   // never throws on bad input
foreach (var problem in document.Diagnostics)
    Console.WriteLine(problem);                          // "12: Warning: Unrecognized line: …"
DiagramView.Diagram = document.Diagrams[0];

// built in code, saved as PlantUML
var diagram = new UmlGraphDiagram(UmlDiagramKind.Class);
diagram.Add(new UmlElement(UmlElementKind.Class, "Animal")).AddMember("name : string", UmlVisibility.Public);
diagram.Add(new UmlElement(UmlElementKind.Class, "Dog"));
diagram.Connect(UmlRelationKind.Generalization, "Dog", "Animal");
DiagramView.Diagram = diagram;
File.WriteAllText("animals.puml", diagram.ToPlantUml());
```

> Use the `.puml` extension: `.uml` files are usually XMI (the OMG exchange format) for tools like Papyrus or
> Enterprise Architect. The parser itself only looks at the content.

## Properties

| Property | Default | Description |
|----------|---------|-------------|
| `Source` | `null` | PlantUML text; setting it parses it and fills `Diagram`. While the text cannot be read (e.g. during editing) the last good diagram stays. |
| `DiagramIndex` | `0` | Which `@startuml` block of a multi-diagram `Source` is shown. |
| `Diagram` | `null` | The shown `UmlDiagram`; set it directly to show a model built in code. |
| `Document` | – | Read-only, the `UmlDocument` parsed from `Source`. |
| `Diagnostics` | – | Read-only, problems in `Source` with 1-based line numbers. |
| `ErrorText` | – | Read-only, the first problem; shown in the bottom left corner (`:error` pseudo class). |
| `Zoom` | `1` | Scale, two-way bindable; clamped between `MinZoom` (`0.1`) and `MaxZoom` (`4`). |
| `SelectedElement` | `null` | The selected model object (two-way): `UmlElement`, `UmlRelation`, `UmlParticipant`, `UmlMessage`, `UmlActivityStatement`, `UmlTimingLifeline`, … |
| `IsSelectionEnabled` | `true` | Click selection and hover highlight. |
| `ShowGrid` | `true` | Dot grid in the background. |
| `ShowZoomControls` | `true` | The zoom buttons in the bottom right corner. |
| `FontSize` | `13` | Base font size of the diagram texts. |

| Member | Description |
|--------|-------------|
| `SelectionChanged` | Routed event with `OldElement` and `NewElement`. |
| `ZoomIn()`, `ZoomOut()`, `ResetZoom()`, `ZoomToFit()` | Same as the buttons; fit never enlarges beyond 100 %. |
| `Refresh()` | Lays the diagram out again after the model was changed in place. |

Keyboard (when the view has focus): `+` / `-` zoom, `0` 100 %, `F` fit, arrows pan, `Esc` clears the selection.
Double click on an empty area fits the diagram.

A selected element is outlined in the accent color together with its lines; when the source is edited, the selection
moves to the element with the same id. `UmlDiagramCanvas` is the drawing surface without buttons and error badge.

## Diagram types and PlantUML syntax

PlantUML has no header for the diagram type, it guesses it from the content – and so does the parser. Four UML types
have no syntax of their own in PlantUML; they are written in the PlantUML form closest to them, with a comment that
PlantUML ignores, so other tools still show them and this library reads back the exact type:

```
' rice:kind=communication
```

| Type | Model | Recognized by | Supported syntax |
|------|-------|---------------|------------------|
| Class | `UmlGraphDiagram` | `class`, `abstract`, `enum`, `<\|--`, … | `class`/`abstract class`/`interface`/`enum`/`annotation`/`entity`, `"Name" as Id`, `<<stereotype>>`, generics, `extends`/`implements`, member bodies with `+ - # ~`, `{static}`, `{abstract}`, `--` separators, `Id : member`, packages/namespaces, notes |
| Object | `UmlGraphDiagram` | `object`, `map` | `object name { field = value }`, links |
| Package | `UmlGraphDiagram` | only `package`/`folder`/`frame`/… | nested `package X { }`, dependencies |
| Component | `UmlGraphDiagram` | `component`, `[Comp]`, `() I`, `port` | components, `interface` (lollipop), `port`/`portin`/`portout`, `database`, `node`, containers |
| Composite structure | `UmlGraphDiagram` | `' rice:kind=compositestructure` | component with parts (`rectangle "p : Part"`) and ports |
| Deployment | `UmlGraphDiagram` | `node`, `artifact`, `device`, … | nodes (3D boxes), artifacts, databases, clouds, folders, frames, nesting |
| Profile | `UmlGraphDiagram` | `metaclass`, `stereotype` | metaclasses, stereotypes with attributes; `Stereo --\|> Metaclass` is drawn as extension (filled triangle) |
| Use case | `UmlGraphDiagram` | `usecase`, `(Use case)`, `:Actor:` | actors, use cases, `rectangle` system boundary, `<<include>>`/`<<extend>>` |
| State | `UmlGraphDiagram` | `[*]`, `state` | `[*]`, composite `state X { }`, `<<choice>>`/`<<fork>>`/`<<join>>`, `[H]`/`[H*]`, entry/exit points, `S : entry / x`, transition labels |
| Communication | `UmlGraphDiagram` | `' rice:kind=communication` | objects and links; messages in the label, one per `\n`, direction `>` / `<`: `a -- b : 1: msg() >\n< 2: reply` |
| Activity | `UmlActivityDiagram` | `start`, `:action;`, `if (…) then`, `\|lane\|` | actions (multi-line), `if`/`elseif`/`else`, `while`, `repeat … repeat while`, `fork`/`fork again`/`end fork`/`end merge`, `split`, swimlanes, `-> label;`, notes, `stop`/`end`/`kill`/`detach` |
| Interaction overview | `UmlActivityDiagram` | `' rice:kind=interactionoverview` | activity syntax with `:ref Interaction;` steps |
| Sequence | `UmlSequenceDiagram` | `participant`, `->` messages, … | `participant`/`actor`/`boundary`/`control`/`entity`/`database`/`collections`/`queue`, `->` `->>` `-->`, `<-`, lost/found `[->` `->]`, `++` `--` `**` `!!`, `activate`/`deactivate`/`destroy`/`create`, `return`, `alt`/`else`/`opt`/`loop`/`par`/`break`/`critical`/`group`/`end`, `ref over`, notes (`left`/`right`/`over`), `== divider ==`, `...`, `\|\|\|`, `autonumber`, `hide footbox` |
| Timing | `UmlTimingDiagram` | `robust`/`concise`/`clock`/`binary`, `@time` | lifelines, `clock … with period P pulse W offset O`, `X has A,B,C`, `@100` / `@+50` / `@Id` blocks, `Id is State`, `A@t1 -> B@t2 : msg` |

Common commands: `title`, `left to right direction`, `top to bottom direction`, comments (`'`, `/' … '/`), several
`@startuml` blocks in one file. `skinparam`, `<style>`, `hide`/`show`, `scale`, headers and legends are accepted and
ignored – the theme does the styling. Preprocessor directives (`!include`, `!define`, `!theme`) are not supported and
give a warning.

## Layout

Graph diagrams use a layered layout: the main flow goes top to bottom (or left to right), generalization parents are
placed above their children, containers are laid out from the inside out, and parallel lines are spread apart. The
PlantUML line length is respected: `A - B` places the two side by side, `A --- B` stretches the line; the direction
words (`-up->`, `-left-`, …) and `-[hidden]-` work too. Sequence diagrams space the participants so every message
label fits; timing diagrams scale the time axis so every state name fits.

To fix an element in place, set `UmlElement.Position` (top-left, relative to its container); the rest is arranged
around it. `UmlElement.Size` overrides the measured size. PlantUML text has no positions, so these come from code.

## Colors

The diagram follows the theme and the light/dark switch. The brushes, all of them derived from the theme palette:

| Resource | Used for |
|----------|----------|
| `RiceUmlNodeBrush`, `RiceUmlNodeHeaderBrush`, `RiceUmlNodeBorderBrush` | Element background, header band (accent tint), border |
| `RiceUmlLineBrush` | Lines and arrow heads |
| `RiceUmlNoteBrush`, `RiceUmlNoteBorderBrush` | Notes |
| `RiceUmlContainerBrush`, `RiceUmlLaneBrush` | Packages, nodes, composite states (translucent, nested ones get darker); swimlanes |
| `RiceUmlSelectionBrush`, `RiceUmlHoverBrush`, `RiceUmlGridBrush` | Selection, hover, background dots |

Override them on the canvas, e.g. `<Style Selector="uml|UmlDiagramCanvas"><Setter Property="NodeShadow" Value="none" /></Style>`.

## Known limitations

- Display only: elements cannot be moved or connected with the mouse.
- Lines into nested elements (and to ports) are straight, they are not routed around other elements.
- PlantUML features outside UML are not supported: mind maps, Gantt charts, salt, JSON/YAML, ER (IE) diagrams,
  the legacy activity syntax, `!include` and the other preprocessor features.
- Concurrent regions of composite states (`--`, `||`) are drawn as one region; activity `partition` frames and inline
  sequence fragments of interaction overview diagrams are not drawn.
