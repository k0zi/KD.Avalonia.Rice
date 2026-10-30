using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls;

internal enum DockGuideKind
{
    /// <summary>An edge of the whole layout.</summary>
    Edge,

    /// <summary>A side of the group under the pointer, or its tabs (the middle).</summary>
    Group,

    /// <summary>A side of the document area as a whole (the outer arrows around a document group's compass).</summary>
    DocumentArea,
}

internal readonly record struct DockGuide(DockGuideKind Kind, DockPosition Position, Rect Bounds);

/// <summary>
/// Where the dock guides go while a pane is dragged, like Visual Studio's: a button at the middle of each edge of the
/// layout, a compass over the group under the pointer (four sides and the middle for tabs), and around the compass of a
/// document group four more for the sides of the whole document area. Also the rectangle a drop would fill.
/// </summary>
internal static class DockGuides
{
    public const double ButtonSize = 32;
    public const double Gap = 4;
    private const double EdgeInset = 10;

    public static List<DockGuide> Place(Rect layout, Rect? group, bool groupIsDocument, Rect? documentArea)
    {
        var guides = new List<DockGuide>
        {
            new(DockGuideKind.Edge, DockPosition.Left, Button(new Point(layout.X + EdgeInset + ButtonSize / 2, layout.Center.Y))),
            new(DockGuideKind.Edge, DockPosition.Top, Button(new Point(layout.Center.X, layout.Y + EdgeInset + ButtonSize / 2))),
            new(DockGuideKind.Edge, DockPosition.Right, Button(new Point(layout.Right - EdgeInset - ButtonSize / 2, layout.Center.Y))),
            new(DockGuideKind.Edge, DockPosition.Bottom, Button(new Point(layout.Center.X, layout.Bottom - EdgeInset - ButtonSize / 2))),
        };
        if (group is not { } g)
            return guides;
        var c = g.Center;
        const double step = ButtonSize + Gap;
        guides.Add(new DockGuide(DockGuideKind.Group, DockPosition.Tab, Button(c)));
        guides.Add(new DockGuide(DockGuideKind.Group, DockPosition.Left, Button(c + new Vector(-step, 0))));
        guides.Add(new DockGuide(DockGuideKind.Group, DockPosition.Top, Button(c + new Vector(0, -step))));
        guides.Add(new DockGuide(DockGuideKind.Group, DockPosition.Right, Button(c + new Vector(step, 0))));
        guides.Add(new DockGuide(DockGuideKind.Group, DockPosition.Bottom, Button(c + new Vector(0, step))));
        if (groupIsDocument && documentArea is not null)
        {
            guides.Add(new DockGuide(DockGuideKind.DocumentArea, DockPosition.Left, Button(c + new Vector(-2 * step, 0))));
            guides.Add(new DockGuide(DockGuideKind.DocumentArea, DockPosition.Top, Button(c + new Vector(0, -2 * step))));
            guides.Add(new DockGuide(DockGuideKind.DocumentArea, DockPosition.Right, Button(c + new Vector(2 * step, 0))));
            guides.Add(new DockGuide(DockGuideKind.DocumentArea, DockPosition.Bottom, Button(c + new Vector(0, 2 * step))));
        }
        return guides;
    }

    private static Rect Button(Point center) =>
        new(center.X - ButtonSize / 2, center.Y - ButtonSize / 2, ButtonSize, ButtonSize);

    public static DockGuide? HitTest(IEnumerable<DockGuide> guides, Point point)
    {
        foreach (var guide in guides)
        {
            if (guide.Bounds.Contains(point))
                return guide;
        }
        return null;
    }

    /// <summary>The part of the layout a drop on <paramref name="guide"/> would fill.</summary>
    public static Rect Preview(DockGuide guide, Rect layout, Rect? group, Rect? documentArea, double size)
    {
        var area = guide.Kind switch
        {
            DockGuideKind.Group when group is { } g => g,
            DockGuideKind.DocumentArea when documentArea is { } d => d,
            _ => layout,
        };
        if (guide.Position == DockPosition.Tab)
            return area;
        // Into a group: half of it; at an edge: the pane's size, but at most a third.
        var horizontal = guide.Position is DockPosition.Left or DockPosition.Right;
        var length = horizontal ? area.Width : area.Height;
        var extent = guide.Kind == DockGuideKind.Group ? length / 2 : Math.Min(size, length / 3);
        return guide.Position switch
        {
            DockPosition.Left => new Rect(area.X, area.Y, extent, area.Height),
            DockPosition.Right => new Rect(area.Right - extent, area.Y, extent, area.Height),
            DockPosition.Top => new Rect(area.X, area.Y, area.Width, extent),
            _ => new Rect(area.X, area.Bottom - extent, area.Width, extent),
        };
    }
}

/// <summary>Draws the dock guides and the drop preview over a <see cref="RiceDockLayout"/> while a pane is dragged.</summary>
internal sealed class DockGuideOverlay(RiceDockLayout layout) : Control
{
    private IReadOnlyList<DockGuide> _guides = [];
    private DockGuide? _hovered;
    private Rect? _preview;

    public void Show(IReadOnlyList<DockGuide> guides, DockGuide? hovered, Rect? preview)
    {
        _guides = guides;
        _hovered = hovered;
        _preview = preview;
        InvalidateVisual();
    }

    public void Hide() => Show([], null, null);

    public override void Render(DrawingContext context)
    {
        var accent = layout.Brush("RiceAccentBrush");
        if (_preview is { } preview && layout.Brush("RiceSelectedRowBrush") is { } fill)
            context.DrawRectangle(fill, new Pen(accent, 2), preview.Deflate(1), 4, 4);
        if (_guides.Count == 0)
            return;

        var surface = layout.Brush("RiceSurfaceBrush");
        var border = new Pen(layout.Brush("RiceBorderBrush"));
        var muted = layout.Brush("RiceForegroundMutedBrush");
        var shadow = layout.TryFindResource("RicePopupShadow", ActualThemeVariant, out var s) && s is BoxShadows b ? b : default;
        foreach (var guide in _guides)
        {
            var hovered = _hovered == guide;
            var rect = guide.Bounds;
            context.DrawRectangle(hovered ? layout.Brush("RiceSelectedRowBrush") ?? surface : surface,
                hovered ? new Pen(accent, 2) : border, new RoundedRect(rect, 6), shadow);
            DrawGlyph(context, guide, rect.Deflate(7), hovered ? accent : muted, accent);
        }
    }

    // A small window outline with the side the pane would take filled; the tab guide shows a tab on top.
    private static void DrawGlyph(DrawingContext context, DockGuide guide, Rect frame, IBrush? outline, IBrush? fill)
    {
        var pen = new Pen(outline, 1.5);
        if (guide.Kind == DockGuideKind.Edge || guide.Kind == DockGuideKind.DocumentArea)
        {
            // An arrow toward the edge.
            var c = frame.Center;
            var (tip, back) = guide.Position switch
            {
                DockPosition.Left => (new Point(frame.Left, c.Y), new Vector(1, 0)),
                DockPosition.Right => (new Point(frame.Right, c.Y), new Vector(-1, 0)),
                DockPosition.Top => (new Point(c.X, frame.Top), new Vector(0, 1)),
                _ => (new Point(c.X, frame.Bottom), new Vector(0, -1)),
            };
            var side = new Vector(back.Y, back.X) * 6;
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(tip, true);
                g.LineTo(tip + back * 7 + side);
                g.LineTo(tip + back * 7 - side);
                g.EndFigure(true);
            }
            context.DrawGeometry(fill, null, geometry);
            var bar = guide.Position is DockPosition.Left or DockPosition.Right
                ? new Rect(Math.Min(tip.X, tip.X + back.X * 16), c.Y - 1.5, 16, 3)
                : new Rect(c.X - 1.5, Math.Min(tip.Y, tip.Y + back.Y * 16), 3, 16);
            context.FillRectangle(fill!, bar);
            if (guide.Kind == DockGuideKind.DocumentArea)
                context.DrawRectangle(null, pen, frame, 2, 2);
            return;
        }

        context.DrawRectangle(null, pen, frame, 2, 2);
        var part = guide.Position switch
        {
            DockPosition.Left => new Rect(frame.X, frame.Y, frame.Width / 2, frame.Height),
            DockPosition.Right => new Rect(frame.Center.X, frame.Y, frame.Width / 2, frame.Height),
            DockPosition.Top => new Rect(frame.X, frame.Y, frame.Width, frame.Height / 2),
            DockPosition.Bottom => new Rect(frame.X, frame.Center.Y, frame.Width, frame.Height / 2),
            _ => frame.Deflate(new Thickness(0, 4, 0, 0)),
        };
        if (fill is not null)
            context.FillRectangle(fill, part, 1);
        if (guide.Position == DockPosition.Tab && fill is not null)
            context.FillRectangle(fill, new Rect(frame.X, frame.Y, frame.Width / 2, 4), 1);
    }
}
