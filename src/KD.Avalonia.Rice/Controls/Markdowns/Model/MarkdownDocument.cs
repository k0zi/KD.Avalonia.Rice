using System.Text;

namespace KD.Avalonia.Rice.Controls.Markdowns;

/// <summary>Target of a link reference definition (<c>[label]: url "title"</c>).</summary>
public sealed record MdLinkDefinition(string Url, string? Title);

/// <summary>
/// A Markdown document parsed by the CommonMark rules with the GitHub (GFM) extensions: tables, task lists,
/// strikethrough, bare URL autolinks and footnotes. Reading never throws; any text is a valid document.
/// </summary>
public sealed class MarkdownDocument
{
    internal MarkdownDocument(List<MdBlock> blocks, Dictionary<string, MdLinkDefinition> links, List<MdFootnoteDefinition> footnotes)
    {
        Blocks = blocks;
        LinkDefinitions = links;
        Footnotes = footnotes;
        Headings = Descendants(blocks).OfType<MdHeading>().ToList();
    }

    public IReadOnlyList<MdBlock> Blocks { get; }

    /// <summary>Link reference definitions by normalized (case folded) label.</summary>
    public IReadOnlyDictionary<string, MdLinkDefinition> LinkDefinitions { get; }

    /// <summary>The referenced footnotes in the order of their numbers.</summary>
    public IReadOnlyList<MdFootnoteDefinition> Footnotes { get; }

    /// <summary>Every heading, also the ones in quotes and lists, in document order (e.g. for a table of contents).</summary>
    public IReadOnlyList<MdHeading> Headings { get; }

    public static MarkdownDocument Parse(string? markdown) => new BlockParser().Parse(markdown ?? "");

    public static MarkdownDocument Load(string path) => Parse(File.ReadAllText(path));

    public static async Task<MarkdownDocument> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        return Parse(await reader.ReadToEndAsync(cancellationToken));
    }

    private static IEnumerable<MdBlock> Descendants(IEnumerable<MdBlock> blocks)
    {
        foreach (var block in blocks)
        {
            yield return block;
            var children = block switch
            {
                MdContainerBlock c => c.Children,
                MdList l => l.Items.Cast<MdBlock>(),
                _ => [],
            };
            foreach (var child in Descendants(children))
                yield return child;
        }
    }
}
