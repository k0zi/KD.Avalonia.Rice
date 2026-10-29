using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using KD.Avalonia.Rice.Controls.Charts;
using KD.Avalonia.Rice.Controls.Uml;
using Material.Icons;
using Material.Icons.Avalonia;

namespace KD.Avalonia.Rice.Controls.Markdowns;

public class MarkdownLinkClickedEventArgs(RoutedEvent routedEvent, string url, Uri? uri) : RoutedEventArgs(routedEvent)
{
    /// <summary>The link as written in the document (<c>#anchor</c>, <c>docs/setup.md</c>, <c>https://…</c>).</summary>
    public string Url { get; } = url;

    /// <summary>The link resolved against <see cref="MarkdownView.BaseUri"/>; null for anchors and unreadable links.</summary>
    public Uri? Uri { get; } = uri;
}

/// <summary>
/// Shows Markdown text (CommonMark with the GitHub extensions) in the colors of the current Rice theme: headings,
/// lists, task lists, quotes, tables, syntax highlighted code, images, footnotes, <c>mermaid</c> and <c>plantuml</c>
/// code blocks as diagrams, and <c>chart</c> code blocks (JSON) as charts.
/// </summary>
[TemplatePart(PartScrollViewer, typeof(ScrollViewer))]
[TemplatePart(PartHost, typeof(Decorator))]
[TemplatePart(PartOverlay, typeof(Panel))]
[PseudoClasses(":empty", ":overlay")]
public class MarkdownView : TemplatedControl
{
    public const string PartScrollViewer = "PART_ScrollViewer";
    public const string PartHost = "PART_Host";
    public const string PartOverlay = "PART_Overlay";

    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Markdown));

    public static readonly DirectProperty<MarkdownView, MarkdownDocument?> DocumentProperty =
        AvaloniaProperty.RegisterDirect<MarkdownView, MarkdownDocument?>(nameof(Document), o => o.Document);

    public static readonly StyledProperty<Uri?> BaseUriProperty =
        AvaloniaProperty.Register<MarkdownView, Uri?>(nameof(BaseUri));

    public static readonly StyledProperty<bool> IsTextSelectionEnabledProperty =
        AvaloniaProperty.Register<MarkdownView, bool>(nameof(IsTextSelectionEnabled), true);

    public static readonly StyledProperty<bool> RenderDiagramsProperty =
        AvaloniaProperty.Register<MarkdownView, bool>(nameof(RenderDiagrams), true);

    public static readonly StyledProperty<double> MaxDiagramHeightProperty =
        AvaloniaProperty.Register<MarkdownView, double>(nameof(MaxDiagramHeight), 560);

    public static readonly StyledProperty<double> ContentMaxWidthProperty =
        AvaloniaProperty.Register<MarkdownView, double>(nameof(ContentMaxWidth), 900);

    public static readonly StyledProperty<FontFamily> CodeFontFamilyProperty =
        AvaloniaProperty.Register<MarkdownView, FontFamily>(nameof(CodeFontFamily),
            new FontFamily("Cascadia Mono, Consolas, JetBrains Mono, DejaVu Sans Mono, Liberation Mono, Noto Sans Mono, monospace"));

    public static readonly RoutedEvent<MarkdownLinkClickedEventArgs> LinkClickedEvent =
        RoutedEvent.Register<MarkdownView, MarkdownLinkClickedEventArgs>(nameof(LinkClicked), RoutingStrategies.Bubble);

    private ScrollViewer? _scrollViewer;
    private Decorator? _host;
    private Panel? _overlay;
    private MarkdownDocument? _document;
    private MarkdownRenderContext? _context;
    private bool _renderPending;

    public MarkdownView() => UpdatePseudoClasses();

    /// <summary>The Markdown text to show.</summary>
    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>The document parsed from <see cref="Markdown"/>.</summary>
    public MarkdownDocument? Document
    {
        get => _document;
        private set => SetAndRaise(DocumentProperty, ref _document, value);
    }

    /// <summary>Base of relative links and image paths, e.g. the folder of the Markdown file (<c>file:///…/docs/</c>).</summary>
    public Uri? BaseUri
    {
        get => GetValue(BaseUriProperty);
        set => SetValue(BaseUriProperty, value);
    }

    public bool IsTextSelectionEnabled
    {
        get => GetValue(IsTextSelectionEnabledProperty);
        set => SetValue(IsTextSelectionEnabledProperty, value);
    }

    /// <summary>Draws <c>mermaid</c>, <c>plantuml</c> and <c>chart</c> code blocks as diagrams; false shows them as code.</summary>
    public bool RenderDiagrams
    {
        get => GetValue(RenderDiagramsProperty);
        set => SetValue(RenderDiagramsProperty, value);
    }

    /// <summary>Diagrams in the text are scaled down to this height; the full size opens with the expand button.</summary>
    public double MaxDiagramHeight
    {
        get => GetValue(MaxDiagramHeightProperty);
        set => SetValue(MaxDiagramHeightProperty, value);
    }

    /// <summary>Width of the text column; wider views center it.</summary>
    public double ContentMaxWidth
    {
        get => GetValue(ContentMaxWidthProperty);
        set => SetValue(ContentMaxWidthProperty, value);
    }

    public FontFamily CodeFontFamily
    {
        get => GetValue(CodeFontFamilyProperty);
        set => SetValue(CodeFontFamilyProperty, value);
    }

    /// <summary>
    /// A link was clicked. Unless a handler sets <see cref="RoutedEventArgs.Handled"/>, anchors (<c>#heading</c>)
    /// scroll the view and other links open with the system launcher.
    /// </summary>
    public event EventHandler<MarkdownLinkClickedEventArgs>? LinkClicked
    {
        add => AddHandler(LinkClickedEvent, value);
        remove => RemoveHandler(LinkClickedEvent, value);
    }

    protected override Type StyleKeyOverride => typeof(MarkdownView);

    /// <summary>Scrolls to the heading with this anchor (<c>getting-started</c>, also <c>#getting-started</c>).</summary>
    public bool ScrollToHeading(string slug)
    {
        if (_context is null || _scrollViewer is null || _host?.Child is not { } content)
            return false;
        slug = Uri.UnescapeDataString(slug.TrimStart('#'));
        if (!_context.Anchors.TryGetValue(slug, out var target) || target.TranslatePoint(default, content) is not { } point)
            return false;
        _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, Math.Max(0, point.Y + (_host.Padding.Top) - 8));
        return true;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host?.PropertyChanged -= OnHostPropertyChanged;
        _scrollViewer = e.NameScope.Find<ScrollViewer>(PartScrollViewer);
        _host = e.NameScope.Find<Decorator>(PartHost);
        _overlay = e.NameScope.Find<Panel>(PartOverlay);
        _host?.PropertyChanged += OnHostPropertyChanged;
        ScheduleRender();
    }

    private void OnHostPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != BoundsProperty || _context is null || _host is null)
            return;
        var width = Math.Max(0, _host.Bounds.Width - _host.Padding.Left - _host.Padding.Right);
        if (Math.Abs(width - _context.AvailableWidth) < 0.5)
            return;
        _context.AvailableWidth = width;
        foreach (var image in _context.Images)
            image.UpdateSize();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == MarkdownProperty)
        {
            Document = string.IsNullOrEmpty(Markdown) ? null : MarkdownDocument.Parse(Markdown);
            UpdatePseudoClasses();
            ScheduleRender();
        }
        else if (property == BaseUriProperty || property == IsTextSelectionEnabledProperty || property == RenderDiagramsProperty
                 || property == CodeFontFamilyProperty || property == FontSizeProperty || property == MaxDiagramHeightProperty)
            ScheduleRender();
    }

    /// <summary>Changes are rendered once, after the current batch of property changes.</summary>
    private void ScheduleRender()
    {
        if (_renderPending)
            return;
        _renderPending = true;
        Dispatcher.UIThread.Post(RenderDocument, DispatcherPriority.Render);
    }

    private void RenderDocument()
    {
        _renderPending = false;
        if (_host is null)
            return;
        CloseOverlay();
        var document = Document;
        if (document is null)
        {
            _host.Child = null;
            _context = null;
            return;
        }

        var fontSize = FontSize > 0 ? FontSize : 14;
        _context = new MarkdownRenderContext
        {
            LinkClicked = OnLinkClicked,
            DiagramFactory = RenderDiagrams ? CreateDiagram : null,
            FontSize = fontSize,
            CodeFontFamily = CodeFontFamily,
            Selectable = IsTextSelectionEnabled,
            BaseUri = BaseUri,
            AvailableWidth = Math.Max(0, _host.Bounds.Width - _host.Padding.Left - _host.Padding.Right),
        };
        // The scroll position stays when the text is edited (live preview).
        var offset = _scrollViewer?.Offset ?? default;
        _host.Child = new MarkdownRenderer(_context).Render(document);
        if (_scrollViewer is { } scrollViewer && offset != default)
            Dispatcher.UIThread.Post(() => scrollViewer.Offset = offset, DispatcherPriority.Loaded);
    }

    private void OnLinkClicked(Control source, string url)
    {
        var uri = url.StartsWith('#') ? null : MarkdownImageLoader.Resolve(url, BaseUri);
        var args = new MarkdownLinkClickedEventArgs(LinkClickedEvent, url, uri);
        source.RaiseEvent(args);
        if (args.Handled)
            return;
        if (url.StartsWith('#'))
            ScrollToHeading(url);
        else if (uri is not null && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
            _ = launcher.LaunchUriAsync(uri);
    }

    // ---- diagrams ----

    private Control? CreateDiagram(MdCodeBlock code)
    {
        switch (code.Language)
        {
            case "plantuml" or "puml" or "uml":
            {
                var source = code.Code.Contains("@start", StringComparison.OrdinalIgnoreCase) ? code.Code : $"@startuml\n{code.Code}\n@enduml";
                var document = UmlDocument.Parse(source);
                if (document.Diagrams.Count == 0)
                    return null;
                var canvas = new UmlDiagramCanvas { Diagram = document.Diagrams[0] };
                return DiagramBlock(canvas, code.Code, () => new UmlDiagramView { Source = source, ShowGrid = true });
            }
            case "mermaid" or "mmd":
            {
                var document = MermaidDocument.Parse(code.Code);
                if (document.Diagram is null)
                    return ErrorBlock(code, document.Diagnostics.FirstOrDefault() is { } problem
                        ? string.Format(RiceStrings.MermaidParseError, problem.Line, problem.Message)
                        : null);
                var canvas = new UmlDiagramCanvas();
                MermaidView.ApplyTo(canvas, document.Diagram);
                return DiagramBlock(canvas, code.Code, () => new MermaidView { Source = code.Code });
            }
            case "chart":
            {
                var result = ChartJsonReader.Read(code.Code);
                if (result.Chart is not { } chart)
                    return ErrorBlock(code, result.Line > 0 ? string.Format(RiceStrings.ChartParseError, result.Line, result.Error) : result.Error);
                chart.Height = Math.Min(result.Height ?? 320, MaxDiagramHeight);
                chart.Margin = new Thickness(8);
                return Block(chart, code.Code, () => ChartJsonReader.Read(code.Code).Chart is { } full ? Padded(full) : new Panel());
            }
            default:
                return null;
        }
    }

    // The full size chart keeps a distance from the edges (and the close button).
    private static Control Padded(Control chart)
    {
        chart.Margin = new Thickness(24, 48, 24, 24);
        return chart;
    }

    /// <summary>A diagram that cannot be read: its code with the first problem above it.</summary>
    private Control ErrorBlock(MdCodeBlock code, string? problem)
    {
        var codeBlock = new MarkdownRenderer(_context!).CodeBlock(code);
        if (problem is null)
            return codeBlock;
        var message = new TextBlock
        {
            Text = problem,
            TextWrapping = TextWrapping.Wrap,
            FontSize = Math.Round(_context!.FontSize * 0.85),
        };
        MarkdownRenderer.Resource(message, TextBlock.ForegroundProperty, "RiceDangerForegroundBrush");
        var icon = new MaterialIcon { Kind = MaterialIconKind.AlertCircleOutline, Width = 16, Height = 16, Margin = new Thickness(0, 0, 8, 0) };
        MarkdownRenderer.Resource(icon, MaterialIcon.ForegroundProperty, "RiceDangerForegroundBrush");
        var bar = new Border
        {
            Child = new DockPanel { Children = { icon, message } },
            Padding = new Thickness(10, 6),
            CornerRadius = new CornerRadius(8, 8, 0, 0),
        };
        MarkdownRenderer.Resource(bar, Border.BackgroundProperty, "RiceDangerBrush");
        return new StackPanel { Children = { bar, codeBlock } };
    }

    /// <summary>A diagram scaled into the text, with buttons to copy its source and to open it in full size.</summary>
    internal Control DiagramBlock(UmlDiagramCanvas canvas, string source, Func<Control> fullView)
    {
        canvas.IsInteractive = false;
        canvas.ShowGrid = false;
        canvas.MaxHeight = MaxDiagramHeight;
        canvas.HorizontalAlignment = HorizontalAlignment.Center;
        MarkdownRenderer.Resource(canvas, UmlDiagramCanvas.BackgroundProperty, "RiceBackgroundBrush");
        return Block(canvas, source, fullView);
    }

    /// <summary>A diagram or chart card in the text, with buttons (shown on hover) to copy its source and to open it in full size.</summary>
    private Control Block(Control content, string source, Func<Control> fullView)
    {
        var renderer = new MarkdownRenderer(_context!);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(4), Opacity = 0 };
        var expand = new Button
        {
            Content = new MaterialIcon { Kind = MaterialIconKind.ArrowExpand, Width = 14, Height = 14 },
            Padding = new Thickness(6, 4),
            MinHeight = 0,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(6),
        };
        ToolTip.SetTip(expand, RiceStrings.MarkdownExpandDiagram);
        expand.Click += (_, _) => OpenOverlay(fullView());
        buttons.Children.Add(renderer.CopyButton(source));
        buttons.Children.Add(expand);

        var panel = new Panel { Children = { content, buttons } };
        var card = new Border
        {
            Child = panel,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Padding = new Thickness(4),
        };
        MarkdownRenderer.Resource(card, Border.BackgroundProperty, "RiceBackgroundBrush");
        MarkdownRenderer.Resource(card, Border.BorderBrushProperty, "RiceBorderBrush");
        card.PointerEntered += (_, _) => buttons.Opacity = 1;
        card.PointerExited += (_, _) => buttons.Opacity = 0;
        content.DoubleTapped += (_, _) => OpenOverlay(fullView());
        return card;
    }

    private void OpenOverlay(Control content)
    {
        if (_overlay is null)
            return;
        _overlay.Children.Clear();
        var close = new Button
        {
            Content = new MaterialIcon { Kind = MaterialIconKind.Close, Width = 16, Height = 16 },
            Padding = new Thickness(7),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(10),
            CornerRadius = new CornerRadius(8),
        };
        ToolTip.SetTip(close, RiceStrings.MarkdownCloseDiagram);
        close.Click += (_, _) => CloseOverlay();
        var card = new Border
        {
            Child = new Panel { Children = { content, close } },
            Margin = new Thickness(24),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
        };
        MarkdownRenderer.Resource(card, Border.BackgroundProperty, "RiceBackgroundBrush");
        MarkdownRenderer.Resource(card, Border.BorderBrushProperty, "RiceBorderBrush");
        MarkdownRenderer.Resource(card, Border.BoxShadowProperty, "RiceCardShadow3");
        _overlay.Children.Add(card);
        _overlay.IsVisible = true;
        PseudoClasses.Set(":overlay", true);
        Dispatcher.UIThread.Post(() => content.Focus(), DispatcherPriority.Loaded);
    }

    private void CloseOverlay()
    {
        if (_overlay is null || !_overlay.IsVisible)
            return;
        _overlay.IsVisible = false;
        _overlay.Children.Clear();
        PseudoClasses.Set(":overlay", false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && _overlay?.IsVisible == true)
        {
            CloseOverlay();
            e.Handled = true;
        }
    }

    private void UpdatePseudoClasses() => PseudoClasses.Set(":empty", Document is null || Document.Blocks.Count == 0);
}
