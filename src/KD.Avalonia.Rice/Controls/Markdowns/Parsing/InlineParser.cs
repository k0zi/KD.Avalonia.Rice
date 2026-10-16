using System.Text;
using System.Text.RegularExpressions;

namespace KD.Avalonia.Rice.Controls.Markdowns;

/// <summary>
/// The inline phase of CommonMark: code spans, escapes, entities, autolinks, raw HTML, links and images with a
/// bracket stack, emphasis with the delimiter stack ("process emphasis"), plus the GFM strikethrough, bare URL
/// autolinks and footnote references.
/// </summary>
internal sealed partial class InlineParser(IReadOnlyDictionary<string, MdLinkDefinition> references, Func<string, MdFootnoteDefinition?> footnote)
{
    private sealed class Delimiter
    {
        public char Char { get; init; }
        public int Count { get; set; }
        public int OriginalCount { get; init; }
        public required MdText Node { get; init; }
        public Delimiter? Previous { get; set; }
        public Delimiter? Next { get; set; }
        public bool CanOpen { get; init; }
        public bool CanClose { get; init; }
    }

    private sealed class Bracket
    {
        public required MdText Node { get; init; }
        public Bracket? Previous { get; init; }
        public Delimiter? PreviousDelimiter { get; init; }
        /// <summary>Position after the opening bracket.</summary>
        public int Index { get; init; }
        public bool Image { get; init; }
        public bool Active { get; set; } = true;
        public bool BracketAfter { get; set; }
    }

    [GeneratedRegex(@"\G<([a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*)>")]
    private static partial Regex EmailAutolinkRegex();

    [GeneratedRegex(@"\G<([A-Za-z][A-Za-z0-9.+-]{1,31}:[^<>\x00-\x20]*)>")]
    private static partial Regex AutolinkRegex();

    [GeneratedRegex(@"\G<(?:[^<>\n\\\x00]|\\.)*>")]
    private static partial Regex DestinationBracesRegex();

    [GeneratedRegex(@"\G(?:""(?:\\[\s\S]|[^""\\\x00])*""|'(?:\\[\s\S]|[^'\\\x00])*'|\((?:\\[\s\S]|[^()\\\x00])*\))")]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"\G\[(?:[^\\\[\]]|\\[\s\S]){0,1000}\]")]
    private static partial Regex LinkLabelRegex();

    [GeneratedRegex(@"\G\[\^([^\]\s]+)\]")]
    private static partial Regex FootnoteReferenceRegex();

    [GeneratedRegex(@"\G[ \t]*(?:\n[ \t]*)?")]
    private static partial Regex SpnlRegex();

    [GeneratedRegex(@"\G[ \t]*(?:\n|$)")]
    private static partial Regex SpaceAtEndOfLineRegex();

    // GFM extended autolinks: www., http(s)://, ftp:// and e-mail addresses in plain text.
    [GeneratedRegex(@"(?<![A-Za-z0-9/@._+-])(?:(?:https?|ftp)://|www\.)[^\s<]+|(?<![A-Za-z0-9._+-])[A-Za-z0-9._+-]+@[A-Za-z0-9_-]+(?:\.[A-Za-z0-9_-]+)+")]
    private static partial Regex BareUrlRegex();

    private string _subject = "";
    private int _pos;
    private List<MdInline> _nodes = [];
    private Delimiter? _delimiters;
    private Bracket? _brackets;

    public List<MdInline> Parse(string text)
    {
        _subject = text;
        _pos = 0;
        _nodes = [];
        _delimiters = null;
        _brackets = null;
        while (_pos < _subject.Length)
            ParseInline();
        ProcessEmphasis(null);
        var result = _nodes;
        Finish(result, insideLink: false);
        return result;
    }

    private char Peek(int offset = 0) => _pos + offset < _subject.Length ? _subject[_pos + offset] : '\0';

    private MdText AddText(string text)
    {
        var node = new MdText(text);
        _nodes.Add(node);
        return node;
    }

    private void ParseInline()
    {
        var c = Peek();
        var handled = c switch
        {
            '\n' => ParseNewline(),
            '\\' => ParseBackslash(),
            '`' => ParseBackticks(),
            '*' or '_' or '~' => HandleDelimiter(c),
            '[' => ParseOpenBracket(),
            '!' => ParseBang(),
            ']' => ParseCloseBracket(),
            '<' => ParseAutolink() || ParseHtmlTag(),
            '&' => ParseEntity(),
            _ => ParseString(),
        };
        if (!handled)
        {
            _pos++;
            AddText(c.ToString());
        }
    }

    private bool ParseNewline()
    {
        _pos++;
        if (_nodes.Count > 0 && _nodes[^1] is MdText { Text: [.., ' '] } last)
        {
            var hard = last.Text.Length >= 2 && last.Text[^2] == ' ';
            last.Text = last.Text.TrimEnd(' ');
            _nodes.Add(hard ? new MdLineBreak() : new MdSoftBreak());
        }
        else
            _nodes.Add(new MdSoftBreak());
        while (Peek() is ' ' or '\t')
            _pos++;
        return true;
    }

    private bool ParseBackslash()
    {
        _pos++;
        if (Peek() == '\n')
        {
            _pos++;
            _nodes.Add(new MdLineBreak());
            while (Peek() is ' ' or '\t')
                _pos++;
        }
        else if (MarkdownText.IsEscapable(Peek()))
        {
            AddText(Peek().ToString());
            _pos++;
        }
        else
            AddText("\\");
        return true;
    }

    private static int RunLength(string s, int start, char c)
    {
        var end = start;
        while (end < s.Length && s[end] == c)
            end++;
        return end - start;
    }

    private bool ParseBackticks()
    {
        var length = RunLength(_subject, _pos, '`');
        var afterOpen = _pos + length;
        var search = afterOpen;
        while ((search = _subject.IndexOf('`', search)) >= 0)
        {
            var run = RunLength(_subject, search, '`');
            if (run == length)
            {
                var contents = _subject[afterOpen..search].Replace('\n', ' ');
                if (contents.Length > 2 && contents[0] == ' ' && contents[^1] == ' ' && contents.Trim(' ').Length > 0)
                    contents = contents[1..^1];
                _nodes.Add(new MdCode(contents));
                _pos = search + run;
                return true;
            }
            search += run;
        }
        // No closer: the backticks are literal.
        AddText(new string('`', length));
        _pos = afterOpen;
        return true;
    }

    // ---- emphasis ----

    private bool HandleDelimiter(char c)
    {
        var start = _pos;
        var count = RunLength(_subject, _pos, c);
        var before = start == 0 ? '\n' : _subject[start - 1];
        var after = start + count >= _subject.Length ? '\n' : _subject[start + count];
        var afterSpace = MarkdownText.IsWhitespace(after);
        var afterPunct = MarkdownText.IsPunctuation(after);
        var beforeSpace = MarkdownText.IsWhitespace(before);
        var beforePunct = MarkdownText.IsPunctuation(before);
        var leftFlanking = !afterSpace && (!afterPunct || beforeSpace || beforePunct);
        var rightFlanking = !beforeSpace && (!beforePunct || afterSpace || afterPunct);
        bool canOpen, canClose;
        if (c == '_')
        {
            canOpen = leftFlanking && (!rightFlanking || beforePunct);
            canClose = rightFlanking && (!leftFlanking || afterPunct);
        }
        else
        {
            canOpen = leftFlanking;
            canClose = rightFlanking;
        }

        _pos += count;
        var node = AddText(_subject.Substring(start, count));
        // GFM strikethrough takes one or two tildes; longer runs are text.
        if ((canOpen || canClose) && (c != '~' || count <= 2))
        {
            var delimiter = new Delimiter
            {
                Char = c,
                Count = count,
                OriginalCount = count,
                Node = node,
                Previous = _delimiters,
                CanOpen = canOpen,
                CanClose = canClose,
            };
            if (_delimiters is not null)
                _delimiters.Next = delimiter;
            _delimiters = delimiter;
        }
        return true;
    }

    private void RemoveDelimiter(Delimiter d)
    {
        if (d.Previous is not null)
            d.Previous.Next = d.Next;
        if (d.Next is not null)
            d.Next.Previous = d.Previous;
        else
            _delimiters = d.Previous;
    }

    private static int BottomIndex(Delimiter closer) => closer.Char switch
    {
        '_' => (closer.CanOpen ? 3 : 0) + closer.OriginalCount % 3,
        '*' => 6 + (closer.CanOpen ? 3 : 0) + closer.OriginalCount % 3,
        _ => 12 + Math.Min(closer.OriginalCount, 2),
    };

    private void ProcessEmphasis(Delimiter? stackBottom)
    {
        var openersBottom = new Delimiter?[15];
        Array.Fill(openersBottom, stackBottom);

        var closer = _delimiters;
        while (closer is not null && closer.Previous != stackBottom)
            closer = closer.Previous;

        while (closer is not null)
        {
            if (!closer.CanClose)
            {
                closer = closer.Next;
                continue;
            }

            var bottomIndex = BottomIndex(closer);
            var opener = closer.Previous;
            var found = false;
            while (opener is not null && opener != stackBottom && opener != openersBottom[bottomIndex])
            {
                if (opener.Char == closer.Char && opener.CanOpen)
                {
                    if (closer.Char == '~')
                    {
                        if (opener.Count == closer.Count)
                        {
                            found = true;
                            break;
                        }
                    }
                    else
                    {
                        var oddMatch = (closer.CanOpen || opener.CanClose) && closer.OriginalCount % 3 != 0
                                       && (opener.OriginalCount + closer.OriginalCount) % 3 == 0;
                        if (!oddMatch)
                        {
                            found = true;
                            break;
                        }
                    }
                }
                opener = opener.Previous;
            }

            var oldCloser = closer;
            if (found && opener is not null)
            {
                var use = closer.Char == '~' ? closer.Count : closer.Count >= 2 && opener.Count >= 2 ? 2 : 1;
                opener.Count -= use;
                closer.Count -= use;
                opener.Node.Text = opener.Node.Text[..^use];
                closer.Node.Text = closer.Node.Text[..^use];

                MdContainerInline wrapper = closer.Char == '~' ? new MdStrikethrough() : use == 1 ? new MdEmphasis() : new MdStrong();
                var openIndex = _nodes.IndexOf(opener.Node);
                var closeIndex = _nodes.IndexOf(closer.Node);
                wrapper.Children.AddRange(_nodes.GetRange(openIndex + 1, closeIndex - openIndex - 1));
                _nodes.RemoveRange(openIndex + 1, closeIndex - openIndex - 1);
                _nodes.Insert(openIndex + 1, wrapper);

                // Delimiters between the two are inside the wrapper now.
                opener.Next = closer;
                closer.Previous = opener;

                if (opener.Count == 0)
                {
                    _nodes.Remove(opener.Node);
                    RemoveDelimiter(opener);
                }
                if (closer.Count == 0)
                {
                    _nodes.Remove(closer.Node);
                    var next = closer.Next;
                    RemoveDelimiter(closer);
                    closer = next;
                }
            }
            else
            {
                closer = closer.Next;
                openersBottom[bottomIndex] = oldCloser.Previous;
                if (!oldCloser.CanOpen)
                    RemoveDelimiter(oldCloser);
            }
        }

        while (_delimiters is not null && _delimiters != stackBottom)
            RemoveDelimiter(_delimiters);
    }

    // ---- links and images ----

    private void AddBracket(MdText node, int index, bool image)
    {
        if (_brackets is not null)
            _brackets.BracketAfter = true;
        _brackets = new Bracket { Node = node, Previous = _brackets, PreviousDelimiter = _delimiters, Index = index, Image = image };
    }

    private bool ParseOpenBracket()
    {
        // Footnote reference (GFM)
        if (Peek(1) == '^' && FootnoteReferenceRegex().Match(_subject, _pos) is { Success: true } m && footnote(m.Groups[1].Value) is { } definition)
        {
            _nodes.Add(new MdFootnoteReference(definition));
            _pos += m.Length;
            return true;
        }
        var start = _pos;
        _pos++;
        AddBracket(AddText("["), start + 1, image: false);
        return true;
    }

    private bool ParseBang()
    {
        var start = _pos;
        _pos++;
        if (Peek() == '[')
        {
            _pos++;
            AddBracket(AddText("!["), start + 2, image: true);
        }
        else
            AddText("!");
        return true;
    }

    private bool ParseCloseBracket()
    {
        var start = _pos;
        _pos++;
        var opener = _brackets;
        if (opener is null)
        {
            AddText("]");
            return true;
        }
        if (!opener.Active)
        {
            AddText("]");
            _brackets = opener.Previous;
            return true;
        }

        string? destination = null;
        string? title = null;
        var matched = false;
        var save = _pos;

        // Inline link: [text](destination "title")
        if (Peek() == '(')
        {
            _pos++;
            Spnl();
            if (ParseLinkDestination() is { } dest)
            {
                destination = dest;
                var beforeTitle = _pos;
                Spnl();
                if (_pos > beforeTitle && _pos > 0 && MarkdownText.IsWhitespace(_subject[_pos - 1]))
                    title = ParseLinkTitle();
                Spnl();
                if (Peek() == ')')
                {
                    _pos++;
                    matched = true;
                }
            }
            if (!matched)
                _pos = save;
        }

        // Reference link: [text][label], [text][], [text]
        if (!matched)
        {
            var beforeLabel = _pos;
            var n = ParseLinkLabel();
            string? label = null;
            if (n > 2)
                label = _subject.Substring(beforeLabel, n);
            else if (!opener.BracketAfter)
                label = _subject[opener.Index..start];
            if (n == 0)
                _pos = save;
            if (label is not null && references.TryGetValue(MarkdownText.NormalizeReference(label), out var definition))
            {
                destination = definition.Url;
                title = definition.Title;
                matched = true;
            }
        }

        if (!matched)
        {
            _brackets = opener.Previous;
            _pos = start + 1;
            AddText("]");
            return true;
        }

        // Emphasis inside the link text is resolved first, then the text goes into the link.
        ProcessEmphasis(opener.PreviousDelimiter);
        MdContainerInline link = opener.Image
            ? new MdImage { Url = destination ?? "", Title = title }
            : new MdLink { Url = destination ?? "", Title = title };
        var openIndex = _nodes.IndexOf(opener.Node);
        link.Children.AddRange(_nodes.Skip(openIndex + 1));
        _nodes.RemoveRange(openIndex, _nodes.Count - openIndex);
        _nodes.Add(link);
        _brackets = opener.Previous;

        // No links in links: the earlier link openers are deactivated.
        if (!opener.Image)
            for (var b = _brackets; b is not null; b = b.Previous)
                if (!b.Image)
                    b.Active = false;
        return true;
    }

    private void Spnl()
    {
        var m = SpnlRegex().Match(_subject, _pos);
        _pos += m.Length;
    }

    private string? ParseLinkDestination()
    {
        if (DestinationBracesRegex().Match(_subject, _pos) is { Success: true } braces)
        {
            _pos += braces.Length;
            return MarkdownText.Unescape(braces.Value[1..^1]);
        }
        if (Peek() == '<')
            return null;

        var start = _pos;
        var openParens = 0;
        char c;
        while ((c = Peek()) != '\0')
        {
            if (c == '\\' && MarkdownText.IsEscapable(Peek(1)))
                _pos += 2;
            else if (c == '(')
            {
                _pos++;
                openParens++;
            }
            else if (c == ')')
            {
                if (openParens < 1)
                    break;
                _pos++;
                openParens--;
            }
            else if (MarkdownText.IsWhitespace(c) || char.IsControl(c))
                break;
            else
                _pos++;
        }
        if ((_pos == start && c != ')') || openParens != 0)
        {
            _pos = start;
            return null;
        }
        return MarkdownText.Unescape(_subject[start.._pos]);
    }

    private string? ParseLinkTitle()
    {
        var m = TitleRegex().Match(_subject, _pos);
        if (!m.Success)
            return null;
        _pos += m.Length;
        return MarkdownText.Unescape(m.Value[1..^1]);
    }

    private int ParseLinkLabel()
    {
        var m = LinkLabelRegex().Match(_subject, _pos);
        if (!m.Success || m.Length > 1001)
            return 0;
        _pos += m.Length;
        return m.Length;
    }

    /// <summary>
    /// Reads a link reference definition at <paramref name="start"/> of a paragraph's text into
    /// <paramref name="map"/>; returns the length of it, 0 when there is none.
    /// </summary>
    public static int ParseReference(string text, int start, Dictionary<string, MdLinkDefinition> map)
    {
        var parser = new InlineParser(map, _ => null) { _subject = text, _pos = start };
        return parser.ParseReference(map);
    }

    private int ParseReference(Dictionary<string, MdLinkDefinition> map)
    {
        var start = _pos;
        var labelLength = ParseLinkLabel();
        if (labelLength == 0)
            return 0;
        var label = _subject.Substring(start, labelLength);
        if (Peek() != ':')
        {
            _pos = start;
            return 0;
        }
        _pos++;
        Spnl();
        var destination = ParseLinkDestination();
        if (destination is null)
        {
            _pos = start;
            return 0;
        }

        var beforeTitle = _pos;
        Spnl();
        string? title = null;
        if (_pos != beforeTitle)
            title = ParseLinkTitle();
        if (title is null)
            _pos = beforeTitle;

        var atLineEnd = true;
        var end = SpaceAtEndOfLineRegex().Match(_subject, _pos);
        if (end.Success)
            _pos += end.Length;
        else if (title is null)
            atLineEnd = false;
        else
        {
            // The title is not alone on its line: the definition ends before it.
            title = null;
            _pos = beforeTitle;
            end = SpaceAtEndOfLineRegex().Match(_subject, _pos);
            atLineEnd = end.Success;
            _pos += end.Length;
        }
        if (!atLineEnd)
        {
            _pos = start;
            return 0;
        }

        var key = MarkdownText.NormalizeReference(label);
        if (key.Length == 0)
        {
            _pos = start;
            return 0;
        }
        map.TryAdd(key, new MdLinkDefinition(destination, title));
        return _pos - start;
    }

    // ---- autolinks, HTML, entities, text ----

    private bool ParseAutolink()
    {
        if (EmailAutolinkRegex().Match(_subject, _pos) is { Success: true } email)
        {
            _pos += email.Length;
            var address = email.Groups[1].Value;
            _nodes.Add(Link("mailto:" + address, address));
            return true;
        }
        if (AutolinkRegex().Match(_subject, _pos) is { Success: true } url)
        {
            _pos += url.Length;
            _nodes.Add(Link(url.Groups[1].Value, url.Groups[1].Value));
            return true;
        }
        return false;
    }

    private static MdLink Link(string url, string text)
    {
        var link = new MdLink { Url = url, IsAutolink = true };
        link.Children.Add(new MdText(text));
        return link;
    }

    private bool ParseHtmlTag()
    {
        var m = MarkdownText.HtmlTag.Match(_subject, _pos);
        if (!m.Success)
            return false;
        _pos += m.Length;
        _nodes.Add(new MdHtmlInline(m.Value));
        return true;
    }

    private bool ParseEntity()
    {
        var m = MarkdownText.EntityRegex().Match(_subject, _pos);
        if (!m.Success)
            return false;
        _pos += m.Length;
        AddText(MarkdownText.DecodeEntity(m.Value));
        return true;
    }

    private bool ParseString()
    {
        var start = _pos;
        while (_pos < _subject.Length && _subject[_pos] is not ('\n' or '\\' or '`' or '*' or '_' or '~' or '[' or ']' or '!' or '<' or '&'))
            _pos++;
        if (_pos == start)
            return false;
        AddText(_subject[start.._pos]);
        return true;
    }

    // ---- post processing ----

    /// <summary>Merges neighbouring texts, drops empty ones and finds bare URLs outside of links.</summary>
    private static void Finish(List<MdInline> nodes, bool insideLink)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is MdText text)
            {
                var builder = new StringBuilder(text.Text);
                while (i + 1 < nodes.Count && nodes[i + 1] is MdText next)
                {
                    builder.Append(next.Text);
                    nodes.RemoveAt(i + 1);
                }
                text.Text = builder.ToString();
                if (text.Text.Length == 0)
                {
                    nodes.RemoveAt(i--);
                    continue;
                }
                if (!insideLink)
                {
                    var parts = SplitBareUrls(text.Text);
                    if (parts is not null)
                    {
                        nodes.RemoveAt(i);
                        nodes.InsertRange(i, parts);
                        i += parts.Count - 1;
                    }
                }
            }
            else if (nodes[i] is MdContainerInline container)
                Finish(container.Children, insideLink || container is MdLink or MdImage);
        }
    }

    private static List<MdInline>? SplitBareUrls(string text)
    {
        if (text.IndexOf("://", StringComparison.Ordinal) < 0 && text.IndexOf("www.", StringComparison.Ordinal) < 0 && text.IndexOf('@') < 0)
            return null;
        List<MdInline>? parts = null;
        var last = 0;
        foreach (Match m in BareUrlRegex().Matches(text))
        {
            var value = TrimUrl(m.Value);
            var isEmail = !value.Contains("://") && !value.StartsWith("www.", StringComparison.Ordinal);
            if (value.Length == 0 || (isEmail && value[^1] is '-' or '_') || (!isEmail && !value.Contains('.') && !value.Contains("://")))
                continue;
            if (value.StartsWith("www.", StringComparison.Ordinal) && value.Length <= 4)
                continue;
            parts ??= [];
            if (m.Index > last)
                parts.Add(new MdText(text[last..m.Index]));
            var url = isEmail ? "mailto:" + value : value.StartsWith("www.", StringComparison.Ordinal) ? "http://" + value : value;
            parts.Add(Link(url, value));
            last = m.Index + value.Length;
        }
        if (parts is null)
            return null;
        if (last < text.Length)
            parts.Add(new MdText(text[last..]));
        return parts;
    }

    /// <summary>GFM's trailing punctuation rules: closing punctuation and unbalanced parentheses are not part of the URL.</summary>
    private static string TrimUrl(string url)
    {
        while (url.Length > 0)
        {
            var c = url[^1];
            if (c is '?' or '!' or '.' or ',' or ':' or '*' or '_' or '~' or '\'' or '"')
                url = url[..^1];
            else if (c == ')' && url.Count(ch => ch == ')') > url.Count(ch => ch == '('))
                url = url[..^1];
            else if (c == ';' && Regex.IsMatch(url, @"&[a-zA-Z0-9]+;$"))
                url = url[..url.LastIndexOf('&')];
            else
                break;
        }
        return url;
    }
}
