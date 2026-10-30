using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls;

/// <summary>Lays out the children of a <see cref="DockSplitNode"/> by their sizes, with a splitter between each two.</summary>
internal sealed class DockSplitPanel : Panel
{
    public const double SplitterThickness = 5;

    private readonly RiceDockLayout _layout;
    private readonly List<Control> _items = [];
    private readonly List<DockSplitter> _splitters = [];
    private double[] _lengths = [];

    public DockSplitPanel(RiceDockLayout layout, DockSplitNode node)
    {
        _layout = layout;
        Node = node;
    }

    public DockSplitNode Node { get; }

    private bool Horizontal => Node.Orientation == Orientation.Horizontal;

    public void SetItems(IReadOnlyList<Control> items)
    {
        if (_items.SequenceEqual(items))
            return;
        Children.Clear();
        _items.Clear();
        _splitters.Clear();
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                var splitter = new DockSplitter(this, i - 1, Horizontal);
                _splitters.Add(splitter);
                Children.Add(splitter);
            }
            RiceDockLayout.Detach(items[i]);
            _items.Add(items[i]);
            Children.Add(items[i]);
        }
        InvalidateMeasure();
    }

    private double MinLength(int index) => Node.Children[index].IsDocument ? _layout.ContentMinSize : 60;

    private double[] ComputeLengths(Size size)
    {
        var total = (Horizontal ? size.Width : size.Height) - SplitterThickness * Math.Max(0, _items.Count - 1);
        return DockModel.Lengths(Node.Children.Select(c => c.Size).ToList(), total, _layout.ContentMinSize);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var finite = new Size(double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 600 : availableSize.Height);
        var lengths = ComputeLengths(finite);
        for (var i = 0; i < _items.Count && i < lengths.Length; i++)
            _items[i].Measure(Horizontal ? new Size(lengths[i], finite.Height) : new Size(finite.Width, lengths[i]));
        foreach (var splitter in _splitters)
            splitter.Measure(finite);
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _lengths = ComputeLengths(finalSize);
        var offset = 0.0;
        for (var i = 0; i < _items.Count && i < _lengths.Length; i++)
        {
            if (i > 0)
            {
                _splitters[i - 1].Arrange(Horizontal
                    ? new Rect(offset, 0, SplitterThickness, finalSize.Height)
                    : new Rect(0, offset, finalSize.Width, SplitterThickness));
                offset += SplitterThickness;
            }
            _items[i].Arrange(Horizontal
                ? new Rect(offset, 0, _lengths[i], finalSize.Height)
                : new Rect(0, offset, finalSize.Width, _lengths[i]));
            offset += _lengths[i];
        }
        return finalSize;
    }

    /// <summary>Moves the splitter after child <paramref name="index"/> by <paramref name="delta"/> from the given lengths.</summary>
    internal void Resize(int index, double startA, double startB, double delta)
    {
        var a = Node.Children[index];
        var b = Node.Children[index + 1];
        var sum = startA + startB;
        var newA = Math.Clamp(startA + delta, Math.Min(MinLength(index), sum / 2), Math.Max(sum - MinLength(index + 1), sum / 2));
        var newB = sum - newA;
        if (a.Size.IsStar && b.Size.IsStar)
        {
            var stars = a.Size.Value + b.Size.Value;
            a.Size = DockLength.Star(stars * newA / sum);
            b.Size = DockLength.Star(stars * newB / sum);
        }
        else
        {
            // The star side takes up whatever the pixel side gives.
            if (!a.Size.IsStar)
                a.Size = DockLength.Pixel(newA);
            if (!b.Size.IsStar)
                b.Size = DockLength.Pixel(newB);
        }
        InvalidateMeasure();
    }

    internal (double A, double B) LengthsAround(int index) =>
        index + 1 < _lengths.Length ? (_lengths[index], _lengths[index + 1]) : (0, 0);

    internal void OnResizeEnded() => _layout.OnLayoutChanged();
}

/// <summary>The band between two children of a split; drag it to share the room differently.</summary>
internal sealed class DockSplitter : Border
{
    private readonly DockSplitPanel _panel;
    private readonly int _index;
    private readonly bool _horizontal;
    private Point? _start;
    private (double A, double B) _startLengths;

    public DockSplitter(DockSplitPanel panel, int index, bool horizontal)
    {
        _panel = panel;
        _index = index;
        _horizontal = horizontal;
        Background = Brushes.Transparent;
        Cursor = new Cursor(horizontal ? StandardCursorType.SizeWestEast : StandardCursorType.SizeNorthSouth);
        Classes.Add("rice-dock-splitter");
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        _start = e.GetPosition(_panel);
        _startLengths = _panel.LengthsAround(_index);
        e.Pointer.Capture(this);
        PseudoClasses.Set(":dragging", true);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_start is not { } start)
            return;
        var moved = e.GetPosition(_panel) - start;
        _panel.Resize(_index, _startLengths.A, _startLengths.B, _horizontal ? moved.X : moved.Y);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_start is null)
            return;
        e.Pointer.Capture(null);
        End();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        End();
    }

    private void End()
    {
        if (_start is null)
            return;
        _start = null;
        PseudoClasses.Set(":dragging", false);
        _panel.OnResizeEnded();
    }
}
