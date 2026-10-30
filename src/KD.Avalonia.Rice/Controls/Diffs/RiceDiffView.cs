using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KD.Avalonia.Rice.Controls.Markdowns;

namespace KD.Avalonia.Rice.Controls.Diffs;

/// <summary>
/// Shows the difference of two texts, like the diff of a code review: side by side (<see cref="DiffViewMode.SideBySide"/>)
/// or in one column (<see cref="DiffViewMode.Inline"/>), with the changed words marked inside the changed lines, syntax
/// colors (<see cref="Language"/>), unchanged runs folded down to <see cref="ContextLines"/> lines (click to unfold), an
/// overview of the changes along the right edge and F7 / Shift+F7 to jump between them.
/// The rows are virtualized, so long files scroll smoothly.
/// </summary>
public class RiceDiffView : TemplatedControl
{
    public static readonly StyledProperty<string?> OldTextProperty =
        AvaloniaProperty.Register<RiceDiffView, string?>(nameof(OldText));

    public static readonly StyledProperty<string?> NewTextProperty =
        AvaloniaProperty.Register<RiceDiffView, string?>(nameof(NewText));

    /// <summary>Title of the old side, e.g. a file name and revision; null: <see cref="RiceStrings.DiffOriginal"/>.</summary>
    public static readonly StyledProperty<string?> OldHeaderProperty =
        AvaloniaProperty.Register<RiceDiffView, string?>(nameof(OldHeader));

    public static readonly StyledProperty<string?> NewHeaderProperty =
        AvaloniaProperty.Register<RiceDiffView, string?>(nameof(NewHeader));

    public static readonly StyledProperty<string?> LanguageProperty =
        AvaloniaProperty.Register<RiceDiffView, string?>(nameof(Language));

    public static readonly StyledProperty<DiffViewMode> ViewModeProperty =
        AvaloniaProperty.Register<RiceDiffView, DiffViewMode>(nameof(ViewMode));

    public static readonly StyledProperty<bool> IgnoreWhitespaceProperty =
        AvaloniaProperty.Register<RiceDiffView, bool>(nameof(IgnoreWhitespace));

    public static readonly StyledProperty<int> ContextLinesProperty =
        AvaloniaProperty.Register<RiceDiffView, int>(nameof(ContextLines), 3, coerce: (_, value) => Math.Max(value, -1));

    public static readonly StyledProperty<bool> WrapLinesProperty =
        AvaloniaProperty.Register<RiceDiffView, bool>(nameof(WrapLines), true);

    public static readonly StyledProperty<FontFamily> CodeFontFamilyProperty =
        AvaloniaProperty.Register<RiceDiffView, FontFamily>(nameof(CodeFontFamily),
            new FontFamily("Cascadia Mono, Consolas, JetBrains Mono, DejaVu Sans Mono, Liberation Mono, Noto Sans Mono, monospace"));

    public static readonly StyledProperty<double> CodeFontSizeProperty =
        AvaloniaProperty.Register<RiceDiffView, double>(nameof(CodeFontSize), 13);

    public static readonly DirectProperty<RiceDiffView, DiffDocument?> DocumentProperty =
        AvaloniaProperty.RegisterDirect<RiceDiffView, DiffDocument?>(nameof(Document), o => o.Document);

    public static readonly DirectProperty<RiceDiffView, string> StatisticsProperty =
        AvaloniaProperty.RegisterDirect<RiceDiffView, string>(nameof(Statistics), o => o.Statistics);

    public static readonly DirectProperty<RiceDiffView, int> CurrentChangeProperty =
        AvaloniaProperty.RegisterDirect<RiceDiffView, int>(nameof(CurrentChange), o => o.CurrentChange);

    private readonly HashSet<int> _expanded = [];
    private readonly Dictionary<string, IBrush?> _brushes = [];
    private DiffDocument? _document;
    private IReadOnlyList<DiffRow> _rows = [];
    private string _statistics = "";
    private int _currentChange = -1;
    private bool _updatePosted;
    private ItemsControl? _rowsHost;
    private RiceDiffOverview? _overview;
    private Button? _previousButton;
    private Button? _nextButton;
    private TextBlock? _oldHeader;
    private TextBlock? _newHeader;
    private IReadOnlyList<IReadOnlyList<CodeToken>> _oldTokens = [];
    private IReadOnlyList<IReadOnlyList<CodeToken>> _newTokens = [];

    static RiceDiffView()
    {
        FocusableProperty.OverrideDefaultValue<RiceDiffView>(true);
    }

    public RiceDiffView()
    {
        ActualThemeVariantChanged += (_, _) => RefreshRows();
        ResourcesChanged += (_, _) => RefreshRows();
    }

    public string? OldText
    {
        get => GetValue(OldTextProperty);
        set => SetValue(OldTextProperty, value);
    }

    public string? NewText
    {
        get => GetValue(NewTextProperty);
        set => SetValue(NewTextProperty, value);
    }

    public string? OldHeader
    {
        get => GetValue(OldHeaderProperty);
        set => SetValue(OldHeaderProperty, value);
    }

    public string? NewHeader
    {
        get => GetValue(NewHeaderProperty);
        set => SetValue(NewHeaderProperty, value);
    }

    /// <summary>Language of the syntax colors, as in a Markdown info string (<c>cs</c>, <c>json</c>, …); null: plain text.</summary>
    public string? Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    public DiffViewMode ViewMode
    {
        get => GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    /// <summary>Lines that differ only in their whitespace count as equal.</summary>
    public bool IgnoreWhitespace
    {
        get => GetValue(IgnoreWhitespaceProperty);
        set => SetValue(IgnoreWhitespaceProperty, value);
    }

    /// <summary>Unchanged lines kept around a change; longer unchanged runs are folded. -1: nothing is folded. Default 3.</summary>
    public int ContextLines
    {
        get => GetValue(ContextLinesProperty);
        set => SetValue(ContextLinesProperty, value);
    }

    /// <summary>Wraps long lines; false: they are cut at the edge. Default true.</summary>
    public bool WrapLines
    {
        get => GetValue(WrapLinesProperty);
        set => SetValue(WrapLinesProperty, value);
    }

    public FontFamily CodeFontFamily
    {
        get => GetValue(CodeFontFamilyProperty);
        set => SetValue(CodeFontFamilyProperty, value);
    }

    public double CodeFontSize
    {
        get => GetValue(CodeFontSizeProperty);
        set => SetValue(CodeFontSizeProperty, value);
    }

    /// <summary>The computed difference; null before the first layout.</summary>
    public DiffDocument? Document
    {
        get => _document;
        private set => SetAndRaise(DocumentProperty, ref _document, value);
    }

    /// <summary>"+12 −3": added and removed lines.</summary>
    public string Statistics
    {
        get => _statistics;
        private set => SetAndRaise(StatisticsProperty, ref _statistics, value);
    }

    /// <summary>The change last jumped to (0 based), -1 before the first jump.</summary>
    public int CurrentChange
    {
        get => _currentChange;
        private set => SetAndRaise(CurrentChangeProperty, ref _currentChange, value);
    }

    /// <summary>The rows shown now.</summary>
    public IReadOnlyList<DiffRow> Rows => _rows;

    protected override Type StyleKeyOverride => typeof(RiceDiffView);

    /// <summary>Jumps to the next change (F7). False when there is none after the current one.</summary>
    public bool NextChange() => GoToChange(CurrentChange + 1);

    /// <summary>Jumps to the previous change (Shift+F7).</summary>
    public bool PreviousChange() => GoToChange(CurrentChange < 0 ? -1 : CurrentChange - 1);

    public bool GoToChange(int change)
    {
        if (_document is null || change < 0 || change >= _document.ChangeCount)
            return false;
        CurrentChange = change;
        var index = _rows.Select((r, i) => (r, i)).FirstOrDefault(x => x.r.ChangeIndex == change, (null!, -1)).i;
        if (index >= 0 && _rowsHost is not null)
        {
            // A little context above the change, when there is.
            _rowsHost.ScrollIntoView(Math.Min(_rows.Count - 1, index + 6));
            _rowsHost.ScrollIntoView(Math.Max(0, index - 2));
        }
        InvalidateRowVisuals();
        UpdateButtons();
        return true;
    }

    /// <summary>Shows the unchanged lines hidden by a fold row.</summary>
    public void ExpandFold(DiffRow fold)
    {
        if (fold.Kind != DiffRowKind.Fold || !_expanded.Add(fold.Block))
            return;
        BuildRows();
    }

    /// <summary>Folds the unchanged runs again.</summary>
    public void CollapseFolds()
    {
        _expanded.Clear();
        BuildRows();
    }

    /// <summary>The difference as a unified diff (<c>git diff</c> format).</summary>
    public string ToUnifiedDiff() =>
        (Document ?? DiffDocument.Compute(OldText, NewText, IgnoreWhitespace))
        .ToUnifiedDiff(OldHeader ?? "a", NewHeader ?? "b", ContextLines < 0 ? 3 : ContextLines);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _previousButton?.Click -= OnPreviousClick;
        _nextButton?.Click -= OnNextClick;
        _rowsHost = e.NameScope.Find<ItemsControl>("PART_Rows");
        _overview = e.NameScope.Find<RiceDiffOverview>("PART_Overview");
        _previousButton = e.NameScope.Find<Button>("PART_PreviousButton");
        _nextButton = e.NameScope.Find<Button>("PART_NextButton");
        _oldHeader = e.NameScope.Find<TextBlock>("PART_OldHeader");
        _newHeader = e.NameScope.Find<TextBlock>("PART_NewHeader");
        _previousButton?.Click += OnPreviousClick;
        _nextButton?.Click += OnNextClick;
        if (_previousButton is not null)
            ToolTip.SetTip(_previousButton, RiceStrings.DiffPreviousChange);
        if (_nextButton is not null)
            ToolTip.SetTip(_nextButton, RiceStrings.DiffNextChange);
        if (e.NameScope.Find<TextBlock>("PART_IdenticalText") is { } identical)
            identical.Text = RiceStrings.DiffIdentical;
        if (_rowsHost is not null)
            _rowsHost.ItemTemplate = new FuncDataTemplate<DiffRow>((_, _) => new RiceDiffRowView(this), supportsRecycling: true);
        _overview?.Owner = this;
        UpdateHeaders();
        PostUpdate();
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => PreviousChange();

    private void OnNextClick(object? sender, RoutedEventArgs e) => NextChange();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == OldTextProperty || property == NewTextProperty || property == IgnoreWhitespaceProperty
            || property == LanguageProperty)
        {
            _expanded.Clear();
            Document = null;
            PostUpdate();
        }
        else if (property == ViewModeProperty || property == ContextLinesProperty)
        {
            PseudoClasses.Set(":inline", ViewMode == DiffViewMode.Inline);
            UpdateHeaders();
            PostUpdate();
        }
        else if (property == WrapLinesProperty || property == CodeFontFamilyProperty || property == CodeFontSizeProperty)
            RefreshRows();
        else if (property == OldHeaderProperty || property == NewHeaderProperty)
            UpdateHeaders();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;
        e.Handled = (e.Key, e.KeyModifiers) switch
        {
            (Key.F7, KeyModifiers.None) or (Key.Down, KeyModifiers.Alt) => NextChange() || true,
            (Key.F7, KeyModifiers.Shift) or (Key.Up, KeyModifiers.Alt) => PreviousChange() || true,
            _ => false,
        };
    }

    // Several properties often change together (both texts, the language): the diff is computed once after them.
    private void PostUpdate()
    {
        if (_updatePosted)
            return;
        _updatePosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _updatePosted = false;
            Update();
        }, DispatcherPriority.Loaded);
    }

    private void Update()
    {
        if (Document is null)
        {
            var document = DiffDocument.Compute(OldText, NewText, IgnoreWhitespace);
            _oldTokens = LineTokens(OldText, Language, document.OldLines.Count);
            _newTokens = LineTokens(NewText, Language, document.NewLines.Count);
            Document = document;
            Statistics = $"+{document.AddedLines} −{document.RemovedLines}";
            PseudoClasses.Set(":identical", document.IsIdentical);
            CurrentChange = -1;
        }
        BuildRows();
    }

    private void BuildRows()
    {
        if (Document is null)
            return;
        _rows = Document.BuildRows(ViewMode, ContextLines, _expanded);
        NumberWidth = Math.Max(2, Math.Max(Document.OldLines.Count, Document.NewLines.Count).ToString().Length);
        _rowsHost?.ItemsSource = _rows;
        _overview?.InvalidateVisual();
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var count = Document?.ChangeCount ?? 0;
        _previousButton?.IsEnabled = CurrentChange > 0;
        _nextButton?.IsEnabled = CurrentChange < count - 1;
    }

    private void UpdateHeaders()
    {
        var oldHeader = OldHeader ?? RiceStrings.DiffOriginal;
        var newHeader = NewHeader ?? RiceStrings.DiffModified;
        _oldHeader?.Text = ViewMode == DiffViewMode.Inline ? $"{oldHeader} \u2192 {newHeader}" : oldHeader;
        _newHeader?.Text = newHeader;
    }

    // The rows keep their text layouts; after a font or theme change they are laid out and painted again.
    private void RefreshRows()
    {
        _brushes.Clear();
        if (_rowsHost is null)
            return;
        foreach (var row in _rowsHost.GetRealizedContainers().SelectMany(c => c.GetSelfAndVisualDescendants()).OfType<RiceDiffRowView>())
            row.Reset();
        _overview?.InvalidateVisual();
    }

    private void InvalidateRowVisuals()
    {
        if (_rowsHost is null)
            return;
        foreach (var row in _rowsHost.GetRealizedContainers().SelectMany(c => c.GetSelfAndVisualDescendants()).OfType<RiceDiffRowView>())
            row.InvalidateVisual();
        _overview?.InvalidateVisual();
    }

    internal int NumberWidth { get; private set; } = 2;

    internal ScrollViewer? RowsScrollViewer => _rowsHost?.GetTemplateDescendants().OfType<ScrollViewer>().FirstOrDefault();

    internal void ScrollToFraction(double fraction)
    {
        if (RowsScrollViewer is not { } scroller)
            return;
        var y = fraction * scroller.Extent.Height - scroller.Viewport.Height / 2;
        scroller.Offset = new Vector(scroller.Offset.X, Math.Clamp(y, 0, Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height)));
    }

    internal IBrush? Brush(string key)
    {
        if (_brushes.TryGetValue(key, out var brush))
            return brush;
        brush = this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;
        _brushes[key] = brush;
        return brush;
    }

    internal IReadOnlyList<CodeToken> TokensOf(bool oldSide, int? number)
    {
        var lines = oldSide ? _oldTokens : _newTokens;
        return number is { } n && n - 1 < lines.Count ? lines[n - 1] : [];
    }

    /// <summary>The syntax tokens of every line, relative to the line; tokens over several lines (comments) are split.</summary>
    internal static IReadOnlyList<IReadOnlyList<CodeToken>> LineTokens(string? text, string? language, int lineCount)
    {
        var result = new List<CodeToken>[lineCount];
        for (var i = 0; i < lineCount; i++)
            result[i] = [];
        if (string.IsNullOrEmpty(text) || !CodeHighlighter.IsSupported(language))
            return result;
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                starts.Add(i + 1);
        }
        foreach (var token in CodeHighlighter.Highlight(text, language))
        {
            var start = token.Start;
            var end = token.Start + token.Length;
            var line = starts.BinarySearch(start);
            if (line < 0)
                line = ~line - 1;
            while (start < end && line < lineCount)
            {
                var lineEnd = line + 1 < starts.Count ? starts[line + 1] - 1 : text.Length;
                var pieceEnd = Math.Min(end, lineEnd);
                if (pieceEnd > start)
                    result[line].Add(new CodeToken(start - starts[line], pieceEnd - start, token.Kind));
                line++;
                start = line < starts.Count ? starts[line] : end;
            }
        }
        return result;
    }
}
