using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls.Diffs;

/// <summary>
/// The narrow strip along the right edge of a <see cref="RiceDiffView"/>: a mark for every changed row (red removed,
/// green added) at its place in the whole diff, and the visible part as a box. Clicking or dragging scrolls there.
/// </summary>
public class RiceDiffOverview : Control
{
    private RiceDiffView? _owner;
    private ScrollViewer? _scroller;
    private bool _dragging;

    internal RiceDiffView? Owner
    {
        get => _owner;
        set
        {
            _owner = value;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_owner is not { } owner)
            return;
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        var rows = owner.Rows;
        if (rows.Count == 0 || size.Height <= 0)
            return;
        var removed = owner.Brush("RiceDiffRemovedMarkBrush");
        var added = owner.Brush("RiceDiffAddedMarkBrush");
        var rowHeight = size.Height / rows.Count;
        var markHeight = Math.Max(2, rowHeight);
        var half = Math.Floor(size.Width / 2);
        for (var i = 0; i < rows.Count; i++)
        {
            var y = i * rowHeight;
            switch (rows[i].Kind)
            {
                case DiffRowKind.Removed when removed is not null:
                    context.FillRectangle(removed, new Rect(2, y, size.Width - 4, markHeight));
                    break;
                case DiffRowKind.Added when added is not null:
                    context.FillRectangle(added, new Rect(2, y, size.Width - 4, markHeight));
                    break;
                case DiffRowKind.Changed:
                    if (removed is not null)
                        context.FillRectangle(removed, new Rect(2, y, half - 2, markHeight));
                    if (added is not null)
                        context.FillRectangle(added, new Rect(half, y, size.Width - half - 2, markHeight));
                    break;
            }
        }

        // The visible part, from the scroll position (the rows are virtualized, so this is an estimate).
        if (Scroller is { Extent.Height: > 0 } scroller && owner.Brush("RiceHoverBrush") is { } hover)
        {
            var top = scroller.Offset.Y / scroller.Extent.Height * size.Height;
            var height = Math.Max(8, scroller.Viewport.Height / scroller.Extent.Height * size.Height);
            context.DrawRectangle(hover, new Pen(owner.Brush("RiceBorderBrush")), new Rect(0.5, top + 0.5, size.Width - 1, Math.Min(height, size.Height - top) - 1), 2, 2);
        }
    }

    private ScrollViewer? Scroller
    {
        get
        {
            var scroller = _owner?.RowsScrollViewer;
            if (scroller != _scroller)
            {
                _scroller?.ScrollChanged -= OnScrollChanged;
                _scroller = scroller;
                _scroller?.ScrollChanged += OnScrollChanged;
            }
            return _scroller;
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => InvalidateVisual();

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        _dragging = true;
        e.Pointer.Capture(this);
        ScrollTo(e);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
            ScrollTo(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    private void ScrollTo(PointerEventArgs e)
    {
        if (Bounds.Height > 0)
            _owner?.ScrollToFraction(Math.Clamp(e.GetPosition(this).Y / Bounds.Height, 0, 1));
    }
}
