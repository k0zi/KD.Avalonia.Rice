using Avalonia;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>
/// Paints a <see cref="UmlScene"/>. Geometries, pens and formatted texts are cached; a new renderer is made when the
/// scene, the brushes or the font change.
/// </summary>
internal sealed class UmlRenderer(UmlScene scene, Func<UmlBrush, IBrush?> brushes, AvaloniaTextMeasurer text, BoxShadows shadow)
{
    private readonly Dictionary<UmlPrim, Geometry> _geometries = [];
    private readonly Dictionary<TextPrim, FormattedText> _texts = [];
    private readonly Dictionary<(UmlBrush, double, UmlStroke), IPen?> _pens = [];
    private readonly IBrush _shadowBrush = new SolidColorBrush(shadow.Count > 0 ? shadow[0].Color : Color.FromArgb(0x30, 0, 0, 0));

    public UmlScene Scene { get; } = scene;

    public void Render(DrawingContext context)
    {
        foreach (var prim in Scene.Prims)
        {
            switch (prim)
            {
                case ShapePrim s:
                    DrawShape(context, s);
                    break;
                case LinePrim l:
                    DrawLine(context, l);
                    break;
                case HeadPrim h:
                    DrawHead(context, h);
                    break;
                case GlyphPrim g:
                    DrawGlyph(context, g);
                    break;
                case TextPrim t:
                    DrawText(context, t);
                    break;
                case WedgePrim w:
                    DrawWedge(context, w);
                    break;
            }
        }
    }

    public IPen? Pen(UmlBrush role, double thickness, UmlStroke style = UmlStroke.Solid)
    {
        if (_pens.TryGetValue((role, thickness, style), out var pen))
            return pen;
        var brush = brushes(role);
        pen = brush is null || role == UmlBrush.None ? null : new Pen(brush, thickness,
            style switch
            {
                UmlStroke.Dashed => new DashStyle([4, 3], 0),
                UmlStroke.Dotted => new DashStyle([0.1, 2.6], 0),
                _ => null,
            },
            style == UmlStroke.Dotted ? PenLineCap.Round : PenLineCap.Flat, PenLineJoin.Round);
        _pens[(role, thickness, style)] = pen;
        return pen;
    }

    private IBrush? Brush(UmlBrush role) => role == UmlBrush.None ? null : brushes(role);

    // ---- shapes ----

    private void DrawShape(DrawingContext context, ShapePrim s)
    {
        var fill = Brush(s.Fill);
        var pen = Pen(s.Stroke, s.Thickness, s.StrokeStyle);
        var r = s.Rect;
        switch (s.Shape)
        {
            case UmlShape.Rectangle:
                context.DrawRectangle(fill, pen, new RoundedRect(r), s.Shadow ? shadow : default);
                return;
            case UmlShape.RoundedRectangle:
                context.DrawRectangle(fill, pen, new RoundedRect(r, s.Radius), s.Shadow ? shadow : default);
                return;
            case UmlShape.TopRounded:
                context.DrawRectangle(fill, pen, new RoundedRect(r, new CornerRadius(s.Radius, s.Radius, 0, 0)), default);
                return;
            case UmlShape.Ellipse or UmlShape.DoubleCircle:
                if (s.Shadow)
                    context.DrawEllipse(_shadowBrush, null, r.Translate(new Vector(0, 2)));
                context.DrawEllipse(fill, pen, r);
                if (s.Shape == UmlShape.DoubleCircle)
                    context.DrawEllipse(null, pen, r.Deflate(4));
                return;
        }

        var geometry = GeometryOf(s);
        if (s.Shadow)
        {
            using (context.PushTransform(Matrix.CreateTranslation(0, 2)))
                context.DrawGeometry(_shadowBrush, null, geometry);
        }
        context.DrawGeometry(fill, pen, geometry);

        // Second parts drawn in the header color: the frame title tag, the 3D faces of a node, the top of a cylinder.
        var header = Brush(UmlBrush.Header);
        switch (s.Shape)
        {
            case UmlShape.Frame:
                context.DrawGeometry(header, pen, Polygon(
                    r.TopLeft, new Point(r.X + s.Extra, r.Y), new Point(r.X + s.Extra, r.Y + s.Extra2 - 6),
                    new Point(r.X + s.Extra - 6, r.Y + s.Extra2), new Point(r.X, r.Y + s.Extra2)));
                break;
            case UmlShape.Node3D:
            {
                var d = s.Extra;
                context.DrawGeometry(header, pen, Polygon(new Point(r.X, r.Y + d), new Point(r.X + d, r.Y), r.TopRight, new Point(r.Right - d, r.Y + d)));
                context.DrawGeometry(header, pen, Polygon(new Point(r.Right - d, r.Y + d), r.TopRight, new Point(r.Right, r.Bottom - d), new Point(r.Right - d, r.Bottom)));
                break;
            }
            case UmlShape.Cylinder:
                context.DrawEllipse(header, pen, new Rect(r.X, r.Y, r.Width, s.Extra * 2));
                break;
            case UmlShape.Note:
            {
                const double fold = 10;
                context.DrawGeometry(Brush(UmlBrush.NoteBorder), null, Polygon(new Point(r.Right - fold, r.Y), new Point(r.Right - fold, r.Y + fold), new Point(r.Right, r.Y + fold)));
                break;
            }
            case UmlShape.Collections:
            {
                var front = new Rect(r.X, r.Y + 4, r.Width - 4, r.Height - 4);
                context.DrawRectangle(fill, pen, new RoundedRect(front, s.Radius));
                break;
            }
            case UmlShape.Subroutine when pen is not null:
                context.DrawLine(pen, new Point(r.X + 8, r.Y), new Point(r.X + 8, r.Bottom));
                context.DrawLine(pen, new Point(r.Right - 8, r.Y), new Point(r.Right - 8, r.Bottom));
                break;
        }
    }

    private Geometry GeometryOf(ShapePrim s)
    {
        if (_geometries.TryGetValue(s, out var geometry))
            return geometry;
        var r = s.Rect;
        geometry = s.Shape switch
        {
            UmlShape.Diamond => Polygon(new Point(r.Center.X, r.Y), new Point(r.Right, r.Center.Y), new Point(r.Center.X, r.Bottom), new Point(r.X, r.Center.Y)),
            UmlShape.Hexagon => Hexagon(r, Math.Min(r.Height / 2, 14)),
            UmlShape.Band => Hexagon(r, Math.Min(r.Width / 2, 5)),
            UmlShape.Note => Polygon(r.TopLeft, new Point(r.Right - 10, r.Y), new Point(r.Right, r.Y + 10), r.BottomRight, r.BottomLeft),
            UmlShape.Folder => Folder(r, s.Extra, s.Extra2, s.Radius),
            UmlShape.Frame => new RectangleGeometry(r, s.Radius, s.Radius),
            UmlShape.Node3D => new RectangleGeometry(new Rect(r.X, r.Y + s.Extra, r.Width - s.Extra, r.Height - s.Extra)),
            UmlShape.Cylinder => Cylinder(r, s.Extra),
            UmlShape.Cloud => Cloud(r),
            UmlShape.Collections => new RectangleGeometry(new Rect(r.X + 4, r.Y, r.Width - 4, r.Height - 4), s.Radius, s.Radius),
            UmlShape.Subroutine => new RectangleGeometry(r, s.Radius, s.Radius),
            UmlShape.Parallelogram => Polygon(new Point(r.X + Lean(r), r.Y), r.TopRight, new Point(r.Right - Lean(r), r.Bottom), r.BottomLeft),
            UmlShape.ParallelogramAlt => Polygon(r.TopLeft, new Point(r.Right - Lean(r), r.Y), r.BottomRight, new Point(r.X + Lean(r), r.Bottom)),
            UmlShape.Trapezoid => Polygon(new Point(r.X + Lean(r), r.Y), new Point(r.Right - Lean(r), r.Y), r.BottomRight, r.BottomLeft),
            UmlShape.TrapezoidAlt => Polygon(r.TopLeft, r.TopRight, new Point(r.Right - Lean(r), r.Bottom), new Point(r.X + Lean(r), r.Bottom)),
            UmlShape.Asymmetric => Polygon(r.TopLeft, r.TopRight, r.BottomRight, r.BottomLeft, new Point(r.X + Lean(r), r.Center.Y)),
            _ => new RectangleGeometry(r),
        };
        _geometries[s] = geometry;
        return geometry;
    }

    private static double Lean(Rect r) => Math.Min(r.Height * 0.45, r.Width / 4);

    private void DrawWedge(DrawingContext context, WedgePrim w)
    {
        var fill = Brush(w.Fill);
        var pen = Pen(w.Stroke, 2);
        if (w.SweepAngle >= 359.99)
        {
            context.DrawEllipse(fill, pen, w.Center, w.Radius, w.Radius);
            return;
        }
        if (!_geometries.TryGetValue(w, out var geometry))
        {
            Point At(double degrees)
            {
                var radians = (degrees - 90) * Math.PI / 180;
                return new Point(w.Center.X + w.Radius * Math.Cos(radians), w.Center.Y + w.Radius * Math.Sin(radians));
            }
            var stream = new StreamGeometry();
            using (var ctx = stream.Open())
            {
                ctx.BeginFigure(w.Center, true);
                ctx.LineTo(At(w.StartAngle));
                ctx.ArcTo(At(w.StartAngle + w.SweepAngle), new Size(w.Radius, w.Radius), 0, w.SweepAngle > 180, SweepDirection.Clockwise);
                ctx.EndFigure(true);
            }
            geometry = stream;
            _geometries[w] = geometry;
        }
        context.DrawGeometry(fill, pen, geometry);
    }

    private static Geometry Polygon(params Point[] points)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(points[0], true);
        foreach (var p in points.Skip(1))
            ctx.LineTo(p);
        ctx.EndFigure(true);
        return geometry;
    }

    private static Geometry Hexagon(Rect r, double inset) => Polygon(
        new Point(r.X, r.Center.Y), new Point(r.X + inset, r.Y), new Point(r.Right - inset, r.Y),
        new Point(r.Right, r.Center.Y), new Point(r.Right - inset, r.Bottom), new Point(r.X + inset, r.Bottom));

    private static Geometry Folder(Rect r, double tabWidth, double tabHeight, double radius)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        var top = r.Y + tabHeight;
        var corner = new Size(radius, radius);
        ctx.BeginFigure(new Point(r.X, r.Bottom - radius), true);
        ctx.LineTo(new Point(r.X, r.Y + 3));
        ctx.ArcTo(new Point(r.X + 3, r.Y), new Size(3, 3), 0, false, SweepDirection.Clockwise);
        ctx.LineTo(new Point(r.X + tabWidth - 3, r.Y));
        ctx.ArcTo(new Point(r.X + tabWidth, r.Y + 3), new Size(3, 3), 0, false, SweepDirection.Clockwise);
        ctx.LineTo(new Point(r.X + tabWidth, top));
        ctx.LineTo(new Point(r.Right - radius, top));
        ctx.ArcTo(new Point(r.Right, top + radius), corner, 0, false, SweepDirection.Clockwise);
        ctx.LineTo(new Point(r.Right, r.Bottom - radius));
        ctx.ArcTo(new Point(r.Right - radius, r.Bottom), corner, 0, false, SweepDirection.Clockwise);
        ctx.LineTo(new Point(r.X + radius, r.Bottom));
        ctx.ArcTo(new Point(r.X, r.Bottom - radius), corner, 0, false, SweepDirection.Clockwise);
        ctx.EndFigure(true);
        return geometry;
    }

    private static Geometry Cylinder(Rect r, double cap)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        var radius = new Size(r.Width / 2, cap);
        ctx.BeginFigure(new Point(r.X, r.Y + cap), true);
        ctx.LineTo(new Point(r.X, r.Bottom - cap));
        ctx.ArcTo(new Point(r.Right, r.Bottom - cap), radius, 0, false, SweepDirection.CounterClockwise);
        ctx.LineTo(new Point(r.Right, r.Y + cap));
        ctx.ArcTo(new Point(r.X, r.Y + cap), radius, 0, false, SweepDirection.CounterClockwise);
        ctx.EndFigure(true);
        return geometry;
    }

    /// <summary>Bumps along an inset rectangle, clockwise, so every arc bulges outwards.</summary>
    private static Geometry Cloud(Rect r)
    {
        var inner = r.Deflate(8);
        var points = new List<Point>();
        void Edge(Point a, Point b, int count)
        {
            for (var i = 0; i < count; i++)
                points.Add(a + (b - a) * ((double)i / count));
        }
        var horizontal = Math.Max(3, (int)(inner.Width / 36));
        var vertical = Math.Max(2, (int)(inner.Height / 36));
        Edge(inner.TopLeft, inner.TopRight, horizontal);
        Edge(inner.TopRight, inner.BottomRight, vertical);
        Edge(inner.BottomRight, inner.BottomLeft, horizontal);
        Edge(inner.BottomLeft, inner.TopLeft, vertical);

        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(points[0], true);
        for (var i = 0; i < points.Count; i++)
        {
            var next = points[(i + 1) % points.Count];
            var distance = UmlGeometry.Length(next - points[i]);
            ctx.ArcTo(next, new Size(distance * 0.6, distance * 0.6), 0, false, SweepDirection.Clockwise);
        }
        ctx.EndFigure(true);
        return geometry;
    }

    // ---- lines and heads ----

    private void DrawLine(DrawingContext context, LinePrim l)
    {
        var pen = Pen(l.Brush, l.Thickness, l.Style);
        if (pen is null || l.Points.Count < 2)
            return;
        if (l.Points.Count == 2)
        {
            context.DrawLine(pen, l.Points[0], l.Points[1]);
            return;
        }
        if (!_geometries.TryGetValue(l, out var geometry))
        {
            geometry = LineGeometry(l.Points, l.Curve);
            _geometries[l] = geometry;
        }
        context.DrawGeometry(null, pen, geometry);
    }

    public static Geometry LineGeometry(IReadOnlyList<Point> points, UmlCurve curve)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(points[0], false);
        if (curve == UmlCurve.Bezier && points.Count == 4)
            ctx.CubicBezierTo(points[1], points[2], points[3]);
        else if (curve == UmlCurve.Smooth)
            foreach (var (c1, c2, end) in UmlGeometry.SmoothSegments(points))
                ctx.CubicBezierTo(c1, c2, end);
        else
            foreach (var p in points.Skip(1))
                ctx.LineTo(p);
        ctx.EndFigure(false);
        return geometry;
    }

    private void DrawHead(DrawingContext context, HeadPrim h)
    {
        var d = h.Direction;
        var n = new Vector(-d.Y, d.X);
        var tip = h.Tip;
        var stroke = Pen(h.Brush, 1.25);
        var solid = Brush(h.Brush);
        var hollow = Brush(UmlBrush.Background);
        switch (h.Head)
        {
            case UmlArrowHead.Arrow:
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(tip - d * 10 + n * 5, false);
                    ctx.LineTo(tip);
                    ctx.LineTo(tip - d * 10 - n * 5);
                    ctx.EndFigure(false);
                }
                context.DrawGeometry(null, stroke, geometry);
                break;
            }
            case UmlArrowHead.FilledTriangle:
                context.DrawGeometry(solid, null, Polygon(tip, tip - d * 10 + n * 4.5, tip - d * 10 - n * 4.5));
                break;
            case UmlArrowHead.Triangle:
                context.DrawGeometry(hollow, stroke, Polygon(tip, tip - d * 12 + n * 7, tip - d * 12 - n * 7));
                break;
            case UmlArrowHead.Diamond or UmlArrowHead.FilledDiamond:
                context.DrawGeometry(h.Head == UmlArrowHead.Diamond ? hollow : solid, stroke,
                    Polygon(tip, tip - d * 8 + n * 5.5, tip - d * 16, tip - d * 8 - n * 5.5));
                break;
            case UmlArrowHead.Circle:
            {
                var c = tip - d * 5;
                context.DrawEllipse(hollow, stroke, c, 5, 5);
                break;
            }
            case UmlArrowHead.Socket:
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(tip + d * 2 + n * 8, false);
                    ctx.ArcTo(tip + d * 2 - n * 8, new Size(8, 8), 0, false,
                        d.X * n.Y - d.Y * n.X > 0 ? SweepDirection.CounterClockwise : SweepDirection.Clockwise);
                    ctx.EndFigure(false);
                }
                context.DrawGeometry(null, stroke, geometry);
                break;
            }
            case UmlArrowHead.Plus:
            {
                var c = tip - d * 6;
                context.DrawEllipse(hollow, stroke, c, 6, 6);
                if (stroke is not null)
                {
                    context.DrawLine(stroke, c - d * 6, c + d * 6);
                    context.DrawLine(stroke, c - n * 6, c + n * 6);
                }
                break;
            }
            case UmlArrowHead.Cross:
                if (stroke is not null)
                {
                    var c = tip - d * 10;
                    context.DrawLine(stroke, c + (d + n) * 4, c - (d + n) * 4);
                    context.DrawLine(stroke, c + (d - n) * 4, c - (d - n) * 4);
                }
                break;
            case UmlArrowHead.Square:
                context.DrawGeometry(hollow, stroke, Polygon(tip + n * 4, tip - d * 8 + n * 4, tip - d * 8 - n * 4, tip - n * 4));
                break;
            case UmlArrowHead.ZeroOrOne or UmlArrowHead.ExactlyOne or UmlArrowHead.ZeroOrMore or UmlArrowHead.OneOrMore when stroke is not null:
            {
                // Crow's foot notation: the many side spreads to the entity, the bar or circle is further out.
                void Bar(double at) => context.DrawLine(stroke, tip - d * at + n * 6, tip - d * at - n * 6);
                var many = h.Head is UmlArrowHead.ZeroOrMore or UmlArrowHead.OneOrMore;
                if (many)
                {
                    context.DrawLine(stroke, tip - d * 11, tip + n * 7);
                    context.DrawLine(stroke, tip - d * 11, tip - n * 7);
                }
                var near = many ? 15 : 6;
                if (h.Head is UmlArrowHead.ExactlyOne)
                {
                    Bar(6);
                    Bar(11);
                }
                else if (h.Head is UmlArrowHead.OneOrMore)
                    Bar(near);
                else
                {
                    if (!many)
                        Bar(near);
                    context.DrawEllipse(hollow, stroke, tip - d * (many ? 19 : 15), 4, 4);
                }
                break;
            }
        }
    }

    // ---- glyphs ----

    private void DrawGlyph(DrawingContext context, GlyphPrim g)
    {
        var r = g.Rect;
        var c = r.Center;
        var stroke = Pen(g.Brush, 1.5);
        var fill = Brush(g.Fill);
        switch (g.Glyph)
        {
            case UmlGlyph.VisibilityPublic:
                context.DrawEllipse(fill, Pen(g.Brush, 1.25), c, r.Width / 2 - 0.5, r.Height / 2 - 0.5);
                break;
            case UmlGlyph.VisibilityPrivate:
                context.DrawRectangle(fill, Pen(g.Brush, 1.25), r.Deflate(0.5));
                break;
            case UmlGlyph.VisibilityProtected:
                context.DrawGeometry(fill, Pen(g.Brush, 1.25), Polygon(new Point(c.X, r.Y), new Point(r.Right, c.Y), new Point(c.X, r.Bottom), new Point(r.X, c.Y)));
                break;
            case UmlGlyph.VisibilityPackage:
                context.DrawGeometry(fill, Pen(g.Brush, 1.25), Polygon(new Point(c.X, r.Y), r.BottomRight, r.BottomLeft));
                break;
            case UmlGlyph.Badge:
            {
                var outlined = g.Brush != UmlBrush.None;
                context.DrawEllipse(fill, outlined ? Pen(UmlBrush.Border, 1.25) : null, c, r.Width / 2, r.Height / 2);
                var letter = g.Letter ?? "";
                var size = r.Height * (letter.Length > 1 ? 0.48 : 0.6);
                var formatted = text.Format(letter, size, true, false, outlined ? Brush(g.Brush) : Brushes.White);
                context.DrawText(formatted, new Point(c.X - formatted.Width / 2, c.Y - formatted.Height / 2));
                break;
            }
            case UmlGlyph.ComponentIcon:
            {
                var pen = Pen(g.Brush, 1.1);
                context.DrawRectangle(fill, pen, new Rect(r.X + 3, r.Y + 1, r.Width - 3, r.Height - 2), 1.5, 1.5);
                context.DrawRectangle(fill, pen, new Rect(r.X, r.Y + 3.5, 6, 3));
                context.DrawRectangle(fill, pen, new Rect(r.X, r.Y + 8, 6, 3));
                break;
            }
            case UmlGlyph.ArtifactIcon:
            {
                var pen = Pen(g.Brush, 1.1);
                context.DrawGeometry(fill, pen, Polygon(new Point(r.X + 2, r.Y), new Point(r.Right - 5, r.Y), new Point(r.Right - 1, r.Y + 4), new Point(r.Right - 1, r.Bottom), new Point(r.X + 2, r.Bottom)));
                if (pen is not null)
                    context.DrawLine(pen, new Point(r.Right - 5, r.Y), new Point(r.Right - 5, r.Y + 4));
                break;
            }
            case UmlGlyph.StickFigure:
            {
                var pen = Pen(g.Brush, 1.6);
                if (pen is null)
                    break;
                context.DrawEllipse(fill, pen, new Point(c.X, r.Y + 6.5), 6, 6);
                context.DrawLine(pen, new Point(c.X, r.Y + 13), new Point(c.X, r.Y + 29));
                context.DrawLine(pen, new Point(r.X + 2, r.Y + 19), new Point(r.Right - 2, r.Y + 19));
                context.DrawLine(pen, new Point(c.X, r.Y + 29), new Point(r.X + 4, r.Bottom - 1));
                context.DrawLine(pen, new Point(c.X, r.Y + 29), new Point(r.Right - 4, r.Bottom - 1));
                break;
            }
            case UmlGlyph.Boundary:
            {
                var center = new Point(r.X + r.Width * 0.6, c.Y);
                if (stroke is not null)
                {
                    context.DrawLine(stroke, new Point(r.X + 3, r.Y + 4), new Point(r.X + 3, r.Bottom - 4));
                    context.DrawLine(stroke, new Point(r.X + 3, c.Y), new Point(center.X - 12, c.Y));
                }
                context.DrawEllipse(fill, stroke, center, 12, 12);
                break;
            }
            case UmlGlyph.Control:
            {
                context.DrawEllipse(fill, stroke, c, 12, 12);
                if (stroke is not null)
                {
                    var top = new Point(c.X, c.Y - 12);
                    context.DrawLine(stroke, top, top + new Vector(5, -4));
                    context.DrawLine(stroke, top, top + new Vector(5, 4));
                }
                break;
            }
            case UmlGlyph.Entity:
                context.DrawEllipse(fill, stroke, c, 12, 12);
                if (stroke is not null)
                    context.DrawLine(stroke, new Point(c.X - 12, c.Y + 13.5), new Point(c.X + 12, c.Y + 13.5));
                break;
            case UmlGlyph.Cross:
            {
                var pen = Pen(g.Brush, 2.5);
                if (pen is null)
                    break;
                context.DrawLine(pen, r.TopLeft, r.BottomRight);
                context.DrawLine(pen, r.TopRight, r.BottomLeft);
                break;
            }
            case UmlGlyph.CircleCross:
            {
                context.DrawEllipse(fill, stroke, c, r.Width / 2, r.Height / 2);
                if (stroke is not null)
                {
                    var k = r.Width / 2 * 0.7071;
                    context.DrawLine(stroke, c + new Vector(-k, -k), c + new Vector(k, k));
                    context.DrawLine(stroke, c + new Vector(k, -k), c + new Vector(-k, k));
                }
                break;
            }
            case UmlGlyph.Bullseye:
                context.DrawEllipse(fill, stroke, c, r.Width / 2 - 0.75, r.Height / 2 - 0.75);
                context.DrawEllipse(Brush(g.Brush), null, c, r.Width / 2 - 5, r.Height / 2 - 5);
                break;
            case UmlGlyph.Dot:
                context.DrawEllipse(fill, null, c, r.Width / 2, r.Height / 2);
                break;
            case UmlGlyph.Circle:
                context.DrawEllipse(fill, stroke, c, r.Width / 2 - 0.75, r.Height / 2 - 0.75);
                break;
            case UmlGlyph.DatabaseIcon:
                context.DrawGeometry(fill, stroke, Cylinder(r, 4));
                break;
        }
    }

    // ---- text ----

    private void DrawText(DrawingContext context, TextPrim t)
    {
        if (string.IsNullOrEmpty(t.Text))
            return;
        if (!_texts.TryGetValue(t, out var formatted))
        {
            formatted = text.Format(t.Text, t.FontSize, t.Bold, t.Italic, Brush(t.Brush));
            if (t.Center)
            {
                formatted.MaxTextWidth = Math.Max(1, t.Size.Width + 1);
                formatted.TextAlignment = TextAlignment.Center;
            }
            if (t.Underline)
                formatted.SetTextDecorations(TextDecorations.Underline);
            _texts[t] = formatted;
        }
        context.DrawText(formatted, t.Origin);
    }
}
