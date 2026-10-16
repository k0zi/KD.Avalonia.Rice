using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace KD.Avalonia.Rice.Controls.Markdowns;

/// <summary>Formatting of an inline HTML element the renderer understands (<c>&lt;sup&gt;</c>, <c>&lt;kbd&gt;</c>, …).</summary>
internal sealed class MdHtmlSpan(string tag) : MdContainerInline
{
    public string Tag { get; } = tag;
}

/// <summary>
/// HTML is not rendered, but the common bits of README files are honored: images, line breaks, links and simple
/// formatting tags (<c>b</c>, <c>i</c>, <c>code</c>, <c>kbd</c>, <c>sup</c>, …); other tags are dropped, their text kept.
/// </summary>
internal static partial class MarkdownHtml
{
    private static readonly HashSet<string> FormattingTags =
        ["b", "strong", "i", "em", "s", "del", "strike", "u", "ins", "code", "kbd", "tt", "samp", "sup", "sub", "mark", "small", "a"];

    private static readonly HashSet<string> BlockTags =
        ["p", "div", "h1", "h2", "h3", "h4", "h5", "h6", "li", "tr", "table", "ul", "ol", "blockquote", "pre", "details", "summary", "section", "header", "footer", "dt", "dd", "hr"];

    [GeneratedRegex(@"<(/?)([A-Za-z][A-Za-z0-9-]*)((?:\s+[^>]*?)?)(/?)>|<!--[\s\S]*?-->|<![^>]*>|<\?[\s\S]*?\?>", RegexOptions.IgnoreCase)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"([A-Za-z_:][\w:.-]*)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s""'=<>`]+))")]
    private static partial Regex AttributeRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"align\s*=\s*[""']?center", RegexOptions.IgnoreCase)]
    private static partial Regex CenterRegex();

    public readonly record struct Tag(string Name, bool IsClosing, bool IsSelfClosing, IReadOnlyDictionary<string, string> Attributes);

    public static Tag? ParseTag(string html)
    {
        var m = TagRegex().Match(html);
        if (!m.Success || m.Groups[2].Length == 0)
            return null;
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match a in AttributeRegex().Matches(m.Groups[3].Value))
            attributes[a.Groups[1].Value] = WebUtility.HtmlDecode(a.Groups[2].Success ? a.Groups[2].Value : a.Groups[3].Success ? a.Groups[3].Value : a.Groups[4].Value);
        var name = m.Groups[2].Value.ToLowerInvariant();
        return new Tag(name, m.Groups[1].Length > 0, m.Groups[4].Length > 0 || name is "br" or "img" or "hr", attributes);
    }

    public static HtmlImage Image(Tag tag)
    {
        var image = new HtmlImage
        {
            Url = tag.Attributes.GetValueOrDefault("src") ?? "",
            Title = tag.Attributes.GetValueOrDefault("title"),
            Width = Dimension(tag.Attributes.GetValueOrDefault("width")),
            Height = Dimension(tag.Attributes.GetValueOrDefault("height")),
        };
        if (tag.Attributes.GetValueOrDefault("alt") is { Length: > 0 } alt)
            image.Children.Add(new MdText(alt));
        return image;
    }

    private static double? Dimension(string? value) =>
        value is not null && double.TryParse(value.TrimEnd('p', 'x'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) && d > 0 ? d : null;

    /// <summary>An image of an <c>&lt;img&gt;</c> tag, which may give its size.</summary>
    internal sealed class HtmlImage : MdContainerInline
    {
        public string Url { get; init; } = "";
        public string? Title { get; init; }
        public double? Width { get; init; }
        public double? Height { get; init; }
        public string AltText => MdInline.PlainTextOf(Children);
    }

    /// <summary>
    /// Pairs the opening and closing tags of a paragraph's inline HTML into spans; <c>&lt;br&gt;</c> and
    /// <c>&lt;img&gt;</c> become a line break and an image, unknown tags are dropped. The document is not changed.
    /// </summary>
    public static List<MdInline> Normalize(IReadOnlyList<MdInline> inlines)
    {
        if (!inlines.Any(i => i is MdHtmlInline))
            return inlines as List<MdInline> ?? inlines.ToList();
        var result = new List<MdInline>();
        for (var i = 0; i < inlines.Count; i++)
        {
            if (inlines[i] is not MdHtmlInline html)
            {
                result.Add(inlines[i]);
                continue;
            }
            if (ParseTag(html.Html) is not { } tag)
                continue;
            if (tag.Name == "br")
                result.Add(new MdLineBreak());
            else if (tag.Name == "img")
                result.Add(Image(tag));
            else if (!tag.IsClosing && !tag.IsSelfClosing && FormattingTags.Contains(tag.Name) && FindClose(inlines, i, tag.Name) is var close and > 0)
            {
                var children = Normalize(inlines.Skip(i + 1).Take(close - i - 1).ToList());
                result.Add(Span(tag, children));
                i = close;
            }
        }
        return result;
    }

    private static int FindClose(IReadOnlyList<MdInline> inlines, int open, string name)
    {
        var depth = 0;
        for (var j = open + 1; j < inlines.Count; j++)
        {
            if (inlines[j] is not MdHtmlInline h || ParseTag(h.Html) is not { } t || t.Name != name || t.IsSelfClosing)
                continue;
            if (!t.IsClosing)
                depth++;
            else if (depth-- == 0)
                return j;
        }
        return -1;
    }

    private static MdContainerInline Span(Tag tag, List<MdInline> children)
    {
        MdContainerInline span = tag.Name switch
        {
            "a" when tag.Attributes.GetValueOrDefault("href") is { } href => new MdLink { Url = href, Title = tag.Attributes.GetValueOrDefault("title") },
            "b" or "strong" => new MdStrong(),
            "i" or "em" => new MdEmphasis(),
            "s" or "del" or "strike" => new MdStrikethrough(),
            _ => new MdHtmlSpan(tag.Name),
        };
        span.Children.AddRange(children);
        return span;
    }

    /// <summary>The content of an HTML block as inlines: text, images, links and formatting; blocks become line breaks.</summary>
    public static (List<MdInline> Inlines, bool Centered) BlockToInlines(string html)
    {
        var flat = new List<MdInline>();
        var centered = CenterRegex().IsMatch(html);
        var position = 0;
        var inPre = false;
        foreach (Match m in TagRegex().Matches(html))
        {
            AddText(flat, html[position..m.Index], inPre);
            position = m.Index + m.Length;
            if (m.Groups[2].Length == 0)
                continue;
            var tag = ParseTag(m.Value)!.Value;
            if (tag.Name == "pre")
                inPre = !tag.IsClosing;
            if (tag.Name is "script" or "style")
            {
                // Their content is never text.
                if (!tag.IsClosing)
                {
                    var end = html.IndexOf("</" + tag.Name, position, StringComparison.OrdinalIgnoreCase);
                    position = end < 0 ? html.Length : end;
                }
                continue;
            }
            if (BlockTags.Contains(tag.Name) || tag.Name == "br")
            {
                if (flat.Count > 0 && flat[^1] is not MdLineBreak)
                    flat.Add(new MdLineBreak());
            }
            else
                flat.Add(new MdHtmlInline(m.Value));
        }
        AddText(flat, html[position..], inPre);
        while (flat.Count > 0 && flat[^1] is MdLineBreak)
            flat.RemoveAt(flat.Count - 1);
        while (flat.Count > 0 && flat[0] is MdLineBreak)
            flat.RemoveAt(0);
        return (Normalize(flat), centered);
    }

    private static void AddText(List<MdInline> output, string text, bool keepWhitespace)
    {
        if (text.Length == 0)
            return;
        text = WebUtility.HtmlDecode(text);
        if (!keepWhitespace)
        {
            text = WhitespaceRegex().Replace(text, " ");
            if (output.Count == 0 || output[^1] is MdLineBreak)
                text = text.TrimStart();
        }
        if (text.Length == 0 || (text == " " && output.Count > 0 && output[^1] is MdText { Text: [.., ' '] }))
            return;
        output.Add(new MdText(text));
    }

    /// <summary>Plain text of an HTML fragment.</summary>
    public static string ToPlainText(string html)
    {
        var builder = new StringBuilder();
        foreach (var inline in BlockToInlines(html).Inlines)
            builder.Append(inline is MdLineBreak ? "\n" : MdInline.PlainTextOf([inline]));
        return builder.ToString();
    }
}
