using System.Reflection;

namespace KD.Avalonia.Rice.Demo;

/// <summary>Markdown samples of the demo: every Markdown feature, every Mermaid diagram type, the repository README.</summary>
public static class MarkdownSamples
{
    public sealed record Sample(string Name, string Markdown)
    {
        public override string ToString() => Name;
    }

    public static IReadOnlyList<Sample> All { get; } =
    [
        new("Feature tour", FeatureTour),
        new("Mermaid gallery", MermaidGallery),
        new("PlantUML in Markdown", PlantUmlInMarkdown),
        new("README.md", Readme()),
    ];

    private static string Readme()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("README.md");
        if (stream is null)
            return "# README\n\nThe README of the repository is not embedded in this build.";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public const string FeatureTour = """
        # Markdown view

        `MarkdownView` shows **CommonMark** with the *GitHub* extensions, drawn in the colors of the current theme.
        Links are clickable: [Avalonia](https://avaloniaui.net "The Avalonia site"), <https://commonmark.org>, bare
        URLs like www.github.com and mail addresses like hello@example.com. Jump to the [table](#tables) or a
        footnote[^tour].

        ## Text

        Emphasis with *stars* or _underscores_, **strong**, ***both***, ~~strikethrough~~, `inline code` and
        backslash\
        line breaks. Entities: &copy; &rarr; &#x1F600;. A little HTML works too: <kbd>Ctrl</kbd> + <kbd>C</kbd>,
        H<sub>2</sub>O, x<sup>2</sup>, <mark>marked</mark>.

        > **Note** Quotes can hold any block,
        > - even lists
        >
        > > and nested quotes.

        ## Lists

        1. First
        2. Second, with a nested list
           - apple
           - pear
             - Williams
        3. Third

        - [x] Parse CommonMark
        - [x] GFM tables and task lists
        - [ ] World domination

        A loose list:

        - Item one, with a paragraph.

          And a second paragraph.

        - Item two.

        ## Tables

        | Feature        | Supported | Notes                          |
        |:---------------|:---------:|-------------------------------:|
        | Headings       |    yes    | with GitHub style anchors      |
        | Tables         |    yes    | left, center, right alignment  |
        | Code coloring  |    yes    | C#, XAML, JSON, JS, Python, …  |
        | Raw HTML       |  partial  | images, `<br>`, simple tags    |

        ## Code

        ```csharp
        // A MarkdownView in code
        var view = new MarkdownView { Markdown = File.ReadAllText("README.md") };
        view.LinkClicked += (_, e) => Console.WriteLine($"Clicked {e.Url}");
        ```

        ```xml
        <md:MarkdownView Markdown="{Binding Text}" BaseUri="avares://MyApp/Docs/" />
        ```

        ```json
        { "name": "Rice", "version": 1.2, "dark": true, "tags": ["avalonia", null] }
        ```

        ```bash
        # build and run the demo
        dotnet run --project samples/KD.Avalonia.Rice.Demo --configuration Release
        ```

            Indented code blocks work too.

        ## Images

        ![Rice icon](avares://KD.Avalonia.Rice.Demo/Assets/icon.png "An image from the app resources")
        ![Missing picture](does-not-exist.png)

        ---

        That's all. Back to the [top](#markdown-view).

        [^tour]: Footnotes are numbered in the order of their first reference and listed at the end.
        """;

    public const string MermaidGallery = """
        # Mermaid diagrams

        Code blocks with the `mermaid` language are drawn as diagrams. Hover a diagram for the buttons: copy the
        source, or open it in full size with zoom and pan (double click works too).

        ## Flowchart

        ```mermaid
        flowchart LR
            A[Start] --> B{Is it working?}
            B -- Yes --> C([Ship it])
            B -- No --> D[[Debug]]
            D --> E[(Logs)]
            E -.-> B
            C ==> F((Done))
            subgraph Release
                C
                F
            end
        ```

        ## Sequence diagram

        ```mermaid
        sequenceDiagram
            autonumber
            actor User
            participant App
            participant API as Web API
            User->>App: Click "Save"
            activate App
            App->>+API: POST /documents
            API-->>-App: 201 Created
            alt saved
                App-->>User: Show success
            else failed
                App-->>User: Show error
            end
            Note over App,API: Retries three times
            deactivate App
        ```

        ## Class diagram

        ```mermaid
        classDiagram
            class Animal {
                <<abstract>>
                +string Name
                +int Age
                +MakeSound()* void
            }
            class Dog {
                +Fetch() void
            }
            class Cat
            Animal <|-- Dog
            Animal <|-- Cat
            Dog "1" --> "*" Toy : plays with
        ```

        ## State diagram

        ```mermaid
        stateDiagram-v2
            [*] --> Idle
            Idle --> Loading : fetch
            Loading --> Ready : success
            Loading --> Error : failure
            Error --> Loading : retry
            Ready --> [*]
            state Loading {
                [*] --> Connecting
                Connecting --> Downloading
            }
        ```

        ## Entity relationship diagram

        ```mermaid
        erDiagram
            CUSTOMER ||--o{ ORDER : places
            ORDER ||--|{ LINE_ITEM : contains
            PRODUCT ||--o{ LINE_ITEM : "ordered in"
            CUSTOMER {
                int id PK
                string name
                string email "unique"
            }
            ORDER {
                int id PK
                int customerId FK
                date created
            }
        ```

        ## Pie chart

        ```mermaid
        pie showData
            title Time spent on the project
            "Coding" : 42
            "Testing" : 23
            "Meetings" : 15
            "Coffee" : 20
        ```

        ## Gantt chart

        ```mermaid
        gantt
            title Release plan
            dateFormat YYYY-MM-DD
            section Design
            Requirements      :done,    des1, 2026-01-05, 7d
            Architecture      :active,  des2, after des1, 5d
            section Build
            Parser            :         dev1, after des2, 10d
            Renderer          :crit,    dev2, after des2, 12d
            section Release
            Beta              :milestone, rel1, after dev2, 0d
            Docs              :         doc1, after dev1, 6d
        ```

        ## Mind map

        ```mermaid
        mindmap
          root((Rice))
            Themes
              Ubuntu
              Fedora
              Nord
            Controls
              Side menu
              Chat
              Diagrams
                UML
                Mermaid
            Docs
        ```
        """;

    public const string PlantUmlInMarkdown = """
        # PlantUML in Markdown

        A `plantuml` code block is drawn with the UML diagram view of the library:

        ```plantuml
        @startuml
        class Document {
          +Blocks : List<Block>
          +Parse(text : string) : Document
        }
        abstract class Block
        class Paragraph
        class Heading {
          +Level : int
        }
        Document *-- "many" Block
        Block <|-- Paragraph
        Block <|-- Heading
        @enduml
        ```

        The `@startuml` / `@enduml` lines may be left out:

        ```plantuml
        Alice -> Bob : Hello
        Bob --> Alice : Hi!
        ```
        """;
}
