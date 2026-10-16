using Avalonia;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>
/// Turns the structured flow of a <see cref="UmlActivityDiagram"/> into a graph (decisions, merges, fork bars, back
/// edges of loops), lays it out top to bottom, in swimlanes when the diagram has any, and paints it.
/// </summary>
internal sealed class ActivitySceneBuilder(UmlActivityDiagram diagram, UmlStyle style)
{
    private enum NodeKind
    {
        Initial,
        Final,
        FlowFinal,
        Action,
        Reference,
        Decision,
        Merge,
        Fork,
        Join,
        Note,
    }

    private sealed class FlowNode(NodeKind kind, string? text, object model, int lane)
    {
        public NodeKind Kind { get; } = kind;
        public string? Text { get; } = text;
        public object Model { get; } = model;
        public int Lane { get; } = lane;
        public int Branches { get; set; } = 2;
        public LayoutNode? Layout { get; set; }
    }

    private sealed record FlowEdge(FlowNode From, FlowNode To, string? Label, bool IsNote = false, bool LabelNearStart = false);

    private readonly record struct Open(FlowNode Node, string? Label, bool FromBranch = false);

    private readonly List<FlowNode> _nodes = [];
    private readonly List<FlowEdge> _edges = [];
    private readonly List<string> _lanes = [];
    private int _lane;
    private FlowNode? _last;
    private string? _pendingLabel;

    private bool LeftToRight => diagram.Direction == UmlDirection.LeftToRight;

    public void Build(UmlScene scene, Point origin)
    {
        Process(diagram.Statements, []);
        if (_nodes.Count == 0)
            return;

        var hasLanes = _lanes.Count > 0;
        var laneHeader = hasLanes ? style.Measure("Xg", bold: true).Height + 18 : 0;
        var layout = new LayeredLayout
        {
            LeftToRight = LeftToRight && !hasLanes,
            NodeSpacing = 36,
            LayerSpacing = 40,
            Lanes = hasLanes ? _lanes.Select(l => style.Measure(l, bold: true).Width + 40).ToList() : null,
        };
        var layoutNodes = _nodes.Select(n => n.Layout = new LayoutNode(Measure(n), n) { Lane = n.Lane }).ToList();
        var layoutEdges = _edges.Select(e =>
        {
            var edge = new LayoutEdge(e.From.Layout!, e.To.Layout!, e);
            if (e.IsNote)
            {
                edge.MinLength = 0;
                edge.OrderHint = e.To.Kind == NodeKind.Note && e.Label == "left" ? -1 : 1;
            }
            else if (e.Label is { Length: > 0 } label)
                edge.LabelSize = style.Measure(label, small: true) + new Size(12, 4);
            return edge;
        }).ToList();
        var size = layout.Run(layoutNodes, layoutEdges);
        var offset = new Vector(origin.X, origin.Y + (hasLanes ? laneHeader + 12 : 0));

        if (hasLanes)
            PaintLanes(scene, layout, origin, laneHeader, size.Height + laneHeader + 24);

        foreach (var edge in layoutEdges)
            PaintEdge(scene, (FlowEdge)edge.Tag!, edge.Points.Select(p => p + offset).ToList());
        foreach (var node in _nodes)
            PaintNode(scene, node, node.Layout!.Bounds.Translate(offset));
    }

    // ---- flow graph ----

    private FlowNode Add(NodeKind kind, string? text, object model)
    {
        var node = new FlowNode(kind, text, model, _lane);
        _nodes.Add(node);
        return node;
    }

    private void Connect(List<Open> open, FlowNode to)
    {
        foreach (var o in open)
        {
            var label = o.Label ?? _pendingLabel;
            _edges.Add(new FlowEdge(o.Node, to, label, LabelNearStart: o.FromBranch));
        }
        _pendingLabel = null;
    }

    /// <summary>Adds the statements after the open ends; returns the new open ends.</summary>
    private List<Open> Process(IEnumerable<UmlActivityStatement> statements, List<Open> open)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case UmlActivityStart:
                {
                    var node = Add(NodeKind.Initial, null, statement);
                    Connect(open, node);
                    open = [new Open(node, null)];
                    _last = node;
                    break;
                }
                case UmlActivityStop or UmlActivityEnd:
                {
                    var node = Add(statement is UmlActivityStop ? NodeKind.Final : NodeKind.FlowFinal, null, statement);
                    Connect(open, node);
                    open = [];
                    _last = node;
                    break;
                }
                case UmlActivityDetach:
                    open = [];
                    break;
                case UmlActivityAction action:
                {
                    var node = Add(NodeKind.Action, action.Text, statement);
                    Connect(open, node);
                    open = [new Open(node, null)];
                    _last = node;
                    break;
                }
                case UmlActivityReference reference:
                {
                    var node = Add(NodeKind.Reference, reference.Name, statement);
                    Connect(open, node);
                    open = [new Open(node, null)];
                    _last = node;
                    break;
                }
                case UmlActivityLane lane:
                {
                    var index = _lanes.IndexOf(lane.Name);
                    if (index < 0)
                    {
                        _lanes.Add(lane.Name);
                        index = _lanes.Count - 1;
                    }
                    _lane = index;
                    break;
                }
                case UmlActivityArrow arrow:
                    _pendingLabel = arrow.Label;
                    break;
                case UmlActivityNote note:
                {
                    if (_last is null)
                        break;
                    var node = new FlowNode(NodeKind.Note, note.Text, statement, _last.Lane);
                    _nodes.Add(node);
                    _edges.Add(new FlowEdge(_last, node, note.Position == UmlNotePosition.Left ? "left" : null, IsNote: true));
                    break;
                }
                case UmlActivityIf conditional:
                {
                    var decision = Add(NodeKind.Decision, conditional.Branches.FirstOrDefault()?.Condition, statement);
                    Connect(open, decision);
                    var ends = new List<Open>();
                    var current = decision;
                    var hasElse = false;
                    foreach (var branch in conditional.Branches)
                    {
                        if (branch != conditional.Branches[0] && branch.Condition is not null)
                        {
                            // elseif: a further decision on the "no" side of the previous one.
                            var next = Add(NodeKind.Decision, branch.Condition, statement);
                            _edges.Add(new FlowEdge(current, next, null));
                            current = next;
                        }
                        hasElse |= branch.Condition is null;
                        _last = current;
                        ends.AddRange(Process(branch.Statements, [new Open(current, branch.Label, true)]));
                    }
                    if (!hasElse)
                        ends.Add(new Open(current, null, true));
                    open = MergeEnds(ends, statement);
                    break;
                }
                case UmlActivityWhile loop:
                {
                    var decision = Add(NodeKind.Decision, loop.Condition, statement);
                    Connect(open, decision);
                    _last = decision;
                    var bodyEnds = Process(loop.Body, [new Open(decision, loop.Label, true)]);
                    Connect(bodyEnds, decision);
                    open = [new Open(decision, loop.EndLabel, true)];
                    _last = decision;
                    break;
                }
                case UmlActivityRepeat repeat:
                {
                    var start = Add(NodeKind.Merge, null, statement);
                    Connect(open, start);
                    _last = start;
                    var bodyEnds = Process(repeat.Body, [new Open(start, null)]);
                    var decision = Add(NodeKind.Decision, repeat.Condition, statement);
                    Connect(bodyEnds, decision);
                    _edges.Add(new FlowEdge(decision, start, repeat.Label, LabelNearStart: true));
                    open = [new Open(decision, repeat.EndLabel, true)];
                    _last = decision;
                    break;
                }
                case UmlActivityFork fork:
                {
                    var ends = new List<Open>();
                    if (fork.IsSplit)
                    {
                        foreach (var branch in fork.Branches)
                            ends.AddRange(Process(branch, open));
                        open = ends;
                        break;
                    }
                    var bar = Add(NodeKind.Fork, null, statement);
                    bar.Branches = fork.Branches.Count;
                    Connect(open, bar);
                    foreach (var branch in fork.Branches)
                        ends.AddRange(Process(branch, [new Open(bar, null)]));
                    if (ends.Count == 0)
                    {
                        open = [];
                        break;
                    }
                    var join = Add(fork.IsMerge ? NodeKind.Merge : NodeKind.Join, null, statement);
                    join.Branches = fork.Branches.Count;
                    Connect(ends, join);
                    open = [new Open(join, null)];
                    _last = join;
                    break;
                }
            }
        }
        return open;
    }

    /// <summary>The branches of a decision meet in a merge node (none needed for a single open end).</summary>
    private List<Open> MergeEnds(List<Open> ends, object model)
    {
        if (ends.Count <= 1)
            return ends;
        var merge = Add(NodeKind.Merge, null, model);
        Connect(ends, merge);
        _last = merge;
        return [new Open(merge, null)];
    }

    // ---- measuring and painting ----

    private Size Measure(FlowNode node)
    {
        var text = style.Measure(node.Text ?? "");
        return node.Kind switch
        {
            NodeKind.Initial => new Size(20, 20),
            NodeKind.Final => new Size(24, 24),
            NodeKind.FlowFinal => new Size(22, 22),
            NodeKind.Merge => new Size(24, 24),
            NodeKind.Decision when string.IsNullOrEmpty(node.Text) => new Size(24, 24),
            NodeKind.Decision => new Size(text.Width + 36, text.Height + 14),
            NodeKind.Fork or NodeKind.Join => LeftToRight && _lanes.Count == 0
                ? new Size(6, Math.Max(70, node.Branches * 56)) : new Size(Math.Max(70, node.Branches * 56), 6),
            NodeKind.Reference => new Size(Math.Max(120, text.Width + 48), text.Height + 34),
            NodeKind.Note => new Size(text.Width + 30, text.Height + 16),
            _ => new Size(Math.Max(80, text.Width + 32), Math.Max(36, text.Height + 18)),
        };
    }

    private static UmlAnchor Anchor(FlowNode node) => node.Kind switch
    {
        NodeKind.Initial or NodeKind.Final or NodeKind.FlowFinal => UmlAnchor.Ellipse,
        NodeKind.Merge => UmlAnchor.Diamond,
        NodeKind.Decision when string.IsNullOrEmpty(node.Text) => UmlAnchor.Diamond,
        _ => UmlAnchor.Rectangle,
    };

    private void PaintNode(UmlScene scene, FlowNode node, Rect r)
    {
        // Merges and join bars belong to their if / fork statement, only the visible steps are selectable.
        var item = node.Kind is NodeKind.Action or NodeKind.Reference or NodeKind.Decision or NodeKind.Note
            ? scene.AddItem(new UmlHitItem(node.Model, r) { CornerRadius = 10 })
            : -1;
        switch (node.Kind)
        {
            case NodeKind.Initial:
                scene.Add(new GlyphPrim { Glyph = UmlGlyph.Dot, Rect = r, Fill = UmlBrush.Solid }, item);
                break;
            case NodeKind.Final:
                scene.Add(new GlyphPrim { Glyph = UmlGlyph.Bullseye, Rect = r, Brush = UmlBrush.Solid, Fill = UmlBrush.Background }, item);
                break;
            case NodeKind.FlowFinal:
                scene.Add(new GlyphPrim { Glyph = UmlGlyph.CircleCross, Rect = r, Brush = UmlBrush.Line, Fill = UmlBrush.Node }, item);
                break;
            case NodeKind.Merge:
                scene.Add(new ShapePrim { Shape = UmlShape.Diamond, Rect = r, Shadow = true }, item);
                break;
            case NodeKind.Decision:
                if (string.IsNullOrEmpty(node.Text))
                    scene.Add(new ShapePrim { Shape = UmlShape.Diamond, Rect = r, Shadow = true }, item);
                else
                {
                    scene.Add(new ShapePrim { Shape = UmlShape.Hexagon, Rect = r, Fill = UmlBrush.Header, Shadow = true }, item);
                    var size = style.Measure(node.Text);
                    scene.Add(style.Text(node.Text, new Point(r.Center.X - size.Width / 2, r.Center.Y - size.Height / 2), center: true), item);
                }
                break;
            case NodeKind.Fork or NodeKind.Join:
                scene.Add(new ShapePrim { Shape = UmlShape.RoundedRectangle, Rect = r, Radius = 3, Fill = UmlBrush.Solid, Stroke = UmlBrush.None }, item);
                break;
            case NodeKind.Reference:
            {
                var tag = style.Measure("ref", bold: true, small: true);
                scene.Add(new ShapePrim { Shape = UmlShape.Frame, Rect = r, Extra = tag.Width + 18, Extra2 = tag.Height + 6, Shadow = true }, item);
                scene.Add(style.Text("ref", new Point(r.X + 7, r.Y + 3), bold: true, small: true), item);
                var size = style.Measure(node.Text ?? "");
                scene.Add(style.Text(node.Text ?? "", new Point(r.Center.X - size.Width / 2, r.Y + (r.Height + tag.Height - size.Height) / 2), center: true), item);
                break;
            }
            case NodeKind.Note:
                scene.Add(new ShapePrim { Shape = UmlShape.Note, Rect = r, Fill = UmlBrush.Note, Stroke = UmlBrush.NoteBorder, Shadow = true }, item);
                scene.Add(style.Text(node.Text ?? "", new Point(r.X + 10, r.Y + 8)), item);
                break;
            default:
            {
                scene.Add(new ShapePrim { Shape = UmlShape.RoundedRectangle, Rect = r, Radius = Math.Min(r.Height / 2, 14), Shadow = true }, item);
                var size = style.Measure(node.Text ?? "");
                scene.Add(style.Text(node.Text ?? "", new Point(r.Center.X - size.Width / 2, r.Center.Y - size.Height / 2), center: true), item);
                break;
            }
        }
    }

    private void PaintEdge(UmlScene scene, FlowEdge edge, List<Point> points)
    {
        if (points.Count < 2)
            return;
        var fromRect = edge.From.Layout!.Bounds.Translate(points[0] - edge.From.Layout.Center);
        var toRect = edge.To.Layout!.Bounds.Translate(points[^1] - edge.To.Layout.Center);
        points[0] = UmlGeometry.Clip(fromRect, Anchor(edge.From), points[1]);
        points[^1] = UmlGeometry.Clip(toRect, Anchor(edge.To), points[^2]);

        var painter = new UmlEdgePainter(style);
        painter.Paint(scene, new UmlEdgePainter.Edge
        {
            Points = points,
            Curve = points.Count > 2 ? UmlCurve.Smooth : UmlCurve.Polyline,
            Stroke = edge.IsNote ? UmlStroke.Dashed : UmlStroke.Solid,
            Thickness = edge.IsNote ? 1 : 1.25,
            ToHead = edge.IsNote ? UmlArrowHead.None : UmlArrowHead.Arrow,
            Label = edge.IsNote ? null : edge.Label,
            LabelAt = edge.LabelNearStart ? 0.3 : 0.5,
        }, -1);
    }

    private void PaintLanes(UmlScene scene, LayeredLayout layout, Point origin, double header, double height)
    {
        for (var i = 0; i < layout.LaneBounds.Count; i++)
        {
            var (x, width) = layout.LaneBounds[i];
            var rect = new Rect(origin.X + x, origin.Y, width, height);
            scene.Add(new ShapePrim
            {
                Shape = UmlShape.Rectangle,
                Rect = rect,
                Fill = i % 2 == 0 ? UmlBrush.Lane : UmlBrush.None,
                Stroke = UmlBrush.None,
            });
            if (i > 0)
                scene.Add(new LinePrim { Points = [rect.TopLeft, rect.BottomLeft], Brush = UmlBrush.Border, Thickness = 1 });
            var name = i < _lanes.Count ? _lanes[i] : "";
            scene.Add(style.CenteredText(name, rect.Center.X, rect.Y + 9, bold: true));
        }
        var right = origin.X + layout.LaneBounds.Sum(l => l.Width);
        scene.Add(new LinePrim { Points = [new Point(origin.X, origin.Y + header), new Point(right, origin.Y + header)], Brush = UmlBrush.Border, Thickness = 1 });
        scene.Add(new ShapePrim { Shape = UmlShape.RoundedRectangle, Rect = new Rect(origin.X, origin.Y, right - origin.X, height), Radius = 8, Fill = UmlBrush.None, Stroke = UmlBrush.Border });
    }
}
