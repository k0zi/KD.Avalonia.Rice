using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;
using KD.Avalonia.Rice.Controls.Markdowns;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// The text presenter of <see cref="RiceCodeEditor"/>: colors the text with <see cref="CodeHighlighter"/> and the
/// <c>RiceCode…Brush</c> resources of the theme.
/// </summary>
public class RiceCodeTextPresenter : TextPresenter
{
    public static readonly StyledProperty<string?> LanguageProperty =
        AvaloniaProperty.Register<RiceCodeTextPresenter, string?>(nameof(Language));

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

    private readonly Dictionary<CodeTokenKind, IBrush?> _brushes = [];
    private string? _highlightedText;
    private string? _highlightedLanguage;
    private IReadOnlyList<CodeToken> _tokens = [];

    public RiceCodeTextPresenter()
    {
        // A theme change replaces the brushes.
        ResourcesChanged += (_, _) =>
        {
            _brushes.Clear();
            InvalidateTextLayout();
        };
        ActualThemeVariantChanged += (_, _) =>
        {
            _brushes.Clear();
            InvalidateTextLayout();
        };
    }

    /// <summary>The language of the code as in a Markdown info string (<c>cs</c>, <c>xaml</c>, <c>json</c>, <c>bash</c>, …).</summary>
    public string? Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TextPresenter);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LanguageProperty)
            InvalidateTextLayout();
    }

    protected override TextLayout CreateTextLayout()
    {
        var layout = base.CreateTextLayout();
        // While an input method composes text the plain layout (with the underlined composition) is kept.
        var text = Text;
        if (string.IsNullOrEmpty(text) || !string.IsNullOrEmpty(PreeditText) || PasswordChar != default)
            return layout;
        var tokens = Tokens(text);
        if (tokens.Count == 0)
            return layout;

        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var italic = new Typeface(FontFamily, FontStyle.Italic, FontWeight, FontStretch);
        var overrides = new List<ValueSpan<TextRunProperties>>(tokens.Count);
        foreach (var token in tokens)
        {
            if (token.Start + token.Length > text.Length || Brush(token.Kind) is not { } brush)
                continue;
            var face = token.Kind == CodeTokenKind.Comment ? italic : typeface;
            overrides.Add(new ValueSpan<TextRunProperties>(token.Start, token.Length,
                new GenericTextRunProperties(face, FontSize, null, brush, null, BaselineAlignment.Baseline, null, FontFeatures)));
        }
        var colored = new TextLayout(text, typeface, FontSize, Foreground, TextAlignment, TextWrapping, null, null,
            FlowDirection, layout.MaxWidth, layout.MaxHeight, LineHeight, LetterSpacing, 0, FontFeatures, overrides);
        layout.Dispose();
        return colored;
    }

    // The layout is rebuilt on every caret and selection change: the tokens only when the text changed.
    private IReadOnlyList<CodeToken> Tokens(string text)
    {
        if (!ReferenceEquals(text, _highlightedText) || Language != _highlightedLanguage)
        {
            _tokens = CodeHighlighter.Highlight(text, Language);
            _highlightedText = text;
            _highlightedLanguage = Language;
        }
        return _tokens;
    }

    private IBrush? Brush(CodeTokenKind kind)
    {
        if (_brushes.TryGetValue(kind, out var brush))
            return brush;
        brush = this.TryFindResource(TokenBrushes[kind], ActualThemeVariant, out var value) ? value as IBrush : null;
        _brushes[kind] = brush;
        return brush;
    }
}
