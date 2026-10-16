using Avalonia;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>Draws lines with their arrow heads, labels, multiplicities and communication messages.</summary>
internal sealed class UmlEdgePainter(UmlStyle style)
{
    public sealed class Edge
    {
        public required List<Point> Points { get; init; }
        public UmlCurve Curve { get; init; }
        public UmlStroke Stroke { get; init; }
        public double Thickness { get; init; } = 1.25;
        public UmlArrowHead FromHead { get; init; }
        public UmlArrowHead ToHead { get; init; }
        public string? Label { get; init; }
        public UmlLabelDirection LabelDirection { get; init; }
        /// <summary>Where the label sits along the line (0–1).</summary>
        public double LabelAt { get; init; } = 0.5;
        /// <summary>+1 / -1 puts the label beside the line on that side of its normal (parallel lines), 0 on it.</summary>
        public int LabelSide { get; init; }
        public string? FromLabel { get; init; }
        public string? ToLabel { get; init; }
        public IReadOnlyList<UmlLinkMessage>? Messages { get; init; }
        public string? Note { get; init; }
        /// <summary>Messages go on the side of the line away from this point (the middle of the diagram).</summary>
        public Point? AwayFrom { get; init; }
    }

    /// <summary>How far the line stops before the tip, so it does not show through hollow heads.</summary>
    public static double HeadInset(UmlArrowHead head) => head switch
    {
        UmlArrowHead.Triangle or UmlArrowHead.FilledTriangle => 12,
        UmlArrowHead.Diamond or UmlArrowHead.FilledDiamond => 16,
        UmlArrowHead.Circle => 10,
        UmlArrowHead.Plus => 12,
        UmlArrowHead.Square => 8,
        _ => 0,
    };

    /// <summary>Paints the edge and returns the bounds of everything drawn (for the hit item).</summary>
    public Rect Paint(UmlScene scene, Edge edge, int item)
    {
        var points = edge.Points;
        if (points.Count < 2)
            return default;

        var tip = points[^1];
        var tail = points[0];
        var endDirection = UmlGeometry.Normalize(points[^1] - points[^2]);
        var startDirection = UmlGeometry.Normalize(points[0] - points[1]);
        if (edge.Curve == UmlCurve.Bezier && points.Count == 4)
        {
            endDirection = UmlGeometry.Normalize(points[3] - points[2]);
            startDirection = UmlGeometry.Normalize(points[0] - points[1]);
        }

        var line = points.ToList();
        if (edge.Curve != UmlCurve.Bezier)
        {
            UmlGeometry.ShortenEnd(line, HeadInset(edge.ToHead));
            UmlGeometry.ShortenStart(line, HeadInset(edge.FromHead));
        }
        scene.Add(new LinePrim { Points = line, Curve = edge.Curve, Style = edge.Stroke, Thickness = edge.Thickness }, item);
        if (edge.ToHead != UmlArrowHead.None)
            scene.Add(new HeadPrim { Tip = tip, Direction = endDirection, Head = edge.ToHead }, item);
        if (edge.FromHead != UmlArrowHead.None)
            scene.Add(new HeadPrim { Tip = tail, Direction = startDirection, Head = edge.FromHead }, item);

        var flat = UmlGeometry.Flatten(points, edge.Curve);
        var bounds = UmlGeometry.BoundsOf(flat).Inflate(8);

        var (mid, direction) = UmlGeometry.PointAt(flat, edge.LabelAt);
        var normal = new Vector(-direction.Y, direction.X);
        if (edge.Label is { Length: > 0 } label)
        {
            var at = mid;
            if (edge.LabelSide != 0)
            {
                var size = style.Measure(label, small: true);
                var side = normal * edge.LabelSide;
                at += side * (Math.Abs(side.X) * (size.Width / 2 + 9) + Math.Abs(side.Y) * (size.Height / 2 + 6));
            }
            bounds = bounds.Union(PaintLabel(scene, label, at, item, edge.LabelDirection, direction));
        }

        if (edge.Messages is { Count: > 0 } messages)
            bounds = bounds.Union(PaintMessages(scene, messages, mid, direction, normal, edge.AwayFrom, item));

        if (edge.FromLabel is { Length: > 0 } fromLabel)
            bounds = bounds.Union(PaintEndLabel(scene, fromLabel, flat[0], UmlGeometry.Normalize(flat[1] - flat[0]), item));
        if (edge.ToLabel is { Length: > 0 } toLabel)
            bounds = bounds.Union(PaintEndLabel(scene, toLabel, flat[^1], UmlGeometry.Normalize(flat[^2] - flat[^1]), item));

        if (edge.Note is { Length: > 0 } note)
        {
            var size = style.Measure(note);
            var anchor = mid + normal * 34;
            var rect = new Rect(anchor.X - size.Width / 2 - 10, anchor.Y - size.Height / 2 - 6, size.Width + 28, size.Height + 12);
            scene.Add(new LinePrim { Points = [mid, rect.Center], Style = UmlStroke.Dashed, Thickness = 1, Brush = UmlBrush.NoteBorder }, item);
            scene.Add(new ShapePrim { Shape = UmlShape.Note, Rect = rect, Fill = UmlBrush.Note, Stroke = UmlBrush.NoteBorder, Shadow = true }, item);
            scene.Add(style.Text(note, new Point(rect.X + 10, rect.Y + 6)), item);
            bounds = bounds.Union(rect);
        }
        return bounds;
    }

    /// <summary>Label on a small pill of the background color, centered on the line.</summary>
    public Rect PaintLabel(UmlScene scene, string label, Point at, int item, UmlLabelDirection labelDirection = UmlLabelDirection.None,
        Vector lineDirection = default)
    {
        var size = style.Measure(label, small: true);
        var arrowSpace = labelDirection == UmlLabelDirection.None ? 0 : 14;
        var pill = new Rect(at.X - (size.Width + arrowSpace) / 2 - 5, at.Y - size.Height / 2 - 2, size.Width + arrowSpace + 10, size.Height + 4);
        scene.Add(new ShapePrim { Shape = UmlShape.RoundedRectangle, Rect = pill, Radius = pill.Height / 2, Fill = UmlBrush.Background, Stroke = UmlBrush.None }, item);
        scene.Add(style.Text(label, new Point(pill.X + 5, pill.Y + 2), small: true, brush: UmlBrush.MutedText), item);
        if (labelDirection != UmlLabelDirection.None)
        {
            var d = labelDirection == UmlLabelDirection.Backward ? -lineDirection : lineDirection;
            var center = new Point(pill.Right - 11, pill.Center.Y);
            scene.Add(new HeadPrim { Tip = center + d * 4, Direction = d, Head = UmlArrowHead.FilledTriangle, Brush = UmlBrush.MutedText }, item);
        }
        return pill;
    }

    private Rect PaintMessages(UmlScene scene, IReadOnlyList<UmlLinkMessage> messages, Point mid, Vector direction, Vector normal,
        Point? awayFrom, int item)
    {
        // Messages stack beside the link, each one with an arrow in its direction.
        var sizes = messages.Select(m => style.Measure(m.Text, small: true)).ToList();
        var width = sizes.Max(s => s.Width) + 22;
        var height = sizes.Sum(s => s.Height + 2);
        Vector side;
        if (awayFrom is { } center && Math.Abs(Vector.Dot(mid - center, normal)) > 1)
            side = Vector.Dot(mid - center, normal) > 0 ? normal : -normal;
        else
            side = normal.X > 0 || (Math.Abs(normal.X) < 0.1 && normal.Y > 0) ? normal : -normal;
        var anchor = mid + side * (12 + Math.Abs(side.X) * width / 2 + Math.Abs(side.Y) * height / 2);
        var block = new Rect(anchor.X - width / 2, anchor.Y - height / 2, width, height);

        var y = block.Y;
        for (var i = 0; i < messages.Count; i++)
        {
            var d = messages[i].Direction == UmlLabelDirection.Backward ? -direction : direction;
            var arrowCenter = new Point(block.X + 7, y + sizes[i].Height / 2 + 1);
            scene.Add(new LinePrim { Points = [arrowCenter - d * 6, arrowCenter + d * 4], Thickness = 1.25, Brush = UmlBrush.Accent }, item);
            scene.Add(new HeadPrim { Tip = arrowCenter + d * 6, Direction = d, Head = UmlArrowHead.FilledTriangle, Brush = UmlBrush.Accent }, item);
            scene.Add(style.Text(messages[i].Text, new Point(block.X + 18, y + 1), small: true), item);
            y += sizes[i].Height + 2;
        }
        return block;
    }

    private Rect PaintEndLabel(UmlScene scene, string text, Point end, Vector inward, int item)
    {
        var size = style.Measure(text, small: true);
        var normal = new Vector(-inward.Y, inward.X);
        var anchor = end + inward * (12 + size.Width / 2 * Math.Abs(inward.X) + size.Height / 2 * Math.Abs(inward.Y))
                     + normal * (8 + size.Width / 2 * Math.Abs(normal.X) + size.Height / 2 * Math.Abs(normal.Y));
        var origin = new Point(anchor.X - size.Width / 2, anchor.Y - size.Height / 2);
        scene.Add(style.Text(text, origin, small: true, brush: UmlBrush.MutedText), item);
        return new Rect(origin, size);
    }
}
