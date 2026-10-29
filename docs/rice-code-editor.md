# RiceCodeEditor

Light code editor for snippets, configuration files and scripts: a `TextBox` with syntax colored text, line numbers and
the caret's line highlighted, plus the usual editor keys. The colors are those of the code blocks of the
[Markdown view](rice-markdown.md), from the same highlighter.

```xml
<rice:RiceCodeEditor Text="{Binding Script}" Language="py" Height="320" />

<rice:RiceCodeEditor Text="{Binding Settings}" Language="json" TabSize="2" ShowLineNumbers="False" />
```

`Language` takes the names of the Markdown info strings: `cs` / `csharp`, `xaml` / `xml` / `html` / `axaml`, `json`,
`js` / `ts`, `py`, `bash` / `sh`, `sql`, `yaml`, `plantuml`, `mermaid`. Other values (or `null`) show plain text.

It is a `TextBox` underneath: bind `Text`, use `CaretIndex`, `SelectionStart` / `SelectionEnd`, `SelectedText`,
`IsReadOnly`, `PlaceholderText`, `Undo()` / `Redo()`, `TextChanged` as usual. `AcceptsReturn` and `AcceptsTab` are on,
the text does not wrap (`TextWrapping="Wrap"` works too: wrapped rows get no line number), the font is monospace.

## Keys

| Key | Action |
|-----|--------|
| Tab | Spaces up to the next tab stop; with several lines selected, indents them. |
| Shift+Tab | Outdents the caret's line or the selected lines by one level. |
| Enter | New line with the indentation of the current one; one level more after `{`, `[`, `(`; between a pair of brackets the closing one goes to its own line. |
| Backspace | In the indentation (only spaces before the caret): back to the previous tab stop. |
| Ctrl+/ | Comments the lines out (at their smallest indentation), or removes the comment when every line has it. |
| Ctrl+Z | Undo; every command above is one step. |

The line comment comes from the language: `//` (C#, JavaScript), `#` (Python, Bash, YAML), `--` (SQL), `'` (PlantUML),
`%%` (Mermaid); XML and JSON have none. `LineComment` sets another one, e.g. `;` for INI files.
`ToggleLineComment()` does the same as Ctrl+/ from code (e.g. from a menu).

## Properties

| Property | Default | Description |
|----------|---------|-------------|
| `Language` | `null` | Language of the code (see above). |
| `ShowLineNumbers` | `true` | The line number gutter on the left; the caret's line number is brighter. |
| `HighlightCurrentLine` | `true` | A band behind the caret's line (hidden while text is selected). |
| `TabSize` | `4` | Width of a tab stop in spaces (1–16). |
| `ConvertTabsToSpaces` | `true` | Tab inserts spaces; `false`: a tab character, and indenting uses tabs. |
| `AutoIndent` | `true` | Enter keeps the indentation. |
| `LineComment` | `null` | Overrides the line comment of the language. |

The whole text is colored again on every change, which is quick up to a few thousand lines; for large files use a full
editor control such as AvaloniaEdit.

## Styling

The editor uses `RiceMarkdownCodeBrush` as background (like the Markdown code blocks), `RiceSelectedRowBrush` for the
selection (the selected text keeps its colors), `RiceHoverBrush` for the current line and the `RiceCode…Brush` resources
for the syntax colors (see [Getting started](getting-started.md)). The border takes the accent color while the editor
has the focus. Template parts: `PART_TextPresenter` (a `RiceCodeTextPresenter`), `PART_ScrollViewer`, `PART_Gutter`
(`RiceCodeGutter`), `PART_LineHighlight` (`RiceCodeLineHighlight`).
