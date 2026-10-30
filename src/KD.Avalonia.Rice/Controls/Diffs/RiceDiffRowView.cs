using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;
using KD.Avalonia.Rice.Controls.Markdowns;

namespace KD.Avalonia.Rice.Controls.Diffs;

/// <summary>
/// One row of a <see cref="RiceDiffView"/> (its <c>DataContext</c> is a <see cref="DiffRow"/>), drawn directly:
/// line numbers, the +/− sign, the line with syntax colors and the changed words marked, wrapped to the width.
/// </summary>
internal sealed class RiceDiffRowView : Control
{
    private const double TextPadding = 8;
    private const double SignWidth = 16;
    private const double VerticalPadding = 1;

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

    private readonly RiceDiffView _owner;
    private TextLayout? _oldLayout;
    private TextLayout? _newLayout;
    private double _lineHeight;
    private double _numberWidth;
    private double _layoutWidth = -1;

    public RiceDiffRowView(RiceDiffView owner)
    {
        _owner = owner;
        ClipToBounds = true;
    }

    private DiffRow? Row => DataContext as DiffRow;

    private bool SideBySide => _owner.ViewMode == DiffViewMode.SideBySide;

    private Typeface Typeface => new(_owner.CodeFontFamily);

    internal void Reset()
    {
        _oldLayout?.Dispose();
        _newLayout?.Dispose();
        _oldLayout = _newLayout = null;
        _layoutWidth = -1;
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Cursor = Row?.Kind == DiffRowKind.Fold ? new Cursor(StandardCursorType.Hand) : null;
        Reset();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 1000 : availableSize.Width;
        EnsureLayouts(width);
        if (Row is not { } row)
            return default;
        if (row.Kind == DiffRowKind.Fold)
            return new Size(0, _lineHeight + 10);
        var height = Math.Max(_lineHeight, Math.Max(_oldLayout?.Height ?? 0, _newLayout?.Height ?? 0));
        return new Size(0, height + 2 * VerticalPadding);
    }

    private void EnsureLayouts(double width)
    {
        if (Math.Abs(width - _layoutWidth) < 0.5 && (_oldLayout is not null || _newLayout is not null || Row?.Kind == DiffRowKind.Fold))
            return;
        _oldLayout?.Dispose();
        _newLayout?.Dispose();
        _oldLayout = _newLayout = null;
        _layoutWidth = width;
        using (var sample = new TextLayout("0", Typeface, _owner.CodeFontSize, null))
        {
            _lineHeight = sample.Height;
            _numberWidth = sample.WidthIncludingTrailingWhitespace * _owner.NumberWidth + 2 * TextPadding;
        }
        if (Row is not { } row || row.Kind == DiffRowKind.Fold)
            return;
        if (SideBySide)
        {
            var textWidth = Math.Max(20, width / 2 - _numberWidth - SignWidth - TextPadding);
            if (row.OldText is not null)
                _oldLayout = Layout(row.OldText, _owner.TokensOf(true, row.OldNumber), textWidth);
            if (row.NewText is not null)
                _newLayout = Layout(row.NewText, _owner.TokensOf(false, row.NewNumber), textWidth);
        }
        else
        {
            var textWidth = Math.Max(20, width - 2 * _numberWidth - SignWidth - TextPadding);
            var oldSide = row.Kind == DiffRowKind.Removed;
            var text = oldSide ? row.OldText : row.NewText ?? row.OldText;
            if (text is not null)
                _newLayout = Layout(text, _owner.TokensOf(oldSide || row.NewText is null, oldSide ? row.OldNumber : row.NewNumber ?? row.OldNumber), textWidth);
        }
    }

    private TextLayout Layout(string text, IReadOnlyList<CodeToken> tokens, double width)
    {
        var typeface = Typeface;
        var italic = new Typeface(_owner.CodeFontFamily, FontStyle.Italic);
        var overrides = new List<ValueSpan<TextRunProperties>>();
        foreach (var token in tokens)
        {
            var length = Math.Min(token.Length, text.Length - token.Start);
            if (length <= 0 || _owner.Brush(TokenBrushes[token.Kind]) is not { } brush)
                continue;
            overrides.Add(new ValueSpan<TextRunProperties>(token.Start, length,
                new GenericTextRunProperties(token.Kind == CodeTokenKind.Comment ? italic : typeface, _owner.CodeFontSize, null, brush)));
        }
        return new TextLayout(text, typeface, _owner.CodeFontSize, _owner.Brush("RiceForegroundBrush"), TextAlignment.Left,
            _owner.WrapLines ? TextWrapping.Wrap : TextWrapping.NoWrap, null, null, FlowDirection.LeftToRight,
            _owner.WrapLines ? width : double.PositiveInfinity, double.PositiveInfinity, double.NaN, 0, 0, null, overrides);
    }

    public override void Render(DrawingContext context)
    {
        if (Row is not { } row)
            return;
        EnsureLayouts(Bounds.Width);
        var size = Bounds.Size;
        if (row.Kind == DiffRowKind.Fold)
        {
            RenderFold(context, row, size);
            return;
        }

        if (SideBySide)
        {
            var half = Math.Floor(size.Width / 2);
            var oldKind = row.Kind switch
            {
                DiffRowKind.Removed or DiffRowKind.Changed => SideKind.Removed,
                DiffRowKind.Added => SideKind.Empty,
                _ => SideKind.Context,
            };
            var newKind = row.Kind switch
            {
                DiffRowKind.Added or DiffRowKind.Changed => SideKind.Added,
                DiffRowKind.Removed => SideKind.Empty,
                _ => SideKind.Context,
            };
            RenderSide(context, new Rect(0, 0, half, size.Height), oldKind, row.OldNumber, null, _oldLayout, row.OldChanges);
            RenderSide(context, new Rect(half + 1, 0, size.Width - half - 1, size.Height), newKind, row.NewNumber, null, _newLayout, row.NewChanges);
            if (_owner.Brush("RiceBorderBrush") is { } divider)
                context.FillRectangle(divider, new Rect(half, 0, 1, size.Height));
        }
        else
        {
            var changes = row.Kind == DiffRowKind.Removed ? row.OldChanges : row.NewChanges;
            var kind = row.Kind switch
            {
                DiffRowKind.Removed => SideKind.Removed,
                DiffRowKind.Added => SideKind.Added,
                _ => SideKind.Context,
            };
            RenderSide(context, new Rect(size), kind, row.OldNumber, row.NewNumber, _newLayout, changes, inline: true);
        }

        // The change last jumped to carries an accent bar.
        if (row.ChangeIndex >= 0 && row.ChangeIndex == _owner.CurrentChange && _owner.Brush("RiceAccentBrush") is { } accent)
            context.FillRectangle(accent, new Rect(0, 0, 3, size.Height));
    }

    // What one side of a row shows; Empty is the side without a line of a one sided row.
    private enum SideKind
    {
        Context,
        Removed,
        Added,
        Empty,
    }

    private void RenderSide(DrawingContext context, Rect cell, SideKind kind, int? number, int? secondNumber, TextLayout? layout,
        IReadOnlyList<TextRange> changes, bool inline = false)
    {
        var (background, word, mark, sign) = kind switch
        {
            SideKind.Removed => ("RiceDiffRemovedBrush", "RiceDiffRemovedWordBrush", "RiceDiffRemovedMarkBrush", "−"),
            SideKind.Added => ("RiceDiffAddedBrush", "RiceDiffAddedWordBrush", "RiceDiffAddedMarkBrush", "+"),
            SideKind.Empty => ("RiceDiffEmptyBrush", null, null, null),
            _ => ((string?)null, (string?)null, (string?)null, (string?)null),
        };
        if (background is not null && _owner.Brush(background) is { } fill)
            context.FillRectangle(fill, cell);

        var muted = _owner.Brush("RiceForegroundMutedBrush");
        var x = cell.X;
        DrawNumber(context, number, x, muted);
        x += _numberWidth;
        if (inline)
        {
            DrawNumber(context, secondNumber, x, muted);
            x += _numberWidth;
        }
        if (sign is not null)
        {
            var signText = Text(sign, mark is null ? muted : _owner.Brush(mark));
            context.DrawText(signText, new Point(x + (SignWidth - signText.Width) / 2, VerticalPadding));
        }
        x += SignWidth;
        if (layout is null)
            return;

        var origin = new Point(x, VerticalPadding);
        using (context.PushClip(new Rect(x, 0, Math.Max(0, cell.Right - x), cell.Height)))
        {
            if (word is not null && _owner.Brush(word) is { } wordBrush)
            {
                foreach (var range in changes)
                {
                    foreach (var rect in layout.HitTestTextRange(range.Start, range.Length))
                        context.FillRectangle(wordBrush, rect.Translate(origin), 2);
                }
            }
            layout.Draw(context, origin);
        }
    }

    private void DrawNumber(DrawingContext context, int? number, double x, IBrush? brush)
    {
        if (number is not { } n)
            return;
        var text = Text(n.ToString(CultureInfo.InvariantCulture), brush);
        context.DrawText(text, new Point(x + _numberWidth - TextPadding - text.Width, VerticalPadding));
    }

    private void RenderFold(DrawingContext context, DiffRow row, Size size)
    {
        if (_owner.Brush(IsPointerOver ? "RiceHoverBrush" : "RiceDiffEmptyBrush") is { } fill)
            context.FillRectangle(fill, new Rect(size));
        var text = Text("⋯  " + string.Format(CultureInfo.CurrentCulture, RiceStrings.DiffHiddenLines, row.HiddenCount),
            _owner.Brush("RiceForegroundMutedBrush"));
        context.DrawText(text, new Point(_numberWidth + SignWidth, (size.Height - text.Height) / 2));
    }

    private FormattedText Text(string text, IBrush? brush) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface, _owner.CodeFontSize, brush);

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (Row?.Kind == DiffRowKind.Fold)
            InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (Row?.Kind == DiffRowKind.Fold)
            InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Row is { Kind: DiffRowKind.Fold } row && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _owner.ExpandFold(row);
            e.Handled = true;
        }
    }
}
