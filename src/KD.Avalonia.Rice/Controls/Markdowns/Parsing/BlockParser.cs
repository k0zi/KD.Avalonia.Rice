using System.Text;
using System.Text.RegularExpressions;

namespace KD.Avalonia.Rice.Controls.Markdowns;

/// <summary>
/// The block phase of the CommonMark algorithm: line by line, the open container blocks (quotes, list items) are
/// matched first, then new blocks are started, and what remains is added to the open leaf block. Paragraphs, which
/// may turn out to be link reference definitions or GFM tables, are resolved when they are closed. The inlines are
/// parsed at the end, when every reference definition is known.
/// </summary>
internal sealed partial class BlockParser
{
    private const int CodeIndent = 4;

    private enum Kind
    {
        Document,
        Quote,
        List,
        Item,
        Footnote,
        Paragraph,
        Heading,
        Code,
        Html,
        ThematicBreak,
    }

    private sealed class ListData
    {
        public bool Ordered { get; init; }
        public char Bullet { get; init; }
        public int Start { get; init; }
        public char Delimiter { get; init; }
        public int MarkerOffset { get; init; }
        public int Padding { get; set; }
        public bool Tight { get; set; } = true;
    }

    private sealed class Node(Kind kind, int line)
    {
        public Kind Kind { get; } = kind;
        public Node? Parent { get; set; }
        public List<Node> Children { get; } = [];
        public bool Open { get; set; } = true;
        public StringBuilder Content { get; } = new();
        public int StartLine { get; } = line;
        public int EndLine { get; set; } = line;
        public ListData? List { get; set; }
        public bool Fenced { get; set; }
        public char FenceChar { get; set; }
        public int FenceLength { get; set; }
        public int FenceOffset { get; set; }
        public string? Info { get; set; }
        public string Literal { get; set; } = "";
        public int Level { get; set; }
        public int HtmlType { get; set; }
        public string? Label { get; set; }

        public Node? LastChild => Children.Count > 0 ? Children[^1] : null;

        public bool AcceptsLines => Kind is Kind.Paragraph or Kind.Code or Kind.Html;

        public bool CanContain(Kind kind) => Kind switch
        {
            Kind.Document or Kind.Quote or Kind.Item or Kind.Footnote => kind != Kind.Item,
            Kind.List => kind == Kind.Item,
            _ => false,
        };
    }

    private enum StartResult
    {
        None,
        Container,
        Leaf,
    }

    [GeneratedRegex(@"^#{1,6}(?:[ \t]+|$)")]
    private static partial Regex AtxMarkerRegex();

    [GeneratedRegex(@"^`{3,}(?!.*`)|^~{3,}")]
    private static partial Regex CodeFenceRegex();

    [GeneratedRegex(@"^(?:`{3,}|~{3,})(?=[ \t]*$)")]
    private static partial Regex ClosingCodeFenceRegex();

    [GeneratedRegex(@"^(?:=+|-+)[ \t]*$")]
    private static partial Regex SetextRegex();

    [GeneratedRegex(@"^(?:\*[ \t]*){3,}$|^(?:_[ \t]*){3,}$|^(?:-[ \t]*){3,}$")]
    private static partial Regex ThematicBreakRegex();

    [GeneratedRegex(@"^[*+-]")]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"^(\d{1,9})([.)])")]
    private static partial Regex OrderedRegex();

    [GeneratedRegex(@"^\[\^([^\]\s]+)\]:")]
    private static partial Regex FootnoteRegex();

    [GeneratedRegex(@"^\[([ xX])\](?=[ \t]|$)")]
    private static partial Regex TaskRegex();

    [GeneratedRegex(@"^\|?[ \t]*:?-+:?[ \t]*(?:\|[ \t]*:?-+:?[ \t]*)*\|?[ \t]*$")]
    private static partial Regex TableDelimiterRegex();

    private readonly Dictionary<string, MdLinkDefinition> _references = [];
    private readonly Dictionary<string, Node> _footnotes = [];
    private Node _document = null!;
    private Node _tip = null!;
    private Node _oldTip = null!;
    private Node _lastMatchedContainer = null!;
    private string _line = "";
    private int _lineNumber;
    private int _offset;
    private int _column;
    private int _nextNonspace;
    private int _nextNonspaceColumn;
    private int _indent;
    private bool _indented;
    private bool _blank;
    private bool _partiallyConsumedTab;
    private bool _allClosed;

    public MarkdownDocument Parse(string text)
    {
        _document = new Node(Kind.Document, 1);
        _tip = _document;
        _oldTip = _document;
        _lastMatchedContainer = _document;
        _lineNumber = 0;

        text = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\0', '�');
        var lines = text.Split('\n');
        var count = lines.Length;
        if (text.EndsWith('\n'))
            count--;
        for (var i = 0; i < count; i++)
            IncorporateLine(lines[i]);
        while (_tip is not null && _tip != _document)
            Finalize(_tip, _lineNumber);
        Finalize(_document, _lineNumber);

        return new Converter(this).Convert(_document);
    }

    // ---- line processing ----

    private char Peek(int position) => position < _line.Length ? _line[position] : '\0';

    private void FindNextNonspace()
    {
        var i = _offset;
        var columns = _column;
        char c;
        while ((c = Peek(i)) != '\0')
        {
            if (c == ' ')
            {
                i++;
                columns++;
            }
            else if (c == '\t')
            {
                i++;
                columns += 4 - columns % 4;
            }
            else
                break;
        }
        _blank = c == '\0';
        _nextNonspace = i;
        _nextNonspaceColumn = columns;
        _indent = _nextNonspaceColumn - _column;
        _indented = _indent >= CodeIndent;
    }

    private void AdvanceNextNonspace()
    {
        _offset = _nextNonspace;
        _column = _nextNonspaceColumn;
        _partiallyConsumedTab = false;
    }

    private void AdvanceOffset(int count, bool columns)
    {
        while (count > 0 && _offset < _line.Length)
        {
            var c = _line[_offset];
            if (c == '\t')
            {
                var charsToTab = 4 - _column % 4;
                if (columns)
                {
                    _partiallyConsumedTab = charsToTab > count;
                    var charsToAdvance = Math.Min(charsToTab, count);
                    _column += charsToAdvance;
                    _offset += _partiallyConsumedTab ? 0 : 1;
                    count -= charsToAdvance;
                }
                else
                {
                    _partiallyConsumedTab = false;
                    _column += charsToTab;
                    _offset++;
                    count--;
                }
            }
            else
            {
                _partiallyConsumedTab = false;
                _offset++;
                _column++;
                count--;
            }
        }
    }

    private void AddLine()
    {
        if (_partiallyConsumedTab)
        {
            _offset++;
            _tip.Content.Append(' ', 4 - _column % 4);
        }
        _tip.Content.Append(_line, Math.Min(_offset, _line.Length), Math.Max(0, _line.Length - _offset)).Append('\n');
    }

    private Node AddChild(Kind kind)
    {
        while (!_tip.CanContain(kind))
            Finalize(_tip, _lineNumber - 1);
        var node = new Node(kind, _lineNumber) { Parent = _tip };
        _tip.Children.Add(node);
        _tip = node;
        return node;
    }

    private void CloseUnmatchedBlocks()
    {
        if (_allClosed)
            return;
        while (_oldTip != _lastMatchedContainer)
        {
            var parent = _oldTip.Parent!;
            Finalize(_oldTip, _lineNumber - 1);
            _oldTip = parent;
        }
        _allClosed = true;
    }

    private void IncorporateLine(string line)
    {
        var container = _document;
        _oldTip = _tip;
        _offset = 0;
        _column = 0;
        _blank = false;
        _partiallyConsumedTab = false;
        _lineNumber++;
        _line = line;

        // 1. Match the open containers.
        while (container.LastChild is { Open: true } last)
        {
            container = last;
            FindNextNonspace();
            var result = Continue(container);
            if (result == 2)
                return;
            if (result == 1)
            {
                container = container.Parent!;
                break;
            }
        }

        _allClosed = container == _oldTip;
        _lastMatchedContainer = container;
        var matchedLeaf = container.Kind != Kind.Paragraph && container.AcceptsLines;

        // 2. Start new blocks.
        while (!matchedLeaf)
        {
            FindNextNonspace();
            if (!_indented && !MaybeSpecial(Peek(_nextNonspace)))
            {
                AdvanceNextNonspace();
                break;
            }
            var result = TryStart(container);
            if (result == StartResult.Container)
            {
                container = _tip;
                continue;
            }
            if (result == StartResult.Leaf)
            {
                container = _tip;
                matchedLeaf = true;
                continue;
            }
            AdvanceNextNonspace();
            break;
        }

        // 3. The rest of the line: lazy paragraph continuation, or text for the open leaf.
        if (!_allClosed && !_blank && _tip.Kind == Kind.Paragraph)
            AddLine();
        else
        {
            CloseUnmatchedBlocks();
            if (container.AcceptsLines)
            {
                AddLine();
                if (container.Kind == Kind.Html && container.HtmlType is >= 1 and <= 5
                    && MarkdownText.HtmlBlockClose[container.HtmlType].IsMatch(_line[Math.Min(_offset, _line.Length)..]))
                    Finalize(container, _lineNumber);
            }
            else if (_offset < _line.Length && !_blank)
            {
                AddChild(Kind.Paragraph);
                AdvanceNextNonspace();
                AddLine();
            }
        }
    }

    private static bool MaybeSpecial(char c) => c is '#' or '`' or '~' or '*' or '+' or '_' or '=' or '<' or '>' or '-' or '[' or >= '0' and <= '9';

    /// <summary>0: matched, 1: not matched, 2: the line is consumed (closing fence).</summary>
    private int Continue(Node container)
    {
        switch (container.Kind)
        {
            case Kind.Quote:
                if (!_indented && Peek(_nextNonspace) == '>')
                {
                    AdvanceNextNonspace();
                    AdvanceOffset(1, false);
                    if (MarkdownText.IsSpaceOrTab(Peek(_offset)))
                        AdvanceOffset(1, true);
                    return 0;
                }
                return 1;
            case Kind.Item:
            {
                var list = container.List!;
                if (_blank)
                {
                    if (container.Children.Count == 0)
                        return 1;
                    AdvanceNextNonspace();
                }
                else if (_indent >= list.MarkerOffset + list.Padding)
                    AdvanceOffset(list.MarkerOffset + list.Padding, true);
                else
                    return 1;
                return 0;
            }
            case Kind.Footnote:
                if (_blank)
                    AdvanceNextNonspace();
                else if (_indent >= 4)
                    AdvanceOffset(4, true);
                else
                    return 1;
                return 0;
            case Kind.Heading or Kind.ThematicBreak:
                return 1;
            case Kind.Code:
                if (container.Fenced)
                {
                    var rest = _line[Math.Min(_nextNonspace, _line.Length)..];
                    var match = _indent <= 3 && Peek(_nextNonspace) == container.FenceChar ? ClosingCodeFenceRegex().Match(rest) : Match.Empty;
                    if (match.Success && match.Length >= container.FenceLength && match.Value[0] == container.FenceChar)
                    {
                        Finalize(container, _lineNumber);
                        return 2;
                    }
                    for (var i = container.FenceOffset; i > 0 && MarkdownText.IsSpaceOrTab(Peek(_offset)); i--)
                        AdvanceOffset(1, true);
                }
                else if (_indent >= CodeIndent)
                    AdvanceOffset(CodeIndent, true);
                else if (_blank)
                    AdvanceNextNonspace();
                else
                    return 1;
                return 0;
            case Kind.Html:
                return _blank && container.HtmlType is 6 or 7 ? 1 : 0;
            case Kind.Paragraph:
                return _blank ? 1 : 0;
            default:
                return 0;
        }
    }

    private StartResult TryStart(Node container)
    {
        var rest = _line[Math.Min(_nextNonspace, _line.Length)..];
        var c = Peek(_nextNonspace);

        // Block quote
        if (!_indented && c == '>')
        {
            AdvanceNextNonspace();
            AdvanceOffset(1, false);
            if (MarkdownText.IsSpaceOrTab(Peek(_offset)))
                AdvanceOffset(1, true);
            CloseUnmatchedBlocks();
            AddChild(Kind.Quote);
            return StartResult.Container;
        }

        // ATX heading
        if (!_indented && c == '#' && AtxMarkerRegex().Match(rest) is { Success: true } atx)
        {
            AdvanceNextNonspace();
            AdvanceOffset(atx.Length, false);
            CloseUnmatchedBlocks();
            var heading = AddChild(Kind.Heading);
            heading.Level = atx.Value.Trim().Length;
            var content = _line[Math.Min(_offset, _line.Length)..];
            content = Regex.Replace(content, @"^[ \t]*#+[ \t]*$", "");
            content = Regex.Replace(content, @"[ \t]+#+[ \t]*$", "");
            heading.Content.Append(content);
            AdvanceOffset(_line.Length - _offset, false);
            return StartResult.Leaf;
        }

        // Fenced code
        if (!_indented && c is '`' or '~' && CodeFenceRegex().Match(rest) is { Success: true } fence)
        {
            CloseUnmatchedBlocks();
            var code = AddChild(Kind.Code);
            code.Fenced = true;
            code.FenceLength = fence.Length;
            code.FenceChar = fence.Value[0];
            code.FenceOffset = _indent;
            AdvanceNextNonspace();
            AdvanceOffset(fence.Length, false);
            return StartResult.Leaf;
        }

        // HTML block
        if (!_indented && c == '<')
        {
            var s = _line[Math.Min(_offset, _line.Length)..].TrimStart(' ', '\t');
            for (var type = 1; type <= 7; type++)
            {
                if (!MarkdownText.HtmlBlockOpen[type].IsMatch(s))
                    continue;
                // Kind 7 cannot interrupt a paragraph.
                if (type == 7 && (container.Kind == Kind.Paragraph || (!_allClosed && !_blank && _tip.Kind == Kind.Paragraph)))
                    continue;
                CloseUnmatchedBlocks();
                var html = AddChild(Kind.Html);
                html.HtmlType = type;
                return StartResult.Leaf;
            }
        }

        // Footnote definition (GFM)
        if (!_indented && c == '[' && FootnoteRegex().Match(rest) is { Success: true } footnote)
        {
            AdvanceNextNonspace();
            AdvanceOffset(footnote.Length, false);
            if (MarkdownText.IsSpaceOrTab(Peek(_offset)))
                AdvanceOffset(1, true);
            CloseUnmatchedBlocks();
            var node = AddChild(Kind.Footnote);
            node.Label = MarkdownText.NormalizeReference(footnote.Groups[1].Value);
            return StartResult.Container;
        }

        // Setext heading
        if (!_indented && container.Kind == Kind.Paragraph && c is '=' or '-' && SetextRegex().Match(rest) is { Success: true } setext)
        {
            CloseUnmatchedBlocks();
            var content = ExtractReferences(container.Content.ToString());
            if (content.Length > 0)
            {
                var heading = new Node(Kind.Heading, container.StartLine) { Parent = container.Parent, Level = setext.Value[0] == '=' ? 1 : 2 };
                heading.Content.Append(content);
                var siblings = container.Parent!.Children;
                siblings[siblings.IndexOf(container)] = heading;
                _tip = heading;
                AdvanceOffset(_line.Length - _offset, false);
                return StartResult.Leaf;
            }
            container.Content.Clear();
        }

        // Thematic break
        if (!_indented && c is '*' or '_' or '-' && ThematicBreakRegex().IsMatch(rest))
        {
            CloseUnmatchedBlocks();
            AddChild(Kind.ThematicBreak);
            AdvanceOffset(_line.Length - _offset, false);
            return StartResult.Leaf;
        }

        // List item
        if ((!_indented || container.Kind == Kind.List) && ParseListMarker(container) is { } data)
        {
            CloseUnmatchedBlocks();
            if (_tip.Kind != Kind.List || !ListsMatch(container.List, data))
            {
                var list = AddChild(Kind.List);
                list.List = data;
            }
            var item = AddChild(Kind.Item);
            item.List = data;
            return StartResult.Container;
        }

        // Indented code
        if (_indented && _tip.Kind != Kind.Paragraph && !_blank)
        {
            AdvanceOffset(CodeIndent, true);
            CloseUnmatchedBlocks();
            AddChild(Kind.Code);
            return StartResult.Leaf;
        }

        return StartResult.None;
    }

    private static bool ListsMatch(ListData? a, ListData b) =>
        a is not null && a.Ordered == b.Ordered && a.Delimiter == b.Delimiter && a.Bullet == b.Bullet;

    private ListData? ParseListMarker(Node container)
    {
        if (_indent >= 4)
            return null;
        var rest = _line[Math.Min(_nextNonspace, _line.Length)..];
        int markerLength;
        ListData data;
        if (BulletRegex().Match(rest) is { Success: true } bullet)
        {
            markerLength = 1;
            data = new ListData { Bullet = bullet.Value[0], MarkerOffset = _indent };
        }
        else if (OrderedRegex().Match(rest) is { Success: true } ordered
                 && (container.Kind != Kind.Paragraph || ordered.Groups[1].Value == "1"))
        {
            markerLength = ordered.Length;
            data = new ListData
            {
                Ordered = true,
                Start = int.Parse(ordered.Groups[1].Value),
                Delimiter = ordered.Groups[2].Value[0],
                MarkerOffset = _indent,
            };
        }
        else
            return null;

        var next = Peek(_nextNonspace + markerLength);
        if (next != '\0' && !MarkdownText.IsSpaceOrTab(next))
            return null;
        // An empty item cannot interrupt a paragraph.
        if (container.Kind == Kind.Paragraph && rest[markerLength..].Trim(' ', '\t').Length == 0)
            return null;

        AdvanceNextNonspace();
        AdvanceOffset(markerLength, true);
        var spacesStartColumn = _column;
        var spacesStartOffset = _offset;
        do
        {
            AdvanceOffset(1, true);
            next = Peek(_offset);
        }
        while (_column - spacesStartColumn < 5 && MarkdownText.IsSpaceOrTab(next));
        var blankItem = Peek(_offset) == '\0';
        var spacesAfterMarker = _column - spacesStartColumn;
        if (spacesAfterMarker >= 5 || spacesAfterMarker < 1 || blankItem)
        {
            data.Padding = markerLength + 1;
            _column = spacesStartColumn;
            _offset = spacesStartOffset;
            if (MarkdownText.IsSpaceOrTab(Peek(_offset)))
                AdvanceOffset(1, true);
        }
        else
            data.Padding = markerLength + spacesAfterMarker;
        return data;
    }

    // ---- finalization ----

    private void Finalize(Node block, int line)
    {
        var parent = block.Parent;
        block.Open = false;
        block.EndLine = Math.Max(block.StartLine, line);
        switch (block.Kind)
        {
            case Kind.Paragraph:
            {
                var content = ExtractReferences(block.Content.ToString());
                block.Content.Clear().Append(content);
                if (content.Trim().Length == 0)
                    parent?.Children.Remove(block);
                break;
            }
            case Kind.Code:
                if (block.Fenced)
                {
                    var content = block.Content.ToString();
                    var newline = content.IndexOf('\n');
                    var first = newline < 0 ? content : content[..newline];
                    block.Info = MarkdownText.Unescape(first.Trim());
                    block.Literal = newline < 0 ? "" : content[(newline + 1)..];
                }
                else
                    block.Literal = Regex.Replace(block.Content.ToString(), @"(\n *)+$", "\n");
                break;
            case Kind.Html:
                block.Literal = Regex.Replace(block.Content.ToString(), @"(\n *)+$", "");
                break;
            case Kind.Item:
                block.EndLine = block.LastChild?.EndLine ?? block.StartLine;
                break;
            case Kind.List:
            {
                var tight = true;
                for (var i = 0; i < block.Children.Count && tight; i++)
                {
                    var item = block.Children[i];
                    if (i < block.Children.Count - 1 && EndsWithBlankLine(item, block.Children[i + 1]))
                        tight = false;
                    for (var j = 0; j < item.Children.Count - 1 && tight; j++)
                        if (EndsWithBlankLine(item.Children[j], item.Children[j + 1]))
                            tight = false;
                }
                block.List!.Tight = tight;
                block.EndLine = block.LastChild?.EndLine ?? block.StartLine;
                break;
            }
            case Kind.Footnote:
                _footnotes.TryAdd(block.Label!, block);
                break;
        }
        _tip = parent!;
    }

    private static bool EndsWithBlankLine(Node block, Node next) => next.StartLine > block.EndLine + 1;

    /// <summary>Takes the link reference definitions off the start of a paragraph; returns what is left.</summary>
    private string ExtractReferences(string content)
    {
        var position = 0;
        while (position < content.Length && content[position] == '[')
        {
            var consumed = InlineParser.ParseReference(content, position, _references);
            if (consumed == 0)
                break;
            position += consumed;
        }
        return content[position..];
    }

    // ---- conversion to the public model ----

    private sealed class Converter(BlockParser parser)
    {
        private readonly List<MdFootnoteDefinition> _numbered = [];
        private readonly Dictionary<string, MdFootnoteDefinition> _footnotes = [];
        private readonly Dictionary<MdFootnoteDefinition, Node> _footnoteNodes = [];
        private readonly Dictionary<string, int> _slugs = [];
        private InlineParser _inlines = null!;

        public MarkdownDocument Convert(Node document)
        {
            // Footnotes are numbered on their first reference.
            _inlines = new InlineParser(parser._references, Footnote);
            var blocks = ConvertBlocks(document.Children);
            // The content of a footnote may reference further footnotes, which are appended to the list.
            for (var i = 0; i < _numbered.Count; i++)
                _numbered[i].Children.AddRange(ConvertBlocks(_footnoteNodes[_numbered[i]].Children));
            return new MarkdownDocument(blocks, parser._references, _numbered);
        }

        private MdFootnoteDefinition? Footnote(string label)
        {
            var key = MarkdownText.NormalizeReference(label);
            if (_footnotes.TryGetValue(key, out var existing))
                return existing;
            if (!parser._footnotes.TryGetValue(key, out var node))
                return null;
            var definition = new MdFootnoteDefinition { Label = label, Line = node.StartLine };
            _footnotes[key] = definition;
            _numbered.Add(definition);
            definition.Number = _numbered.Count;
            _footnoteNodes[definition] = node;
            return definition;
        }

        private List<MdBlock> ConvertBlocks(IEnumerable<Node> nodes)
        {
            var blocks = new List<MdBlock>();
            foreach (var node in nodes)
                ConvertBlock(node, blocks);
            return blocks;
        }

        private void ConvertBlock(Node node, List<MdBlock> output)
        {
            switch (node.Kind)
            {
                case Kind.Paragraph:
                    ConvertParagraph(node, output);
                    break;
                case Kind.Heading:
                {
                    var heading = new MdHeading { Level = node.Level, Line = node.StartLine };
                    heading.Inlines.AddRange(_inlines.Parse(node.Content.ToString().Trim()));
                    heading.Slug = UniqueSlug(MarkdownText.Slug(heading.PlainText));
                    output.Add(heading);
                    break;
                }
                case Kind.Quote:
                {
                    var quote = new MdQuote { Line = node.StartLine };
                    quote.Children.AddRange(ConvertBlocks(node.Children));
                    output.Add(quote);
                    break;
                }
                case Kind.List:
                {
                    var data = node.List!;
                    var list = new MdList { IsOrdered = data.Ordered, Start = data.Start, IsTight = data.Tight, Line = node.StartLine };
                    foreach (var child in node.Children)
                        list.Items.Add(ConvertItem(child));
                    output.Add(list);
                    break;
                }
                case Kind.Code:
                {
                    var info = string.IsNullOrWhiteSpace(node.Info) ? null : node.Info;
                    output.Add(new MdCodeBlock
                    {
                        Info = info,
                        Language = info?.Split([' ', '\t', '{'], 2)[0].ToLowerInvariant() ?? "",
                        Code = node.Literal.EndsWith('\n') ? node.Literal[..^1] : node.Literal,
                        IsFenced = node.Fenced,
                        Line = node.StartLine,
                    });
                    break;
                }
                case Kind.Html:
                    output.Add(new MdHtmlBlock { Html = node.Literal, Line = node.StartLine });
                    break;
                case Kind.ThematicBreak:
                    output.Add(new MdThematicBreak { Line = node.StartLine });
                    break;
                // Footnote definitions are shown at the end of the document, not where they are written.
                case Kind.Footnote:
                    break;
            }
        }

        private MdListItem ConvertItem(Node node)
        {
            var item = new MdListItem { Line = node.StartLine };
            if (node.Children.FirstOrDefault() is { Kind: Kind.Paragraph } first
                && TaskRegex().Match(first.Content.ToString()) is { Success: true } task)
            {
                item.IsTask = true;
                item.IsChecked = task.Groups[1].Value != " ";
                var rest = first.Content.ToString()[task.Length..].TrimStart(' ', '\t');
                first.Content.Clear().Append(rest);
                if (rest.Trim().Length == 0)
                {
                    item.Children.AddRange(ConvertBlocks(node.Children.Skip(1)));
                    return item;
                }
            }
            item.Children.AddRange(ConvertBlocks(node.Children));
            return item;
        }

        private string UniqueSlug(string slug)
        {
            if (!_slugs.TryGetValue(slug, out var count))
            {
                _slugs[slug] = 1;
                return slug;
            }
            _slugs[slug] = count + 1;
            return $"{slug}-{count}";
        }

        /// <summary>A paragraph may hold a GFM table: a header line, a delimiter line and the rows to its end.</summary>
        private void ConvertParagraph(Node node, List<MdBlock> output)
        {
            var lines = node.Content.ToString().TrimEnd('\n').Split('\n');
            for (var i = 0; i + 1 < lines.Length; i++)
            {
                if (!TableDelimiterRegex().IsMatch(lines[i + 1]))
                    continue;
                var header = SplitRow(lines[i]);
                var delimiters = SplitRow(lines[i + 1]);
                if (header.Count != delimiters.Count || !(lines[i].Contains('|') || lines[i + 1].Contains('|')))
                    continue;

                if (i > 0)
                    AddParagraph(string.Join("\n", lines[..i]), node.StartLine, output);
                var table = new MdTable { Line = node.StartLine + i };
                foreach (var d in delimiters)
                {
                    var cell = d.Trim();
                    table.Alignments.Add((cell.StartsWith(':'), cell.EndsWith(':')) switch
                    {
                        (true, true) => MdTableAlignment.Center,
                        (true, false) => MdTableAlignment.Left,
                        (false, true) => MdTableAlignment.Right,
                        _ => MdTableAlignment.None,
                    });
                }
                table.Rows.Add(Row(header, table.Alignments.Count, true));
                for (var j = i + 2; j < lines.Length; j++)
                    table.Rows.Add(Row(SplitRow(lines[j]), table.Alignments.Count, false));
                output.Add(table);
                return;
            }
            AddParagraph(string.Join("\n", lines), node.StartLine, output);
        }

        private void AddParagraph(string content, int line, List<MdBlock> output)
        {
            var paragraph = new MdParagraph { Line = line };
            paragraph.Inlines.AddRange(_inlines.Parse(content.Trim()));
            output.Add(paragraph);
        }

        private MdTableRow Row(List<string> cells, int columns, bool header)
        {
            var row = new MdTableRow { IsHeader = header };
            for (var i = 0; i < columns; i++)
                row.Cells.Add(i < cells.Count ? _inlines.Parse(cells[i].Trim()) : []);
            return row;
        }

        /// <summary>Cells of a table line; <c>\|</c> is a pipe inside a cell.</summary>
        private static List<string> SplitRow(string line)
        {
            var text = line.Trim();
            if (text.StartsWith('|'))
                text = text[1..];
            if (text.EndsWith('|') && !text.EndsWith("\\|"))
                text = text[..^1];
            var cells = new List<string>();
            var cell = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == '|')
                {
                    cell.Append('|');
                    i++;
                }
                else if (text[i] == '|')
                {
                    cells.Add(cell.ToString());
                    cell.Clear();
                }
                else
                    cell.Append(text[i]);
            }
            cells.Add(cell.ToString());
            return cells;
        }
    }
}
