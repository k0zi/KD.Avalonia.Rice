using Avalonia;
using Avalonia.Media;
using KD.Avalonia.Rice.Controls.Uml;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.Controls.Charts;

/// <summary>Paints a <see cref="ChartScene"/>. The scene changes on every animation frame, so little is cached.</summary>
internal sealed class ChartRenderer(Func<ChartBrush, IBrush?> brushes, AvaloniaTextMeasurer text)
{
    private static readonly IBrush DarkText = new SolidColorBrush(Color.Parse("#1A1A1A"));
    private readonly Dictionary<Color, IBrush> _colors = [];
    private readonly Dictionary<(ChartPaint, double, bool), IPen?> _pens = [];

    /// <summary>Paints the scene; <paramref name="opacityOf"/> gives the extra opacity of an item's prims (hover dimming).</summary>
    public void Render(DrawingContext context, ChartScene scene, Func<int, double> opacityOf)
    {
        foreach (var prim in scene.Prims)
        {
            var opacity = prim.Opacity * (prim.Item >= 0 ? opacityOf(prim.Item) : 1);
            if (opacity <= 0.001)
                continue;
            using var _ = opacity < 0.999 ? context.PushOpacity(opacity) : default(DrawingContext.PushedState?);
            switch (prim)
            {
                case RectPrim r:
                    if (r.Rect.Width > 0 && r.Rect.Height > 0)
                        context.DrawRectangle(Brush(r.Fill), Pen(r.Stroke, r.Thickness), new RoundedRect(r.Rect, r.Radius));
                    break;
                case PathPrim p when p.Points.Count >= 2:
                    context.DrawGeometry(Brush(p.Fill), Pen(p.Stroke, p.Thickness, p.Dashed), PathGeometry(p));
                    break;
                case ArcPrim a when a.SweepAngle > 0.01 && a.Radius > 0:
                    context.DrawGeometry(Brush(a.Fill), a.Thickness > 0 ? Pen(a.Stroke, a.Thickness) : null, ArcGeometry(a.Center, a.Radius, a.InnerRadius, a.StartAngle, a.SweepAngle));
                    break;
                case CirclePrim c when c.Radius > 0:
                    context.DrawEllipse(Brush(c.Fill), Pen(c.Stroke, c.Thickness), c.Center, c.Radius, c.Radius);
                    break;
                case LabelPrim t when t.Text.Length > 0:
                    var brush = t.On is { } on ? Contrasting(Brush(on)) : Brush(t.Brush);
                    context.DrawText(text.Format(t.Text, t.FontSize, t.Bold, false, brush), t.Origin);
                    break;
            }
        }
    }

    public IBrush? Brush(ChartPaint paint)
    {
        if (paint.Color is { } color)
        {
            if (!_colors.TryGetValue(color, out var brush))
                _colors[color] = brush = new SolidColorBrush(color);
            return brush;
        }
        return paint.Brush == ChartBrush.None ? null : brushes(paint.Brush);
    }

    public IPen? Pen(ChartPaint paint, double thickness, bool dashed = false)
    {
        if (paint.IsNone || thickness <= 0)
            return null;
        if (_pens.TryGetValue((paint, thickness, dashed), out var pen))
            return pen;
        var brush = Brush(paint);
        pen = brush is null ? null : new Pen(brush, thickness, dashed ? new DashStyle([4, 3], 0) : null, PenLineCap.Round, PenLineJoin.Round);
        _pens[(paint, thickness, dashed)] = pen;
        return pen;
    }

    /// <summary>Dark or white text, whichever reads better on the fill.</summary>
    private static IBrush Contrasting(IBrush? fill) =>
        fill is ISolidColorBrush solid && ColorMath.Luminance(solid.Color) > 0.6 ? DarkText : Brushes.White;

    // ---- geometry ----

    private static Geometry PathGeometry(PathPrim p)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        var points = p.Points;
        ctx.BeginFigure(points[0], p.Closed || !p.Fill.IsNone);
        var smooth = Math.Min(p.SmoothCount, points.Count);
        var i = 1;
        if (smooth > 2)
        {
            foreach (var (c1, c2, end) in SmoothSegments(points, smooth))
                ctx.CubicBezierTo(c1, c2, end);
            i = smooth;
        }
        for (; i < points.Count; i++)
            ctx.LineTo(points[i]);
        ctx.EndFigure(p.Closed);
        return geometry;
    }

    /// <summary>
    /// Cubic Bézier segments through the first <paramref name="count"/> points (Catmull-Rom tangents), with the control
    /// points kept within the y range of their segment, so the curve never overshoots the data.
    /// </summary>
    public static IEnumerable<(Point C1, Point C2, Point End)> SmoothSegments(IReadOnlyList<Point> points, int count)
    {
        for (var i = 0; i < count - 1; i++)
        {
            var p0 = points[Math.Max(0, i - 1)];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = points[Math.Min(count - 1, i + 2)];
            var lo = Math.Min(p1.Y, p2.Y);
            var hi = Math.Max(p1.Y, p2.Y);
            var c1 = new Point(p1.X + (p2.X - p0.X) / 6, Math.Clamp(p1.Y + (p2.Y - p0.Y) / 6, lo, hi));
            var c2 = new Point(p2.X - (p3.X - p1.X) / 6, Math.Clamp(p2.Y - (p3.Y - p1.Y) / 6, lo, hi));
            yield return (c1, c2, p2);
        }
    }

    /// <summary>Ring segment (a slice when <paramref name="inner"/> is 0); angles in degrees clockwise from the top.</summary>
    public static Geometry ArcGeometry(Point center, double radius, double inner, double start, double sweep)
    {
        sweep = Math.Min(sweep, 360);
        if (sweep >= 359.99)
        {
            var ring = new EllipseGeometry(new Rect(center, center).Inflate(radius));
            return inner > 0 ? new CombinedGeometry(GeometryCombineMode.Exclude, ring, new EllipseGeometry(new Rect(center, center).Inflate(inner))) : ring;
        }
        Point At(double r, double degrees)
        {
            var radians = (degrees - 90) * Math.PI / 180;
            return new Point(center.X + r * Math.Cos(radians), center.Y + r * Math.Sin(radians));
        }
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var large = sweep > 180;
            if (inner > 0)
            {
                ctx.BeginFigure(At(radius, start), true);
                ctx.ArcTo(At(radius, start + sweep), new Size(radius, radius), 0, large, SweepDirection.Clockwise);
                ctx.LineTo(At(inner, start + sweep));
                ctx.ArcTo(At(inner, start), new Size(inner, inner), 0, large, SweepDirection.CounterClockwise);
            }
            else
            {
                ctx.BeginFigure(center, true);
                ctx.LineTo(At(radius, start));
                ctx.ArcTo(At(radius, start + sweep), new Size(radius, radius), 0, large, SweepDirection.Clockwise);
            }
            ctx.EndFigure(true);
        }
        return geometry;
    }
}
