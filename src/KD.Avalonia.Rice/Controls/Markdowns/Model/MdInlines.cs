using System.Text;

namespace KD.Avalonia.Rice.Controls.Markdowns;

/// <summary>Text level element of a paragraph, heading or table cell.</summary>
public abstract class MdInline
{
    public static string PlainTextOf(IEnumerable<MdInline> inlines)
    {
        var builder = new StringBuilder();
        Append(builder, inlines);
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, IEnumerable<MdInline> inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case MdText t:
                    builder.Append(t.Text);
                    break;
                case MdCode c:
                    builder.Append(c.Text);
                    break;
                case MdSoftBreak:
                    builder.Append(' ');
                    break;
                case MdLineBreak:
                    builder.Append('\n');
                    break;
                case MdContainerInline container:
                    Append(builder, container.Children);
                    break;
            }
        }
    }
}

public sealed class MdText(string text) : MdInline
{
    public string Text { get; internal set; } = text;

    public override string ToString() => Text;
}

/// <summary>Inline holding other inlines: emphasis, strong, strikethrough, link, image (its alt text).</summary>
public abstract class MdContainerInline : MdInline
{
    public List<MdInline> Children { get; } = [];
}

public sealed class MdEmphasis : MdContainerInline;

public sealed class MdStrong : MdContainerInline;

public sealed class MdStrikethrough : MdContainerInline;

/// <summary>Code span (<c>`code`</c>).</summary>
public sealed class MdCode(string text) : MdInline
{
    public string Text { get; } = text;
}

public sealed class MdLink : MdContainerInline
{
    public string Url { get; internal set; } = "";

    public string? Title { get; internal set; }

    /// <summary><c>&lt;https://…&gt;</c> or a bare URL / e-mail address (GFM).</summary>
    public bool IsAutolink { get; internal set; }
}

/// <summary>Image; <see cref="MdContainerInline.Children"/> is the alt text.</summary>
public sealed class MdImage : MdContainerInline
{
    public string Url { get; internal set; } = "";

    public string? Title { get; internal set; }

    public string AltText => PlainTextOf(Children);
}

/// <summary>Line break in the source that is shown as a space.</summary>
public sealed class MdSoftBreak : MdInline;

/// <summary>Hard line break (two spaces or a backslash at the end of the line).</summary>
public sealed class MdLineBreak : MdInline;

/// <summary>GFM footnote reference (<c>[^note]</c>).</summary>
public sealed class MdFootnoteReference(MdFootnoteDefinition definition) : MdInline
{
    public MdFootnoteDefinition Definition { get; } = definition;
}

/// <summary>Raw inline HTML tag; not rendered, except <c>&lt;br&gt;</c> and <c>&lt;img&gt;</c>.</summary>
public sealed class MdHtmlInline(string html) : MdInline
{
    public string Html { get; } = html;
}
