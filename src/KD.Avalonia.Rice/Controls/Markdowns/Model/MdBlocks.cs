namespace KD.Avalonia.Rice.Controls.Markdowns;

/// <summary>A block of a <see cref="MarkdownDocument"/>: paragraph, heading, list, code block, table, …</summary>
public abstract class MdBlock
{
    /// <summary>First line of the block in the source, 1-based.</summary>
    public int Line { get; internal set; }
}

/// <summary>A block holding other blocks: block quote, list item, footnote definition.</summary>
public abstract class MdContainerBlock : MdBlock
{
    public List<MdBlock> Children { get; } = [];
}

/// <summary>A block whose content is text with formatting: paragraph, heading.</summary>
public abstract class MdLeafBlock : MdBlock
{
    public List<MdInline> Inlines { get; } = [];

    /// <summary>The text without formatting.</summary>
    public string PlainText => MdInline.PlainTextOf(Inlines);
}

public sealed class MdParagraph : MdLeafBlock;

public sealed class MdHeading : MdLeafBlock
{
    /// <summary>1 – 6.</summary>
    public int Level { get; internal set; }

    /// <summary>Anchor id as GitHub makes it (<c>## Getting started</c> → <c>getting-started</c>); unique in the document.</summary>
    public string Slug { get; internal set; } = "";
}

public sealed class MdQuote : MdContainerBlock;

public sealed class MdList : MdBlock
{
    public bool IsOrdered { get; internal set; }

    /// <summary>Number of the first item of an ordered list.</summary>
    public int Start { get; internal set; } = 1;

    /// <summary>Tight lists have no blank lines between their items; their paragraphs are drawn without spacing.</summary>
    public bool IsTight { get; internal set; } = true;

    public List<MdListItem> Items { get; } = [];
}

public sealed class MdListItem : MdContainerBlock
{
    /// <summary>GFM task list item (<c>- [ ] todo</c>, <c>- [x] done</c>).</summary>
    public bool IsTask { get; internal set; }

    public bool IsChecked { get; internal set; }
}

public sealed class MdCodeBlock : MdBlock
{
    /// <summary>The whole info string of a fenced block (<c>```csharp title</c> → <c>csharp title</c>).</summary>
    public string? Info { get; internal set; }

    /// <summary>First word of <see cref="Info"/>, lower case; empty when none.</summary>
    public string Language { get; internal set; } = "";

    public string Code { get; internal set; } = "";

    public bool IsFenced { get; internal set; }
}

public sealed class MdThematicBreak : MdBlock;

/// <summary>Raw HTML block; HTML is not rendered: the text of it is shown, images and line breaks are honored.</summary>
public sealed class MdHtmlBlock : MdBlock
{
    public string Html { get; internal set; } = "";
}

public enum MdTableAlignment
{
    None,
    Left,
    Center,
    Right,
}

/// <summary>GFM table; <see cref="Rows"/>[0] is the header row.</summary>
public sealed class MdTable : MdBlock
{
    public List<MdTableAlignment> Alignments { get; } = [];

    public List<MdTableRow> Rows { get; } = [];
}

public sealed class MdTableRow
{
    public bool IsHeader { get; internal set; }

    /// <summary>The cells; every row has as many as the table has columns.</summary>
    public List<List<MdInline>> Cells { get; } = [];
}

/// <summary>GFM footnote (<c>[^note]: text</c>), numbered in the order of the first reference.</summary>
public sealed class MdFootnoteDefinition : MdContainerBlock
{
    public string Label { get; internal set; } = "";

    /// <summary>1-based; 0 while not referenced (not shown).</summary>
    public int Number { get; internal set; }
}
