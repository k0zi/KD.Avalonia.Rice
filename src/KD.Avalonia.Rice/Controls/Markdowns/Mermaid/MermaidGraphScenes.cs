using Avalonia;
using KD.Avalonia.Rice.Controls.Uml;

namespace KD.Avalonia.Rice.Controls.Markdowns;

/// <summary>Builds the scene of a <see cref="MermaidDiagram"/> under its title.</summary>
internal static class MermaidSceneBuilder
{
    public static UmlScene Build(MermaidDiagram diagram, UmlStyle style) =>
        UmlSceneBuilder.WithTitle(diagram.Title, style, (scene, origin) =>
        {
            switch (diagram)
            {
                case MermaidFlowchart flowchart:
                    new FlowchartSceneBuilder(flowchart, style).Build(scene, origin);
                    break;
                case MermaidErDiagram er:
                    new ErSceneBuilder(er, style).Build(scene, origin);
                    break;
                case MermaidPie pie:
                    new PieSceneBuilder(pie, style).Build(scene, origin);
                    break;
                case MermaidGantt gantt:
                    new GanttSceneBuilder(gantt, style).Build(scene, origin);
                    break;
                case MermaidMindmap mindmap:
                    new MindmapSceneBuilder(mindmap, style).Build(scene, origin);
                    break;
            }
        });
}

/// <summary>
/// Layered layout of boxes that may contain boxes (subgraphs): the content of a container is laid out first, then
/// the container takes part in the layout of its level as one box. Links into nested boxes are lifted to the
/// containers for the layout and drawn straight.
/// </summary>
internal sealed class BoxGraph
{
    public sealed class Box(object model, Size size)
    {
        public object Model { get; } = model;
        public Size Size { get; set; } = size;
        public List<Box> Children { get; } = [];
        public Box? Parent { get; set; }
        /// <summary>Room around the content of a container, e.g. for its title.</summary>
        public Thickness Padding { get; set; }
        public Size MinSize { get; set; }
        /// <summary>Direction of a container's content; null keeps the outer direction.</summary>
        public bool? LeftToRight { get; set; }
        /// <summary>Result: position in the scene.</summary>
        public Rect Bounds { get; set; }
        public bool IsContainer => Children.Count > 0;
    }

    public sealed class Link(Box from, Box to, object model)
    {
        public Box From { get; } = from;
        public Box To { get; } = to;
        public object Model { get; } = model;
        public Size LabelSize { get; set; }
        public int MinLength { get; set; } = 1;
        /// <summary>Result: the route from center to center.</summary>
        public List<Point> Points { get; set; } = [];
    }

    private sealed class Level
    {
        public Size Size { get; set; }
        public Dictionary<Box, Rect> Rects { get; } = [];
        public Dictionary<Box, Level> Children { get; } = [];
        public Dictionary<Link, List<Point>> Routes { get; } = [];
        public Vector ContentOffset { get; set; }
    }

    public bool LeftToRight { get; set; }

    /// <summary>Bottom to top or right to left: the result is mirrored.</summary>
    public bool Reverse { get; set; }

    public double NodeSpacing { get; set; } = 40;

    public double LayerSpacing { get; set; } = 56;

    private IReadOnlyList<Link> _links = [];

    public void Run(IReadOnlyList<Box> roots, IReadOnlyList<Link> links, Point origin)
    {
        _links = links;
        foreach (var root in roots)
            SetParents(root, null);
        var top = LayoutLevel(roots, LeftToRight);
        Place(top, origin);
        foreach (var link in links.Where(l => l.Points.Count < 2))
            link.Points = [link.From.Bounds.Center, link.To.Bounds.Center];

        if (Reverse)
        {
            var area = new Rect(origin, top.Size);
            Point Mirror(Point p) => LeftToRight ? new Point(area.X + area.Right - p.X, p.Y) : new Point(p.X, area.Y + area.Bottom - p.Y);
            foreach (var box in roots.SelectMany(All))
            {
                var r = box.Bounds;
                box.Bounds = LeftToRight ? new Rect(area.X + area.Right - r.Right, r.Y, r.Width, r.Height) : new Rect(r.X, area.Y + area.Bottom - r.Bottom, r.Width, r.Height);
            }
            foreach (var link in links)
                link.Points = link.Points.Select(Mirror).ToList();
        }
    }

    private static IEnumerable<Box> All(Box box) => box.Children.SelectMany(All).Prepend(box);

    private static void SetParents(Box box, Box? parent)
    {
        box.Parent = parent;
        foreach (var child in box.Children)
            SetParents(child, box);
    }

    private Level LayoutLevel(IReadOnlyList<Box> members, bool leftToRight)
    {
        var level = new Level();
        if (members.Count == 0)
            return level;
        var nodes = new Dictionary<Box, LayoutNode>();
        foreach (var box in members)
        {
            var size = box.Size;
            if (box.IsContainer)
            {
                var inner = LayoutLevel(box.Children, box.LeftToRight ?? leftToRight);
                level.Children[box] = inner;
                var p = box.Padding;
                var width = Math.Max(box.MinSize.Width, inner.Size.Width + p.Left + p.Right);
                var height = Math.Max(box.MinSize.Height, inner.Size.Height + p.Top + p.Bottom);
                inner.ContentOffset = new Vector(p.Left + (width - p.Left - p.Right - inner.Size.Width) / 2, p.Top);
                size = new Size(width, height);
                box.Size = size;
            }
            nodes[box] = new LayoutNode(size, box);
        }

        var set = members.ToHashSet();
        Box? Lift(Box box)
        {
            for (Box? current = box; current is not null; current = current.Parent)
                if (set.Contains(current))
                    return current;
            return null;
        }

        var edges = new List<LayoutEdge>();
        var direct = new Dictionary<LayoutEdge, Link>();
        foreach (var link in _links)
        {
            var from = Lift(link.From);
            var to = Lift(link.To);
            if (from is null || to is null || from == to)
                continue;
            var edge = new LayoutEdge(nodes[from], nodes[to], link) { MinLength = link.MinLength, LabelSize = link.LabelSize };
            edges.Add(edge);
            if (from == link.From && to == link.To)
                direct[edge] = link;
        }

        var layout = new LayeredLayout { LeftToRight = leftToRight, NodeSpacing = NodeSpacing, LayerSpacing = LayerSpacing };
        level.Size = layout.Run(nodes.Values.ToList(), edges);
        foreach (var (box, node) in nodes)
            level.Rects[box] = node.Bounds;
        foreach (var (edge, link) in direct)
            level.Routes[link] = [.. edge.Points];
        return level;
    }

    private static void Place(Level level, Point origin)
    {
        foreach (var (box, rect) in level.Rects)
        {
            var absolute = rect.Translate((Vector)origin);
            box.Bounds = absolute;
            if (level.Children.TryGetValue(box, out var inner))
                Place(inner, absolute.TopLeft + inner.ContentOffset);
        }
        foreach (var (link, points) in level.Routes)
            link.Points = points.Select(p => p + (Vector)origin).ToList();
    }
}

/// <summary>Lays out and paints a <see cref="MermaidFlowchart"/>.</summary>
internal sealed class FlowchartSceneBuilder(MermaidFlowchart chart, UmlStyle style)
{
    private readonly UmlEdgePainter _edges = new(style);

    public void Build(UmlScene scene, Point origin)
    {
        var boxes = new Dictionary<string, BoxGraph.Box>();
        foreach (var node in chart.Nodes)
            boxes[node.Id] = new BoxGraph.Box(node, Measure(node));

        var subgraphBoxes = new Dictionary<MermaidSubgraph, BoxGraph.Box>();
        var titleHeight = style.Measure("Ag", bold: true, small: true).Height;
        BoxGraph.Box Container(MermaidSubgraph subgraph)
        {
            var box = new BoxGraph.Box(subgraph, default)
            {
                Padding = new Thickness(16, titleHeight + 18, 16, 16),
                MinSize = new Size(style.Measure(subgraph.Title, bold: true, small: true).Width + 32, titleHeight + 40),
                LeftToRight = subgraph.Direction is null ? null : subgraph.Direction is MermaidDirection.LeftToRight or MermaidDirection.RightToLeft,
            };
            foreach (var child in subgraph.Subgraphs)
                box.Children.Add(Container(child));
            foreach (var id in subgraph.Nodes)
                if (boxes.TryGetValue(id, out var nodeBox))
                    box.Children.Add(nodeBox);
            boxes.TryAdd(subgraph.Id, box);
            subgraphBoxes[subgraph] = box;
            return box;
        }
        var roots = chart.Subgraphs.Select(Container).ToList();
        // Empty subgraphs still show as a box.
        foreach (var (subgraph, box) in subgraphBoxes.Where(p => p.Value.Children.Count == 0))
            box.Size = box.MinSize;
        var nested = subgraphBoxes.Values.SelectMany(b => b.Children).ToHashSet();
        roots.AddRange(chart.Nodes.Select(n => boxes[n.Id]).Where(b => !nested.Contains(b)));

        var links = new List<BoxGraph.Link>();
        foreach (var edge in chart.Edges)
        {
            if (!boxes.TryGetValue(edge.From, out var from) || !boxes.TryGetValue(edge.To, out var to) || from == to)
                continue;
            var label = edge.Text is { Length: > 0 } text ? style.Measure(text, small: true) : default;
            links.Add(new BoxGraph.Link(from, to, edge)
            {
                MinLength = edge.Length,
                LabelSize = label.Width > 0 ? new Size(label.Width + 14, label.Height + 6) : default,
            });
        }

        var graph = new BoxGraph
        {
            LeftToRight = chart.Direction is MermaidDirection.LeftToRight or MermaidDirection.RightToLeft,
            Reverse = chart.Direction is MermaidDirection.BottomToTop or MermaidDirection.RightToLeft,
            NodeSpacing = 36,
            LayerSpacing = 52,
        };
        graph.Run(roots, links, origin);

        // Containers outer first, then the edges, then the nodes over them.
        foreach (var (subgraph, box) in subgraphBoxes.OrderBy(p => Depth(p.Value)))
        {
            var item = scene.AddItem(new UmlHitItem(subgraph, box.Bounds) { IsContainer = true, HeaderHeight = titleHeight + 12, CornerRadius = 10 });
            scene.Add(new ShapePrim { Shape = UmlShape.RoundedRectangle, Rect = box.Bounds, Radius = 10, Fill = UmlBrush.Container, Stroke = UmlBrush.Border }, item);
            scene.Add(style.Text(subgraph.Title, new Point(box.Bounds.X + 12, box.Bounds.Y + 8), bold: true, small: true, brush: UmlBrush.MutedText), item);
        }
        foreach (var link in links)
            PaintEdge(scene, link);
        foreach (var node in chart.Nodes)
            PaintNode(scene, node, boxes[node.Id].Bounds);
        foreach (var edge in chart.Edges.Where(e => e.From == e.To && boxes.ContainsKey(e.From)))
            PaintSelfLoop(scene, edge, boxes[edge.From].Bounds);
    }

    private static int Depth(BoxGraph.Box box)
    {
        var depth = 0;
        for (var p = box.Parent; p is not null; p = p.Parent)
            depth++;
        return depth;
    }

    private Size Measure(MermaidNode node)
    {
        var text = style.Measure(node.Text);
        var w = Math.Max(text.Width, 8);
        var h = text.Height;
        var pad = style.Padding;
        return node.Shape switch
        {
            MermaidNodeShape.Circle or MermaidNodeShape.DoubleCircle => new Size(Math.Max(w, h) + 2 * pad + 8, Math.Max(w, h) + 2 * pad + 8),
            MermaidNodeShape.Diamond => new Size(Math.Max(w * 1.5 + 2 * pad, 64), Math.Max(h * 2 + 2 * pad, 48)),
            MermaidNodeShape.Hexagon => new Size(w + 2 * pad + 28, h + 2 * pad),
            MermaidNodeShape.Parallelogram or MermaidNodeShape.ParallelogramAlt or MermaidNodeShape.Trapezoid or MermaidNodeShape.TrapezoidAlt
                => new Size(w + 2 * pad + 2 * Math.Min(h + 2 * pad, 40) * 0.45, h + 2 * pad),
            MermaidNodeShape.Asymmetric => new Size(w + 2 * pad + 14, h + 2 * pad),
            MermaidNodeShape.Cylinder => new Size(w + 2 * pad, h + 2 * pad + 16),
            MermaidNodeShape.Subroutine => new Size(w + 2 * pad + 16, h + 2 * pad),
            MermaidNodeShape.Stadium => new Size(w + 2 * pad + h, h + 2 * pad),
            _ => new Size(w + 2 * pad + 8, h + 2 * pad),
        };
    }

    private static UmlAnchor Anchor(MermaidNodeShape shape) => shape switch
    {
        MermaidNodeShape.Circle or MermaidNodeShape.DoubleCircle => UmlAnchor.Ellipse,
        MermaidNodeShape.Diamond => UmlAnchor.Diamond,
        _ => UmlAnchor.Rectangle,
    };

    private void PaintNode(UmlScene scene, MermaidNode node, Rect r)
    {
        var item = scene.AddItem(new UmlHitItem(node, r) { CornerRadius = node.Shape is MermaidNodeShape.Stadium ? r.Height / 2 : 6 });
        var (shape, radius, extra) = node.Shape switch
        {
            MermaidNodeShape.Rounded => (UmlShape.RoundedRectangle, 10.0, 0.0),
            MermaidNodeShape.Stadium => (UmlShape.RoundedRectangle, r.Height / 2, 0.0),
            MermaidNodeShape.Subroutine => (UmlShape.Subroutine, 2.0, 0.0),
            MermaidNodeShape.Cylinder => (UmlShape.Cylinder, 0.0, 8.0),
            MermaidNodeShape.Circle => (UmlShape.Ellipse, 0.0, 0.0),
            MermaidNodeShape.DoubleCircle => (UmlShape.DoubleCircle, 0.0, 0.0),
            MermaidNodeShape.Diamond => (UmlShape.Diamond, 0.0, 0.0),
            MermaidNodeShape.Hexagon => (UmlShape.Hexagon, 0.0, 0.0),
            MermaidNodeShape.Parallelogram => (UmlShape.Parallelogram, 0.0, 0.0),
            MermaidNodeShape.ParallelogramAlt => (UmlShape.ParallelogramAlt, 0.0, 0.0),
            MermaidNodeShape.Trapezoid => (UmlShape.Trapezoid, 0.0, 0.0),
            MermaidNodeShape.TrapezoidAlt => (UmlShape.TrapezoidAlt, 0.0, 0.0),
            MermaidNodeShape.Asymmetric => (UmlShape.Asymmetric, 0.0, 0.0),
            _ => (UmlShape.RoundedRectangle, 3.0, 0.0),
        };
        scene.Add(new ShapePrim
        {
            Shape = shape,
            Rect = r,
            Radius = radius,
            Extra = extra,
            Fill = UmlBrush.Header,
            Stroke = UmlBrush.Accent,
            Thickness = 1.25,
            Shadow = true,
        }, item);
        var text = style.Measure(node.Text);
        var top = r.Center.Y - text.Height / 2 + (node.Shape == MermaidNodeShape.Cylinder ? 6 : 0);
        var centerX = r.Center.X + (node.Shape == MermaidNodeShape.Asymmetric ? 6 : 0);
        scene.Add(style.CenteredText(node.Text, centerX, top), item);
    }

    private static UmlArrowHead Head(MermaidEdgeEnd end) => end switch
    {
        MermaidEdgeEnd.Arrow => UmlArrowHead.FilledTriangle,
        MermaidEdgeEnd.Circle => UmlArrowHead.Circle,
        MermaidEdgeEnd.Cross => UmlArrowHead.Cross,
        _ => UmlArrowHead.None,
    };

    private void PaintEdge(UmlScene scene, BoxGraph.Link link)
    {
        var edge = (MermaidEdge)link.Model;
        if (edge.Line == MermaidLineStyle.Invisible || link.Points.Count < 2)
            return;
        var points = link.Points.ToList();
        points[0] = Clip(link.From, points[1]);
        points[^1] = Clip(link.To, points[^2]);
        var item = scene.AddItem(new UmlHitItem(edge, default)
        {
            Path = points,
            Curve = points.Count > 2 ? UmlCurve.Smooth : UmlCurve.Polyline,
            From = link.From.Model,
            To = link.To.Model,
        });
        scene.Items[item].Bounds = _edges.Paint(scene, new UmlEdgePainter.Edge
        {
            Points = points,
            Curve = points.Count > 2 ? UmlCurve.Smooth : UmlCurve.Polyline,
            Stroke = edge.Line == MermaidLineStyle.Dotted ? UmlStroke.Dashed : UmlStroke.Solid,
            Thickness = edge.Line == MermaidLineStyle.Thick ? 2.75 : 1.4,
            FromHead = Head(edge.FromEnd),
            ToHead = Head(edge.ToEnd),
            Label = edge.Text,
        }, item);
    }

    private static Point Clip(BoxGraph.Box box, Point toward) =>
        UmlGeometry.Clip(box.Bounds, box.Model is MermaidNode node ? Anchor(node.Shape) : UmlAnchor.Rectangle, toward);

    private void PaintSelfLoop(UmlScene scene, MermaidEdge edge, Rect rect)
    {
        var start = new Point(rect.Right - Math.Min(20, rect.Width / 3), rect.Y);
        var end = new Point(rect.Right, rect.Y + Math.Min(16, rect.Height / 3));
        var points = new List<Point> { start, start + new Vector(4, -36), end + new Vector(36, -4), end };
        var item = scene.AddItem(new UmlHitItem(edge, default) { Path = points, Curve = UmlCurve.Bezier });
        var bounds = _edges.Paint(scene, new UmlEdgePainter.Edge { Points = points, Curve = UmlCurve.Bezier, ToHead = Head(edge.ToEnd), Thickness = 1.4 }, item);
        if (edge.Text is { Length: > 0 } label)
            bounds = bounds.Union(_edges.PaintLabel(scene, label, new Point(rect.Right + 28, rect.Y - 24), item));
        scene.Items[item].Bounds = bounds;
    }
}

/// <summary>Lays out and paints a <see cref="MermaidErDiagram"/>: entities as tables, relationships in crow's foot notation.</summary>
internal sealed class ErSceneBuilder(MermaidErDiagram diagram, UmlStyle style)
{
    private readonly UmlEdgePainter _edges = new(style);

    private double RowHeight => style.Measure("Ag", small: true).Height + 8;

    private double HeaderHeight => style.Measure("Ag", bold: true).Height + 14;

    private (double Type, double Name, double Keys, double Comment) Columns(MermaidEntity entity) =>
    (
        entity.Attributes.Select(a => style.Measure(a.Type, small: true).Width).DefaultIfEmpty().Max(),
        entity.Attributes.Select(a => style.Measure(a.Name, small: true).Width).DefaultIfEmpty().Max(),
        entity.Attributes.Select(a => style.Measure(string.Join(", ", a.Keys), bold: true, small: true).Width).DefaultIfEmpty().Max(),
        entity.Attributes.Select(a => a.Comment is null ? 0 : style.Measure(a.Comment, italic: true, small: true).Width).DefaultIfEmpty().Max()
    );

    private Size Measure(MermaidEntity entity)
    {
        var title = style.Measure(entity.Label, bold: true).Width + 32;
        if (entity.Attributes.Count == 0)
            return new Size(Math.Max(title, 100), HeaderHeight);
        var c = Columns(entity);
        var width = 12 + c.Type + 14 + c.Name + (c.Keys > 0 ? 14 + c.Keys : 0) + (c.Comment > 0 ? 14 + c.Comment : 0) + 12;
        return new Size(Math.Max(title, width), HeaderHeight + entity.Attributes.Count * RowHeight + 6);
    }

    public void Build(UmlScene scene, Point origin)
    {
        var boxes = diagram.Entities.ToDictionary(e => e.Id, e => new BoxGraph.Box(e, Measure(e)));
        var links = new List<BoxGraph.Link>();
        foreach (var relationship in diagram.Relationships)
        {
            if (!boxes.TryGetValue(relationship.From, out var from) || !boxes.TryGetValue(relationship.To, out var to) || from == to)
                continue;
            var label = relationship.Label is { Length: > 0 } text ? style.Measure(text, small: true) : default;
            links.Add(new BoxGraph.Link(from, to, relationship)
            {
                LabelSize = label.Width > 0 ? new Size(label.Width + 14, label.Height + 6) : default,
            });
        }
        var graph = new BoxGraph
        {
            LeftToRight = diagram.Direction is MermaidDirection.LeftToRight or MermaidDirection.RightToLeft,
            Reverse = diagram.Direction is MermaidDirection.BottomToTop or MermaidDirection.RightToLeft,
            NodeSpacing = 56,
            LayerSpacing = 72,
        };
        graph.Run(boxes.Values.ToList(), links, origin);

        foreach (var link in links)
        {
            var relationship = (MermaidRelationship)link.Model;
            var points = link.Points.ToList();
            points[0] = UmlGeometry.Clip(link.From.Bounds, UmlAnchor.Rectangle, points[1]);
            points[^1] = UmlGeometry.Clip(link.To.Bounds, UmlAnchor.Rectangle, points[^2]);
            var curve = points.Count > 2 ? UmlCurve.Smooth : UmlCurve.Polyline;
            var item = scene.AddItem(new UmlHitItem(relationship, default) { Path = points, Curve = curve, From = link.From.Model, To = link.To.Model });
            scene.Items[item].Bounds = _edges.Paint(scene, new UmlEdgePainter.Edge
            {
                Points = points,
                Curve = curve,
                Stroke = relationship.IsIdentifying ? UmlStroke.Solid : UmlStroke.Dashed,
                FromHead = Head(relationship.FromCardinality),
                ToHead = Head(relationship.ToCardinality),
                Label = relationship.Label,
            }, item);
        }
        foreach (var entity in diagram.Entities)
            PaintEntity(scene, entity, boxes[entity.Id].Bounds);
    }

    private static UmlArrowHead Head(MermaidCardinality cardinality) => cardinality switch
    {
        MermaidCardinality.ZeroOrOne => UmlArrowHead.ZeroOrOne,
        MermaidCardinality.ZeroOrMore => UmlArrowHead.ZeroOrMore,
        MermaidCardinality.OneOrMore => UmlArrowHead.OneOrMore,
        _ => UmlArrowHead.ExactlyOne,
    };

    private void PaintEntity(UmlScene scene, MermaidEntity entity, Rect r)
    {
        var item = scene.AddItem(new UmlHitItem(entity, r));
        scene.Add(new ShapePrim { Shape = UmlShape.RoundedRectangle, Rect = r, Radius = 6, Shadow = true }, item);
        var header = new Rect(r.X, r.Y, r.Width, HeaderHeight);
        scene.Add(new ShapePrim
        {
            Shape = entity.Attributes.Count == 0 ? UmlShape.RoundedRectangle : UmlShape.TopRounded,
            Rect = header,
            Radius = 6,
            Fill = UmlBrush.Header,
            Stroke = UmlBrush.None,
        }, item);
        scene.Add(new ShapePrim { Shape = UmlShape.RoundedRectangle, Rect = r, Radius = 6, Fill = UmlBrush.None }, item);
        var title = style.Measure(entity.Label, bold: true);
        scene.Add(style.CenteredText(entity.Label, r.Center.X, r.Y + (HeaderHeight - title.Height) / 2, bold: true), item);
        if (entity.Attributes.Count == 0)
            return;
        scene.Add(new LinePrim { Points = [new Point(r.X, header.Bottom), new Point(r.Right, header.Bottom)], Brush = UmlBrush.Border, Thickness = 1 }, item);

        var c = Columns(entity);
        var y = header.Bottom + 3;
        foreach (var attribute in entity.Attributes)
        {
            var textTop = y + 4;
            var x = r.X + 12;
            scene.Add(style.Text(attribute.Type, new Point(x, textTop), small: true, brush: UmlBrush.MutedText), item);
            x += c.Type + 14;
            scene.Add(style.Text(attribute.Name, new Point(x, textTop), small: true), item);
            x += c.Name + 14;
            if (c.Keys > 0)
            {
                if (attribute.Keys.Count > 0)
                    scene.Add(style.Text(string.Join(", ", attribute.Keys), new Point(x, textTop), bold: true, small: true, brush: UmlBrush.Accent), item);
                x += c.Keys + 14;
            }
            if (attribute.Comment is { } comment)
                scene.Add(style.Text(comment, new Point(x, textTop), italic: true, small: true, brush: UmlBrush.MutedText), item);
            y += RowHeight;
        }
    }
}
