using Avalonia;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>
/// Lays out and paints a <see cref="UmlGraphDiagram"/>. Containers are laid out from the inside out: the content of a
/// container first, then the container takes part in the layout of its level as one box.
/// </summary>
internal sealed class GraphSceneBuilder
{
    private sealed class Level
    {
        public Size Size { get; set; }
        public Dictionary<UmlElement, Rect> Rects { get; } = [];
        public Dictionary<UmlElement, Level> Children { get; } = [];
        public Dictionary<UmlRelation, List<Point>> Routes { get; } = [];
        public Vector ContentOffset { get; set; }
    }

    private readonly UmlGraphDiagram _diagram;
    private readonly UmlStyle _style;
    private readonly UmlNodePainter _painter;
    private readonly UmlEdgePainter _edges;
    private readonly Dictionary<UmlElement, UmlElement?> _parents = [];
    private readonly Dictionary<string, UmlElement> _byId = [];
    private readonly Dictionary<UmlElement, Rect> _absolute = [];
    private readonly Dictionary<UmlRelation, List<Point>> _routes = [];
    private readonly HashSet<UmlElement> _containers = [];
    private Point _center;

    public GraphSceneBuilder(UmlGraphDiagram diagram, UmlStyle style)
    {
        _diagram = diagram;
        _style = style;
        _painter = new UmlNodePainter(style, diagram.Kind, diagram.Direction == UmlDirection.LeftToRight);
        _edges = new UmlEdgePainter(style);
        foreach (var e in diagram.Elements)
            Index(e, null);
    }

    private void Index(UmlElement element, UmlElement? parent)
    {
        _parents[element] = parent;
        _byId.TryAdd(element.Id, element);
        foreach (var child in element.Children)
            Index(child, element);
    }

    private static bool IsContainer(UmlElement e) => e.Children.Any(c => !c.IsPort) && !UmlNodePainter.IsClassifier(e.Kind);

    public void Build(UmlScene scene, Point origin)
    {
        var top = LayoutLevel(_diagram.Elements);
        Place(top, origin);
        PlacePorts();
        RouteRemaining();
        Paint(scene);
    }

    // ---- layout ----

    private Level LayoutLevel(IReadOnlyList<UmlElement> elements)
    {
        var level = new Level();
        var members = elements.Where(e => !e.IsPort).ToList();
        if (members.Count == 0)
            return level;

        var nodes = new Dictionary<UmlElement, LayoutNode>();
        foreach (var e in members)
        {
            Size size;
            if (IsContainer(e))
            {
                _containers.Add(e);
                var inner = LayoutLevel(e.Children);
                level.Children[e] = inner;
                var padding = _painter.ContainerPadding(e);
                var min = _painter.MinContainerSize(e);
                var width = Math.Max(min.Width, inner.Size.Width + padding.Left + padding.Right);
                var height = Math.Max(min.Height, inner.Size.Height + padding.Top + padding.Bottom);
                inner.ContentOffset = new Vector(padding.Left + (width - padding.Left - padding.Right - inner.Size.Width) / 2, padding.Top);
                size = e.Size ?? new Size(width, height);
            }
            else
                size = _painter.Measure(e);
            nodes[e] = new LayoutNode(size, e) { Fixed = e.Position };
        }

        // Relations are lifted to this level: an edge into a nested element pulls its container.
        var set = members.ToHashSet();
        UmlElement? Lift(string id)
        {
            if (!_byId.TryGetValue(id, out var e))
                return null;
            for (UmlElement? current = e; current is not null; current = _parents[current])
                if (set.Contains(current))
                    return current;
            return null;
        }

        var edges = new List<LayoutEdge>();
        var direct = new Dictionary<LayoutEdge, UmlRelation>();
        foreach (var relation in _diagram.Relations)
        {
            var from = Lift(relation.From);
            var to = Lift(relation.To);
            if (from is null || to is null || from == to)
                continue;
            var edge = CreateEdge(relation, nodes[from], nodes[to], from, to);
            edges.Add(edge);
            if (from.Id == relation.From && to.Id == relation.To)
                direct[edge] = relation;
        }

        // Communication messages are written along the links: those need longer lines.
        var communication = _diagram.Kind == UmlDiagramKind.Communication;
        var layout = new LayeredLayout
        {
            LeftToRight = _diagram.Direction == UmlDirection.LeftToRight,
            NodeSpacing = communication ? 150 : 40,
            LayerSpacing = communication ? 110 : 60,
        };
        level.Size = layout.Run(nodes.Values.ToList(), edges);
        foreach (var (element, node) in nodes)
            level.Rects[element] = node.Bounds;
        foreach (var (edge, relation) in direct)
            level.Routes[relation] = [.. edge.Points];
        return level;
    }

    private LayoutEdge CreateEdge(UmlRelation relation, LayoutNode fromNode, LayoutNode toNode, UmlElement from, UmlElement to)
    {
        var edge = new LayoutEdge(fromNode, toNode, relation);
        var label = relation.Label ?? (relation.Messages.Count > 0 ? string.Join("\n", relation.Messages.Select(m => m.Text)) : null);
        var labelSize = string.IsNullOrEmpty(label) ? default : _style.Measure(label, small: true);
        // Multiplicities sit near the ends: room for them above and below the label.
        var ends = new[] { relation.FromLabel, relation.ToLabel }.Count(t => !string.IsNullOrEmpty(t));
        var endHeight = ends == 0 ? 0 : _style.Measure("1", small: true).Height + 6;
        if (labelSize.Width > 0 || ends > 0)
            edge.LabelSize = new Size(labelSize.Width + 12, labelSize.Height + 4 + endHeight * ends);

        switch (relation.Direction)
        {
            case UmlLinkDirection.Up:
                edge.Reverse = true;
                edge.MinLength = Math.Max(1, relation.Length - 1);
                break;
            case UmlLinkDirection.Down:
                edge.MinLength = Math.Max(1, relation.Length - 1);
                break;
            case UmlLinkDirection.Left:
                edge.MinLength = 0;
                edge.OrderHint = -1;
                break;
            case UmlLinkDirection.Right:
                edge.MinLength = 0;
                break;
            default:
                if (relation.Length <= 1 || from.Kind == UmlElementKind.Note || to.Kind == UmlElementKind.Note)
                    edge.MinLength = 0;
                else
                {
                    edge.MinLength = relation.Length - 1;
                    // The parent of a generalization goes above its children, whichever way it is written.
                    if (relation.ToHead is UmlArrowHead.Triangle or UmlArrowHead.FilledTriangle
                        && relation.FromHead is not (UmlArrowHead.Triangle or UmlArrowHead.FilledTriangle))
                        edge.Reverse = true;
                }
                break;
        }
        return edge;
    }

    private void Place(Level level, Point origin)
    {
        foreach (var (element, rect) in level.Rects)
        {
            var absolute = rect.Translate((Vector)origin);
            _absolute[element] = absolute;
            if (level.Children.TryGetValue(element, out var inner))
                Place(inner, absolute.TopLeft + inner.ContentOffset);
        }
        foreach (var (relation, points) in level.Routes)
            _routes[relation] = points.Select(p => p + (Vector)origin).ToList();
    }

    /// <summary>Ports sit on the border of their component: in on the left, out on the right, the others on top.</summary>
    private void PlacePorts()
    {
        foreach (var owner in _absolute.Keys.ToList())
        {
            var ports = owner.Children.Where(c => c.IsPort).ToList();
            if (ports.Count == 0)
                continue;
            var r = _absolute[owner];
            const double size = 12;
            void Distribute(List<UmlElement> list, Func<int, int, Point> position)
            {
                for (var i = 0; i < list.Count; i++)
                    _absolute[list[i]] = new Rect(position(i, list.Count), new Size(size, size));
            }
            Distribute(ports.Where(p => p.Kind == UmlElementKind.PortIn).ToList(),
                (i, n) => new Point(r.X - size / 2, r.Y + r.Height * (i + 1) / (n + 1) - size / 2));
            Distribute(ports.Where(p => p.Kind == UmlElementKind.PortOut).ToList(),
                (i, n) => new Point(r.Right - size / 2, r.Y + r.Height * (i + 1) / (n + 1) - size / 2));
            Distribute(ports.Where(p => p.Kind == UmlElementKind.Port).ToList(),
                (i, n) => new Point(r.X + r.Width * (i + 1) / (n + 1) - size / 2, r.Y - size / 2));
        }
    }

    /// <summary>Relations not routed by a layout level (into nested elements, ports): straight lines.</summary>
    private void RouteRemaining()
    {
        foreach (var relation in _diagram.Relations)
        {
            if (_routes.ContainsKey(relation))
                continue;
            if (!_byId.TryGetValue(relation.From, out var from) || !_byId.TryGetValue(relation.To, out var to)
                || !_absolute.TryGetValue(from, out var a) || !_absolute.TryGetValue(to, out var b) || from == to)
                continue;
            _routes[relation] = [a.Center, b.Center];
        }
    }

    // ---- painting ----

    private void Paint(UmlScene scene)
    {
        var items = new Dictionary<UmlElement, int>();
        if (_absolute.Count > 0)
            _center = new Point(_absolute.Values.Average(r => r.Center.X), _absolute.Values.Average(r => r.Center.Y));

        // Containers first, outer before inner, so their content is painted over them.
        foreach (var container in _containers.OrderBy(Depth))
        {
            var rect = _absolute[container];
            var item = scene.AddItem(new UmlHitItem(container, rect) { IsContainer = true, HeaderHeight = _painter.HeaderHeight(container) });
            items[container] = item;
            _painter.PaintContainer(scene, container, rect, item);
        }

        PaintRelations(scene);

        foreach (var (element, rect) in _absolute.OrderBy(p => p.Key.IsPort ? 1 : 0))
        {
            if (_containers.Contains(element))
                continue;
            var item = scene.AddItem(new UmlHitItem(element, rect) { CornerRadius = element.Kind == UmlElementKind.State ? 14 : 6 });
            items[element] = item;
            _painter.Paint(scene, element, rect, item);
            if (element.IsPort && element.Name.Length > 0)
            {
                var label = _style.Measure(element.Name, small: true);
                var origin = element.Kind switch
                {
                    UmlElementKind.PortIn => new Point(rect.X - label.Width - 3, rect.Y - label.Height),
                    UmlElementKind.PortOut => new Point(rect.Right + 3, rect.Y - label.Height),
                    _ => new Point(rect.Center.X - label.Width / 2, rect.Y - label.Height - 2),
                };
                scene.Add(_style.Text(element.Name, origin, small: true, brush: UmlBrush.MutedText), item);
            }
        }
    }

    private int Depth(UmlElement e)
    {
        var depth = 0;
        for (var p = _parents[e]; p is not null; p = _parents[p])
            depth++;
        return depth;
    }

    private void PaintRelations(UmlScene scene)
    {
        // Parallel lines between the same two elements are spread apart.
        var groups = _diagram.Relations.Where(r => _routes.ContainsKey(r) && r.LineStyle != UmlLineStyle.Hidden)
            .GroupBy(r => string.CompareOrdinal(r.From, r.To) < 0 ? (r.From, r.To) : (r.To, r.From));
        foreach (var group in groups)
        {
            var list = group.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                var relation = list[i];
                var points = _routes[relation];
                var offset = list.Count > 1 && points.Count == 2 ? (i - (list.Count - 1) / 2.0) * 16 : 0;
                // Offsets are measured in one direction for the whole group.
                if (offset != 0 && string.CompareOrdinal(relation.From, relation.To) > 0)
                    offset = -offset;
                PaintRelation(scene, relation, points, offset);
            }
        }

        foreach (var relation in _diagram.Relations.Where(r => r.From == r.To && r.LineStyle != UmlLineStyle.Hidden))
            if (_byId.TryGetValue(relation.From, out var element) && _absolute.TryGetValue(element, out var rect))
                PaintSelfLoop(scene, relation, element, rect);
    }

    private void PaintRelation(UmlScene scene, UmlRelation relation, List<Point> route, double offset)
    {
        var from = _byId[relation.From];
        var to = _byId[relation.To];
        var fromRect = _painter.AnchorRect(from, _absolute[from]);
        var toRect = _painter.AnchorRect(to, _absolute[to]);

        var points = route.ToList();
        if (points.Count < 2)
            return;
        points[0] = UmlGeometry.Clip(fromRect, _painter.Anchor(from), points[1]);
        points[^1] = UmlGeometry.Clip(toRect, _painter.Anchor(to), points[^2]);
        if (offset != 0)
            points = UmlGeometry.Offset(points, offset);

        var toHead = relation.ToHead;
        var fromHead = relation.FromHead;
        // Profiles: the extension of a metaclass has a filled triangle.
        if (_diagram.Kind == UmlDiagramKind.Profile)
        {
            if (toHead == UmlArrowHead.Triangle && to.Kind == UmlElementKind.Metaclass)
                toHead = UmlArrowHead.FilledTriangle;
            if (fromHead == UmlArrowHead.Triangle && from.Kind == UmlElementKind.Metaclass)
                fromHead = UmlArrowHead.FilledTriangle;
        }

        var isNoteLink = from.Kind == UmlElementKind.Note || to.Kind == UmlElementKind.Note;
        var edge = new UmlEdgePainter.Edge
        {
            Points = points,
            Curve = points.Count > 2 ? UmlCurve.Smooth : UmlCurve.Polyline,
            Stroke = relation.LineStyle switch
            {
                UmlLineStyle.Dashed => UmlStroke.Dashed,
                UmlLineStyle.Dotted => UmlStroke.Dotted,
                _ => UmlStroke.Solid,
            },
            Thickness = relation.LineStyle == UmlLineStyle.Bold ? 2.5 : isNoteLink ? 1 : 1.25,
            FromHead = fromHead,
            ToHead = toHead,
            Label = relation.Label,
            // Labels of parallel lines go beside them, so they do not cover the neighbouring line.
            LabelSide = Math.Sign(offset),
            LabelDirection = relation.LabelDirection,
            FromLabel = relation.FromLabel,
            ToLabel = relation.ToLabel,
            Messages = relation.Messages,
            Note = relation.Note,
            AwayFrom = relation.Messages.Count > 0 ? _center : null,
        };
        var item = scene.AddItem(new UmlHitItem(relation, default)
        {
            Path = points,
            Curve = edge.Curve,
            From = from,
            To = to,
        });
        scene.Items[item].Bounds = _edges.Paint(scene, edge, item);
    }

    private void PaintSelfLoop(UmlScene scene, UmlRelation relation, UmlElement element, Rect rect)
    {
        var start = new Point(rect.Right - Math.Min(24, rect.Width / 3), rect.Y);
        var end = new Point(rect.Right, rect.Y + Math.Min(20, rect.Height / 3));
        var points = new List<Point> { start, start + new Vector(4, -40), end + new Vector(40, -4), end };
        var edge = new UmlEdgePainter.Edge
        {
            Points = points,
            Curve = UmlCurve.Bezier,
            Stroke = relation.IsDashed ? UmlStroke.Dashed : UmlStroke.Solid,
            FromHead = relation.FromHead,
            ToHead = relation.ToHead,
        };
        var item = scene.AddItem(new UmlHitItem(relation, default) { Path = points, Curve = UmlCurve.Bezier, From = element, To = element });
        var bounds = _edges.Paint(scene, edge, item);
        if (relation.Label is { Length: > 0 } label)
            bounds = bounds.Union(_edges.PaintLabel(scene, label, new Point(rect.Right + 30, rect.Y - 26), item));
        scene.Items[item].Bounds = bounds;
    }
}
