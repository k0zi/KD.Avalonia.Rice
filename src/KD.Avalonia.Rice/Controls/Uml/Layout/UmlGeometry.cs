using Avalonia;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>Outline used where lines meet an element.</summary>
internal enum UmlAnchor
{
    Rectangle,
    Ellipse,
    Diamond,
}

internal static class UmlGeometry
{
    /// <summary>Where the line from the center of <paramref name="rect"/> toward <paramref name="toward"/> leaves the outline.</summary>
    public static Point Clip(Rect rect, UmlAnchor anchor, Point toward)
    {
        var c = rect.Center;
        var d = toward - c;
        if (Math.Abs(d.X) < 1e-6 && Math.Abs(d.Y) < 1e-6)
            return c;
        var hw = rect.Width / 2;
        var hh = rect.Height / 2;
        if (hw <= 0 || hh <= 0)
            return c;

        double t;
        switch (anchor)
        {
            case UmlAnchor.Ellipse:
                t = 1 / Math.Sqrt(d.X * d.X / (hw * hw) + d.Y * d.Y / (hh * hh));
                break;
            case UmlAnchor.Diamond:
                t = 1 / (Math.Abs(d.X) / hw + Math.Abs(d.Y) / hh);
                break;
            default:
                var tx = Math.Abs(d.X) < 1e-6 ? double.MaxValue : hw / Math.Abs(d.X);
                var ty = Math.Abs(d.Y) < 1e-6 ? double.MaxValue : hh / Math.Abs(d.Y);
                t = Math.Min(tx, ty);
                break;
        }
        // The point is inside the outline: no clipping possible.
        if (t >= 1)
            return toward;
        return c + d * t;
    }

    public static double Length(Vector v) => Math.Sqrt(v.X * v.X + v.Y * v.Y);

    public static Vector Normalize(Vector v)
    {
        var length = Length(v);
        return length < 1e-9 ? new Vector(1, 0) : v / length;
    }

    public static double Length(IReadOnlyList<Point> points)
    {
        double total = 0;
        for (var i = 1; i < points.Count; i++)
            total += Length(points[i] - points[i - 1]);
        return total;
    }

    /// <summary>Point at <paramref name="fraction"/> (0–1) of the polyline's length, with the direction of that segment.</summary>
    public static (Point Point, Vector Direction) PointAt(IReadOnlyList<Point> points, double fraction)
    {
        if (points.Count == 0)
            return (default, new Vector(1, 0));
        if (points.Count == 1)
            return (points[0], new Vector(1, 0));
        var target = Length(points) * Math.Clamp(fraction, 0, 1);
        for (var i = 1; i < points.Count; i++)
        {
            var segment = points[i] - points[i - 1];
            var length = Length(segment);
            if (target <= length || i == points.Count - 1)
            {
                var t = length < 1e-9 ? 0 : Math.Min(1, target / length);
                return (points[i - 1] + segment * t, Normalize(segment));
            }
            target -= length;
        }
        return (points[^1], Normalize(points[^1] - points[^2]));
    }

    /// <summary>Moves the last point back along the line by <paramref name="distance"/> (room for an arrow head).</summary>
    public static void ShortenEnd(List<Point> points, double distance)
    {
        if (points.Count < 2 || distance <= 0)
            return;
        var direction = points[^1] - points[^2];
        var length = Length(direction);
        if (length <= distance)
            return;
        points[^1] -= direction / length * distance;
    }

    public static void ShortenStart(List<Point> points, double distance)
    {
        points.Reverse();
        ShortenEnd(points, distance);
        points.Reverse();
    }

    /// <summary>Shifts a polyline sideways (parallel edges between the same two elements).</summary>
    public static List<Point> Offset(IReadOnlyList<Point> points, double distance)
    {
        if (points.Count < 2 || distance == 0)
            return [.. points];
        var direction = Normalize(points[^1] - points[0]);
        var normal = new Vector(-direction.Y, direction.X) * distance;
        return points.Select(p => p + normal).ToList();
    }

    public static double DistanceToSegment(Point p, Point a, Point b)
    {
        var ab = b - a;
        var lengthSquared = ab.X * ab.X + ab.Y * ab.Y;
        var t = lengthSquared < 1e-9 ? 0 : Math.Clamp(((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / lengthSquared, 0, 1);
        return Length(p - (a + ab * t));
    }

    public static double DistanceToPolyline(Point p, IReadOnlyList<Point> points)
    {
        var best = double.MaxValue;
        for (var i = 1; i < points.Count; i++)
            best = Math.Min(best, DistanceToSegment(p, points[i - 1], points[i]));
        return best;
    }

    /// <summary>
    /// Cubic Bézier segments (control 1, control 2, end) of a smooth curve through the points (Catmull–Rom).
    /// </summary>
    public static List<(Point C1, Point C2, Point End)> SmoothSegments(IReadOnlyList<Point> points, double tension = 0.5)
    {
        var segments = new List<(Point, Point, Point)>();
        for (var i = 0; i + 1 < points.Count; i++)
        {
            var p0 = i == 0 ? points[i] : points[i - 1];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = i + 2 < points.Count ? points[i + 2] : p2;
            var c1 = p1 + (p2 - p0) * (tension / 3);
            var c2 = p2 - (p3 - p1) * (tension / 3);
            segments.Add((c1, c2, p2));
        }
        return segments;
    }

    /// <summary>Points along the curve, for hit testing and bounds.</summary>
    public static List<Point> Flatten(IReadOnlyList<Point> points, UmlCurve curve)
    {
        if (curve == UmlCurve.Polyline || points.Count < 3)
            return [.. points];
        var result = new List<Point> { points[0] };
        List<(Point C1, Point C2, Point End)> segments = curve == UmlCurve.Bezier && points.Count == 4
            ? [(points[1], points[2], points[3])]
            : SmoothSegments(points);
        var start = points[0];
        foreach (var (c1, c2, end) in segments)
        {
            for (var k = 1; k <= 8; k++)
            {
                var t = k / 8.0;
                var u = 1 - t;
                result.Add(new Point(
                    u * u * u * start.X + 3 * u * u * t * c1.X + 3 * u * t * t * c2.X + t * t * t * end.X,
                    u * u * u * start.Y + 3 * u * u * t * c1.Y + 3 * u * t * t * c2.Y + t * t * t * end.Y));
            }
            start = end;
        }
        return result;
    }

    public static Rect BoundsOf(IEnumerable<Point> points)
    {
        var list = points.ToList();
        if (list.Count == 0)
            return default;
        var left = list.Min(p => p.X);
        var top = list.Min(p => p.Y);
        return new Rect(left, top, list.Max(p => p.X) - left, list.Max(p => p.Y) - top);
    }
}
