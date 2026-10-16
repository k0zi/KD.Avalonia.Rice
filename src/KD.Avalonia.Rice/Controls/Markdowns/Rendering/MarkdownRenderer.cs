using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Material.Icons;
using Material.Icons.Avalonia;

namespace KD.Avalonia.Rice.Controls.Markdowns;

/// <summary>Settings and services of one rendering of a document.</summary>
internal sealed class MarkdownRenderContext
{
    public required Action<Control, string> LinkClicked { get; init; }

    /// <summary>Makes the control of a diagram code block (mermaid, plantuml), or null to show it as code.</summary>
    public Func<MdCodeBlock, Control?>? DiagramFactory { get; init; }

    public double FontSize { get; init; } = 14;

    public FontFamily CodeFontFamily { get; init; } = FontFamily.Default;

    public bool Selectable { get; init; } = true;

    public Uri? BaseUri { get; init; }

    /// <summary>Width of the document content, for the images.</summary>
    public double AvailableWidth { get; set; }

    /// <summary>Anchors of the headings (by slug) and footnotes (<c>fn-1</c>, <c>fnref-1</c>).</summary>
    public Dictionary<string, Control> Anchors { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<MarkdownImage> Images { get; } = [];
}

/// <summary>Turns a <see cref="MarkdownDocument"/> into controls: text blocks with inlines, borders, grids.</summary>
internal sealed class MarkdownRenderer(MarkdownRenderContext context)
{
    private const string MutedBrush = "RiceForegroundMutedBrush";
    private const string BorderBrush = "RiceBorderBrush";

    private MarkdownRenderContext Context => context;

    private double FontSize => context.FontSize;

    private double BlockSpacing => Math.Round(FontSize * 0.85);

    public Control Render(MarkdownDocument document)
    {
        var root = Blocks(document.Blocks, tight: false, depth: 0);
        if (document.Footnotes.Count > 0)
            root.Children.Add(Footnotes(document));
        return root;
    }

    // ---- blocks ----

    private StackPanel Blocks(IEnumerable<MdBlock> blocks, bool tight, int depth)
    {
        var panel = new StackPanel { Spacing = tight ? 2 : BlockSpacing };
        var first = true;
        foreach (var block in blocks)
        {
            if (Block(block, tight, depth) is not { } control)
                continue;
            // Headings get extra room above them, except at the top.
            if (block is MdHeading && !first)
                control.Margin = new Thickness(0, FontSize * 0.6, 0, 0);
            panel.Children.Add(control);
            first = false;
        }
        return panel;
    }

    private Control? Block(MdBlock block, bool tight, int depth) => block switch
    {
        MdParagraph p => Paragraph(p.Inlines),
        MdHeading h => Heading(h),
        MdQuote q => Quote(q, depth),
        MdList l => List(l, depth),
        MdCodeBlock c => context.DiagramFactory?.Invoke(c) ?? CodeBlock(c),
        MdThematicBreak => Rule(),
        MdTable t => Table(t),
        MdHtmlBlock h => HtmlBlock(h),
        _ => null,
    };

    private MarkdownTextBlock Paragraph(IReadOnlyList<MdInline> inlines, double? fontSize = null)
    {
        var text = new MarkdownTextBlock(context) { TextWrapping = TextWrapping.Wrap };
        if (fontSize is { } size)
            text.FontSize = size;
        new InlineBuilder(this, text).Add(text.Inlines!, inlines);
        return text;
    }

    private Control Heading(MdHeading heading)
    {
        var scale = heading.Level switch { 1 => 2.0, 2 => 1.5, 3 => 1.25, 4 => 1.0, 5 => 0.875, _ => 0.85 };
        var text = Paragraph(heading.Inlines, Math.Round(FontSize * scale));
        text.FontWeight = heading.Level <= 2 ? FontWeight.Bold : FontWeight.SemiBold;
        if (heading.Level == 6)
            Resource(text, TextBlock.ForegroundProperty, MutedBrush);
        Control result = text;
        // GitHub style: a thin rule under the two biggest headings.
        if (heading.Level <= 2)
        {
            var border = new Border { Child = text, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, FontSize * 0.3) };
            Resource(border, Border.BorderBrushProperty, BorderBrush);
            result = border;
        }
        context.Anchors.TryAdd(heading.Slug, result);
        return result;
    }

    private Control Quote(MdQuote quote, int depth)
    {
        var content = Blocks(quote.Children, tight: false, depth);
        Resource(content, TextElement.ForegroundProperty, MutedBrush);
        var border = new Border
        {
            Child = content,
            BorderThickness = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(FontSize, 2, 0, 2),
        };
        Resource(border, Border.BorderBrushProperty, "RiceMarkdownQuoteBrush");
        return border;
    }

    private Control List(MdList list, int depth)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = FontSize * 0.45, RowSpacing = list.IsTight ? 4 : BlockSpacing };
        var lineHeight = FontSize * 1.33;
        for (var i = 0; i < list.Items.Count; i++)
        {
            var item = list.Items[i];
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Control marker;
            if (item.IsTask)
                marker = TaskBox(item.IsChecked, lineHeight);
            else if (list.IsOrdered)
                marker = new TextBlock { Text = $"{list.Start + i}.", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = FontSize * 1.1 };
            else
                marker = new TextBlock
                {
                    Text = (depth % 3) switch { 0 => "•", 1 => "◦", _ => "▪" },
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MinWidth = FontSize * 0.9,
                };
            if (marker is TextBlock { } mark)
                Resource(mark, TextBlock.ForegroundProperty, MutedBrush);
            Grid.SetRow(marker, i);
            grid.Children.Add(marker);

            var content = Blocks(item.Children, list.IsTight, depth + 1);
            Grid.SetRow(content, i);
            Grid.SetColumn(content, 1);
            grid.Children.Add(content);
        }
        return grid;
    }

    private Control TaskBox(bool isChecked, double lineHeight)
    {
        var size = Math.Round(FontSize * 1.05);
        var box = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(isChecked ? 0 : 1.5),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, Math.Max(0, (lineHeight - size) / 2), 0, 0),
        };
        if (isChecked)
        {
            Resource(box, Border.BackgroundProperty, "RiceAccentBrush");
            var check = new MaterialIcon { Kind = MaterialIconKind.Check, Width = size - 3, Height = size - 3 };
            Resource(check, MaterialIcon.ForegroundProperty, "RiceAccentForegroundBrush");
            box.Child = check;
        }
        else
            Resource(box, Border.BorderBrushProperty, MutedBrush);
        return box;
    }

    internal Control CodeBlock(MdCodeBlock code)
    {
        var text = new SelectableTextBlock
        {
            FontFamily = context.CodeFontFamily,
            FontSize = Math.Round(FontSize * 0.9),
            Padding = new Thickness(FontSize, FontSize * 0.75),
            TextWrapping = TextWrapping.NoWrap,
        };
        AddHighlighted(text, code.Code, code.Language);
        var scroller = new ScrollViewer
        {
            Content = text,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        var header = new DockPanel { Margin = new Thickness(FontSize, 4, 4, 0) };
        var copy = CopyButton(code.Code);
        DockPanel.SetDock(copy, Dock.Right);
        header.Children.Add(copy);
        var language = new TextBlock
        {
            Text = code.Language,
            FontSize = Math.Round(FontSize * 0.8),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Resource(language, TextBlock.ForegroundProperty, MutedBrush);
        header.Children.Add(language);

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto") };
        Grid.SetRow(scroller, 1);
        grid.Children.Add(header);
        grid.Children.Add(scroller);
        return Card(grid, "RiceMarkdownCodeBrush");
    }

    private Border Card(Control child, string background)
    {
        var border = new Border
        {
            Child = child,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
        };
        Resource(border, Border.BackgroundProperty, background);
        Resource(border, Border.BorderBrushProperty, BorderBrush);
        return border;
    }

    public Button CopyButton(string text)
    {
        var icon = new MaterialIcon { Kind = MaterialIconKind.ContentCopy, Width = 14, Height = 14 };
        var button = new Button
        {
            Content = icon,
            Padding = new Thickness(6, 4),
            MinHeight = 0,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(6),
            VerticalAlignment = VerticalAlignment.Top,
        };
        Resource(icon, MaterialIcon.ForegroundProperty, MutedBrush);
        ToolTip.SetTip(button, RiceStrings.MarkdownCopy);
        button.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(button)?.Clipboard is not { } clipboard)
                return;
            try
            {
                await clipboard.SetTextAsync(text);
            }
            catch (Exception)
            {
                return;
            }
            icon.Kind = MaterialIconKind.Check;
            ToolTip.SetTip(button, RiceStrings.MarkdownCopied);
            DispatcherTimer.RunOnce(() =>
            {
                icon.Kind = MaterialIconKind.ContentCopy;
                ToolTip.SetTip(button, RiceStrings.MarkdownCopy);
            }, TimeSpan.FromSeconds(1.5));
        };
        return button;
    }

    private static readonly Dictionary<CodeTokenKind, string> TokenBrushes = new()
    {
        [CodeTokenKind.Keyword] = "RiceCodeKeywordBrush",
        [CodeTokenKind.Type] = "RiceCodeTypeBrush",
        [CodeTokenKind.String] = "RiceCodeStringBrush",
        [CodeTokenKind.Number] = "RiceCodeNumberBrush",
        [CodeTokenKind.Comment] = "RiceCodeCommentBrush",
        [CodeTokenKind.Operator] = "RiceCodeOperatorBrush",
        [CodeTokenKind.Tag] = "RiceCodeTagBrush",
        [CodeTokenKind.Attribute] = "RiceCodeAttributeBrush",
    };

    private static void AddHighlighted(SelectableTextBlock text, string code, string language)
    {
        var tokens = CodeHighlighter.Highlight(code, language);
        if (tokens.Count == 0)
        {
            text.Text = code;
            return;
        }
        var inlines = text.Inlines!;
        var position = 0;
        foreach (var token in tokens)
        {
            if (token.Start > position)
                inlines.Add(new Run(code[position..token.Start]));
            var run = new Run(code.Substring(token.Start, token.Length));
            run.Bind(TextElement.ForegroundProperty, text.GetResourceObservable(TokenBrushes[token.Kind]));
            if (token.Kind == CodeTokenKind.Comment)
                run.FontStyle = FontStyle.Italic;
            inlines.Add(run);
            position = token.Start + token.Length;
        }
        if (position < code.Length)
            inlines.Add(new Run(code[position..]));
    }

    private Control Rule()
    {
        var rule = new Border { Height = 2, CornerRadius = new CornerRadius(1), Margin = new Thickness(0, FontSize * 0.4) };
        Resource(rule, Border.BackgroundProperty, BorderBrush);
        return rule;
    }

    private Control Table(MdTable table)
    {
        var columns = table.Alignments.Count;
        var grid = new Grid();
        for (var c = 0; c < columns; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var r = 0; r < table.Rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var row = table.Rows[r];
            for (var c = 0; c < columns; c++)
            {
                var text = Paragraph(row.Cells[c]);
                text.MaxWidth = 480;
                text.TextAlignment = table.Alignments[c] switch
                {
                    MdTableAlignment.Center => TextAlignment.Center,
                    MdTableAlignment.Right => TextAlignment.Right,
                    _ => TextAlignment.Left,
                };
                text.HorizontalAlignment = table.Alignments[c] switch
                {
                    MdTableAlignment.Center => HorizontalAlignment.Center,
                    MdTableAlignment.Right => HorizontalAlignment.Right,
                    _ => HorizontalAlignment.Left,
                };
                if (row.IsHeader)
                    text.FontWeight = FontWeight.SemiBold;
                var cell = new Border
                {
                    Child = text,
                    Padding = new Thickness(FontSize * 0.85, FontSize * 0.45),
                    BorderThickness = new Thickness(0, 0, c < columns - 1 ? 1 : 0, r < table.Rows.Count - 1 ? 1 : 0),
                };
                Resource(cell, Border.BorderBrushProperty, BorderBrush);
                if (row.IsHeader)
                    Resource(cell, Border.BackgroundProperty, "RiceMarkdownTableHeaderBrush");
                else if (r % 2 == 0)
                    Resource(cell, Border.BackgroundProperty, "RiceMarkdownTableStripeBrush");
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }
        var scroller = new ScrollViewer
        {
            Content = grid,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        var card = Card(scroller, "RiceBackgroundBrush");
        card.HorizontalAlignment = HorizontalAlignment.Left;
        return card;
    }

    private Control? HtmlBlock(MdHtmlBlock html)
    {
        var (inlines, centered) = MarkdownHtml.BlockToInlines(html.Html);
        if (inlines.Count == 0)
            return null;
        var text = Paragraph(inlines);
        if (centered)
        {
            text.TextAlignment = TextAlignment.Center;
            text.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        return text;
    }

    private Control Footnotes(MarkdownDocument document)
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, FontSize, 0, 0) };
        panel.Children.Add(Rule());
        var small = Math.Round(FontSize * 0.875);
        foreach (var footnote in document.Footnotes)
        {
            var row = new DockPanel();
            var number = new TextBlock { Text = $"{footnote.Number}.", FontSize = small, MinWidth = FontSize * 1.4, VerticalAlignment = VerticalAlignment.Top };
            Resource(number, TextBlock.ForegroundProperty, MutedBrush);
            DockPanel.SetDock(number, Dock.Left);
            row.Children.Add(number);

            var back = new MarkdownTextBlock(context) { FontSize = small, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            var span = new Span();
            span.Inlines.Add(new Run("↩"));
            Resource(span, TextElement.ForegroundProperty, "RiceMarkdownLinkBrush");
            back.Inlines!.Add(span);
            back.AddLink(0, 1, $"#fnref-{footnote.Number}", span);
            ToolTip.SetTip(back, RiceStrings.MarkdownBackToReference);
            DockPanel.SetDock(back, Dock.Right);
            row.Children.Add(back);

            var content = Blocks(footnote.Children, tight: true, depth: 0);
            content.SetValue(TextElement.FontSizeProperty, small);
            row.Children.Add(content);
            context.Anchors.TryAdd($"fn-{footnote.Number}", row);
            panel.Children.Add(row);
        }
        return panel;
    }

    internal static void Resource(StyledElement element, AvaloniaProperty property, string key) =>
        element.Bind(property, element.GetResourceObservable(key));

    // ---- inlines ----

    /// <summary>Adds inlines to one text block, counting characters so the links can be hit tested.</summary>
    private sealed class InlineBuilder(MarkdownRenderer renderer, MarkdownTextBlock block)
    {
        private int _position;

        // Avalonia does not raise or lower text runs, so super- and subscripts use the Unicode script characters.
        private const string Plain = "0123456789+-=()naeioxhklmnpstijruv";
        private const string Superscripts = "⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻⁼⁽⁾ⁿᵃᵉⁱᵒˣʰᵏˡᵐⁿᵖˢᵗⁱʲʳᵘᵛ";
        private const string Subscripts = "₀₁₂₃₄₅₆₇₈₉₊₋₌₍₎ₙₐₑᵢₒₓₕₖₗₘₙₚₛₜᵢⱼᵣᵤᵥ";
        private int _script;

        public static string? ToScript(string text, bool superscript)
        {
            var table = superscript ? Superscripts : Subscripts;
            var chars = new char[text.Length];
            for (var i = 0; i < text.Length; i++)
            {
                var index = Plain.IndexOf(char.ToLowerInvariant(text[i]));
                if (index < 0 || (text[i] != char.ToLowerInvariant(text[i]) && !char.IsDigit(text[i])))
                    return null;
                chars[i] = table[index];
            }
            return new string(chars);
        }

        private MarkdownRenderContext Context => renderer.Context;

        public void Add(InlineCollection target, IReadOnlyList<MdInline> inlines)
        {
            foreach (var inline in MarkdownHtml.Normalize(inlines))
                Add(target, inline);
        }

        private void Text(InlineCollection target, string text, Action<Run>? style = null)
        {
            if (text.Length == 0)
                return;
            if (_script != 0 && ToScript(text, _script > 0) is { } script)
                text = script;
            var run = new Run(text);
            style?.Invoke(run);
            target.Add(run);
            _position += text.Length;
        }

        private Span Span(InlineCollection target, IEnumerable<MdInline> children, Span span)
        {
            target.Add(span);
            Add(span.Inlines, children.ToList());
            return span;
        }

        private void Add(InlineCollection target, MdInline inline)
        {
            switch (inline)
            {
                case MdText t:
                    Text(target, t.Text);
                    break;
                case MdSoftBreak:
                    Text(target, " ");
                    break;
                case MdLineBreak:
                    Text(target, "\n");
                    break;
                case MdCode c:
                    Text(target, c.Text.Replace('\n', ' '), CodeStyle);
                    break;
                case MdStrong s:
                    Span(target, s.Children, new Bold());
                    break;
                case MdEmphasis e:
                    Span(target, e.Children, new Italic());
                    break;
                case MdStrikethrough s:
                    Span(target, s.Children, new Span { TextDecorations = TextDecorations.Strikethrough });
                    break;
                case MdLink l:
                    Link(target, l.Children, l.Url);
                    break;
                case MdImage i:
                    Image(target, i.Url, i.AltText, i.Title, null, null);
                    break;
                case MarkdownHtml.HtmlImage i:
                    Image(target, i.Url, i.AltText, i.Title, i.Width, i.Height);
                    break;
                case MdFootnoteReference f:
                {
                    var number = f.Definition.Number;
                    var start = _position;
                    var span = new Span();
                    Resource(span, TextElement.ForegroundProperty, "RiceMarkdownLinkBrush");
                    target.Add(span);
                    Text(span.Inlines, ToScript(number.ToString(), true)!);
                    block.AddLink(start, _position, $"#fn-{number}", span);
                    Context.Anchors.TryAdd($"fnref-{number}", block);
                    break;
                }
                case MdHtmlSpan h:
                    HtmlSpan(target, h);
                    break;
            }
        }

        private void CodeStyle(Run run)
        {
            run.FontFamily = Context.CodeFontFamily;
            run.FontSize = Math.Round(Context.FontSize * 0.9);
            Resource(run, TextElement.BackgroundProperty, "RiceMarkdownInlineCodeBrush");
        }

        private void HtmlSpan(InlineCollection target, MdHtmlSpan html)
        {
            var span = new Span();
            switch (html.Tag)
            {
                case "code" or "kbd" or "tt" or "samp":
                    span.FontFamily = Context.CodeFontFamily;
                    span.FontSize = Math.Round(Context.FontSize * 0.9);
                    Resource(span, TextElement.BackgroundProperty, "RiceMarkdownInlineCodeBrush");
                    break;
                case "sup" or "sub":
                {
                    var text = MdInline.PlainTextOf(html.Children);
                    var superscript = html.Tag == "sup";
                    // Text that has no script characters is just made smaller.
                    if (ToScript(text, superscript) is null)
                        span.FontSize = Math.Round(Context.FontSize * 0.75);
                    var previous = _script;
                    _script = superscript ? 1 : -1;
                    Span(target, html.Children, span);
                    _script = previous;
                    return;
                }
                case "u" or "ins":
                    span.TextDecorations = TextDecorations.Underline;
                    break;
                case "mark":
                    Resource(span, TextElement.BackgroundProperty, "RiceMarkdownTableHeaderBrush");
                    break;
                case "small":
                    span.FontSize = Math.Round(Context.FontSize * 0.85);
                    break;
            }
            Span(target, html.Children, span);
        }

        private void Link(InlineCollection target, IEnumerable<MdInline> children, string url)
        {
            var start = _position;
            var span = new Span();
            Resource(span, TextElement.ForegroundProperty, "RiceMarkdownLinkBrush");
            _currentLink = url;
            Span(target, children, span);
            _currentLink = null;
            block.AddLink(start, _position, url, span);
        }

        private string? _currentLink;

        private void Image(InlineCollection target, string url, string alt, string? title, double? width, double? height)
        {
            var image = new MarkdownImage(Context, url, alt, title, width, height);
            if (_currentLink is { } link)
            {
                // Images in links follow the link when clicked.
                image.Cursor = new Cursor(StandardCursorType.Hand);
                image.PointerReleased += (_, e) =>
                {
                    if (e.InitialPressMouseButton != MouseButton.Left)
                        return;
                    e.Handled = true;
                    Context.LinkClicked(image, link);
                };
            }
            target.Add(new InlineUIContainer(image) { BaselineAlignment = BaselineAlignment.Bottom });
            _position += 1;
        }
    }
}
