using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Text block that shows <see cref="SourceText"/> with the characters at <see cref="Highlights"/> emphasized
/// (bold, in <see cref="HighlightBrush"/>), e.g. the characters a search matched.
/// </summary>
public class RiceHighlightedText : TextBlock
{
    public static readonly StyledProperty<string?> SourceTextProperty =
        AvaloniaProperty.Register<RiceHighlightedText, string?>(nameof(SourceText));

    public static readonly StyledProperty<IReadOnlyList<int>?> HighlightsProperty =
        AvaloniaProperty.Register<RiceHighlightedText, IReadOnlyList<int>?>(nameof(Highlights));

    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<RiceHighlightedText, IBrush?>(nameof(HighlightBrush));

    public string? SourceText
    {
        get => GetValue(SourceTextProperty);
        set => SetValue(SourceTextProperty, value);
    }

    /// <summary>Indexes of the characters to emphasize.</summary>
    public IReadOnlyList<int>? Highlights
    {
        get => GetValue(HighlightsProperty);
        set => SetValue(HighlightsProperty, value);
    }

    /// <summary>Color of the emphasized characters; null keeps the text color.</summary>
    public IBrush? HighlightBrush
    {
        get => GetValue(HighlightBrushProperty);
        set => SetValue(HighlightBrushProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceTextProperty || change.Property == HighlightsProperty
            || change.Property == HighlightBrushProperty)
            UpdateInlines();
    }

    private void UpdateInlines()
    {
        var text = SourceText ?? "";
        var marked = Highlights is { Count: > 0 } highlights ? new HashSet<int>(highlights) : null;
        if (marked is null)
        {
            Inlines = null;
            Text = text;
            return;
        }

        // Runs of equally marked characters, alternating plain and emphasized.
        var inlines = new InlineCollection();
        var start = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            if (i < text.Length && marked.Contains(i) == marked.Contains(start))
                continue;
            var run = new Run(text[start..i]);
            if (marked.Contains(start))
            {
                run.FontWeight = FontWeight.Bold;
                if (HighlightBrush is { } brush)
                    run.Foreground = brush;
            }
            inlines.Add(run);
            start = i;
        }
        Inlines = inlines;
    }
}
