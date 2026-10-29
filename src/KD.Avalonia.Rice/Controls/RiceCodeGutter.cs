using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Line numbers beside the text of a <see cref="RiceCodeEditor"/>, following its scrolling; the number of the caret's
/// line is drawn in <c>Foreground</c>, the others in <see cref="NumberBrush"/>. Wrapped lines get a number only on their
/// first row.
/// </summary>
public class RiceCodeGutter : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<RiceCodeGutter>();

    public static readonly StyledProperty<IBrush?> NumberBrushProperty =
        AvaloniaProperty.Register<RiceCodeGutter, IBrush?>(nameof(NumberBrush));

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        Border.BackgroundProperty.AddOwner<RiceCodeGutter>();

    public static readonly StyledProperty<Thickness> PaddingProperty =
        Decorator.PaddingProperty.AddOwner<RiceCodeGutter>();

    private TextPresenter? _presenter;
    private int _lineCount = 1;

    static RiceCodeGutter()
    {
        AffectsRender<RiceCodeGutter>(ForegroundProperty, NumberBrushProperty, BackgroundProperty);
        AffectsMeasure<RiceCodeGutter>(PaddingProperty, TextElement.FontFamilyProperty, TextElement.FontSizeProperty);
        ClipToBoundsProperty.OverrideDefaultValue<RiceCodeGutter>(true);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public IBrush? NumberBrush
    {
        get => GetValue(NumberBrushProperty);
        set => SetValue(NumberBrushProperty, value);
    }

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Thickness Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>The presenter whose lines are numbered; set by the editor.</summary>
    internal TextPresenter? Presenter
    {
        get => _presenter;
        set
        {
            _presenter?.LayoutUpdated -= OnPresenterLayoutUpdated;
            _presenter?.PropertyChanged -= OnPresenterPropertyChanged;
            _presenter = value;
            _presenter?.LayoutUpdated += OnPresenterLayoutUpdated;
            _presenter?.PropertyChanged += OnPresenterPropertyChanged;
            InvalidateMeasure();
        }
    }

    private void OnPresenterPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextPresenter.CaretIndexProperty)
            InvalidateVisual();
    }

    private void OnPresenterLayoutUpdated(object? sender, EventArgs e)
    {
        var count = LineCount(_presenter?.Text);
        if (count.ToString(CultureInfo.InvariantCulture).Length != _lineCount.ToString(CultureInfo.InvariantCulture).Length)
            InvalidateMeasure();
        _lineCount = count;
        InvalidateVisual();
    }

    internal static int LineCount(string? text) => string.IsNullOrEmpty(text) ? 1 : text.AsSpan().Count('\n') + 1;

    private Typeface Typeface => new(TextElement.GetFontFamily(this));

    private double NumberFontSize => TextElement.GetFontSize(this);

    protected override Size MeasureOverride(Size availableSize)
    {
        // At least two digits, so the gutter does not grow at line 10 of a short file.
        var digits = Math.Max(2, _lineCount.ToString(CultureInfo.InvariantCulture).Length);
        var sample = Format(new string('0', digits), Foreground);
        return new Size(sample.Width + Padding.Left + Padding.Right, 0);
    }

    private FormattedText Format(string text, IBrush? brush) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface, NumberFontSize, brush);

    public override void Render(DrawingContext context)
    {
        if (Background is { } background)
            context.FillRectangle(background, new Rect(Bounds.Size));
        if (_presenter is not { } presenter || TopLevel.GetTopLevel(presenter) is null)
            return;
        var text = presenter.Text ?? "";
        var layout = presenter.TextLayout;
        var caretLine = CaretLine(text, presenter.CaretIndex);
        var line = 0;
        var top = 0.0;
        foreach (var textLine in layout.TextLines)
        {
            var start = textLine.FirstTextSourceIndex;
            var startsLine = start == 0 || start - 1 < text.Length && text[start - 1] == '\n';
            if (startsLine)
            {
                line++;
                var y = presenter.TranslatePoint(new Point(0, top), this)?.Y ?? top;
                if (y + textLine.Height >= 0 && y <= Bounds.Height)
                {
                    var number = Format(line.ToString(CultureInfo.InvariantCulture), line == caretLine ? Foreground : NumberBrush);
                    var x = Bounds.Width - Padding.Right - number.Width;
                    // On the baseline of the text, which may use another font size.
                    context.DrawText(number, new Point(x, y + textLine.Baseline - number.Baseline));
                }
            }
            top += textLine.Height;
        }
    }

    internal static int CaretLine(string text, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        return text.AsSpan(0, caret).Count('\n') + 1;
    }
}
