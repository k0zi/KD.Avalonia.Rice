using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls.Markdowns;

/// <summary>
/// Text of a paragraph, heading or cell. Avalonia has no hyperlink inline, so the character ranges of the links are
/// kept here and found under the pointer with the text layout: hand cursor, underline on hover, click.
/// </summary>
internal sealed class MarkdownTextBlock(MarkdownRenderContext context) : SelectableTextBlock
{
    private sealed record LinkRange(int Start, int End, string Url, Span Span);

    private const double ClickTolerance = 4;

    private readonly List<LinkRange> _links = [];
    private LinkRange? _hover;
    private LinkRange? _pressed;
    private Point _pressPoint;

    protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

    public void AddLink(int start, int end, string url, Span span)
    {
        if (end > start)
            _links.Add(new LinkRange(start, end, url, span));
    }

    private LinkRange? LinkAt(Point point)
    {
        if (_links.Count == 0)
            return null;
        var padding = Padding;
        var hit = TextLayout.HitTestPoint(new Point(point.X - padding.Left, point.Y - padding.Top));
        if (!hit.IsInside)
            return null;
        var index = hit.CharacterHit.FirstCharacterIndex;
        return _links.FirstOrDefault(l => index >= l.Start && index < l.End);
    }

    private void SetHover(LinkRange? link)
    {
        if (link == _hover)
            return;
        if (_hover is not null)
            _hover.Span.TextDecorations = null;
        _hover = link;
        if (link is not null)
            link.Span.TextDecorations = global::Avalonia.Media.TextDecorations.Underline;
        Cursor = link is not null ? new Cursor(StandardCursorType.Hand) : context.Selectable ? new Cursor(StandardCursorType.Ibeam) : Cursor.Default;
        ToolTip.SetTip(this, link?.Url);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (context.Selectable)
            base.OnPointerMoved(e);
        if (_pressed is null || !context.Selectable)
            SetHover(LinkAt(e.GetPosition(this)));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(null);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        _pressed = point.Properties.IsLeftButtonPressed ? LinkAt(point.Position) : null;
        _pressPoint = point.Position;
        if (context.Selectable)
            base.OnPointerPressed(e);
        else if (_pressed is not null)
            e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (context.Selectable)
            base.OnPointerReleased(e);
        var pressed = _pressed;
        _pressed = null;
        if (pressed is null || e.InitialPressMouseButton != MouseButton.Left)
            return;
        var position = e.GetPosition(this);
        var moved = position - _pressPoint;
        // A drag over the link selects text instead of following it.
        if (Math.Abs(moved.X) > ClickTolerance || Math.Abs(moved.Y) > ClickTolerance || LinkAt(position) != pressed)
            return;
        ClearSelection();
        e.Handled = true;
        context.LinkClicked(this, pressed.Url);
    }
}
