using Avalonia;

namespace KD.Avalonia.Rice.Controls.Uml;

internal sealed class LayoutNode(Size size, object? tag = null)
{
    public Size Size { get; set; } = size;

    public object? Tag { get; } = tag;

    /// <summary>Fixed top-left position; the node is not moved by the layout.</summary>
    public Point? Fixed { get; set; }

    /// <summary>Swimlane index (lane mode only).</summary>
    public int Lane { get; set; }

    /// <summary>Output: top-left corner.</summary>
    public Point Position { get; set; }

    public Rect Bounds => new(Position, Size);

    public Point Center => Bounds.Center;

    internal bool IsDummy { get; init; }
    internal int Index { get; set; }
    internal int Layer { get; set; }
    internal int Order { get; set; }
    internal double X { get; set; }
    internal List<LayoutNode> Upper { get; } = [];
    internal List<LayoutNode> Lower { get; } = [];
}

internal sealed class LayoutEdge(LayoutNode from, LayoutNode to, object? tag = null)
{
    public LayoutNode From { get; } = from;

    public LayoutNode To { get; } = to;

    public object? Tag { get; } = tag;

    /// <summary>Layers between the ends; 0 keeps them side by side.</summary>
    public int MinLength { get; set; } = 1;

    /// <summary>Layering goes from <see cref="To"/> to <see cref="From"/> (To is placed above / before From).</summary>
    public bool Reverse { get; set; }

    /// <summary>For side-by-side edges: +1 puts To after From, -1 before.</summary>
    public int OrderHint { get; set; } = 1;

    /// <summary>Space the label needs; the layout keeps room for it between the layers.</summary>
    public Size LabelSize { get; set; }

    /// <summary>Output: polyline from the center of From to the center of To through the bends.</summary>
    public List<Point> Points { get; } = [];

    public bool IsSelfLoop => From == To;

    internal List<LayoutNode> Chain { get; } = [];
    internal bool Flat { get; set; }
}

/// <summary>
/// Layered graph layout (Sugiyama): cycle removal, longest-path layering, dummy nodes for long edges, barycenter
/// crossing reduction and median based coordinates. Disconnected parts are laid out separately and packed into rows.
/// Optionally the nodes are kept in swimlanes (columns).
/// </summary>
internal sealed class LayeredLayout
{
    private const double DummyWidth = 6;

    public double NodeSpacing { get; set; } = 36;

    public double LayerSpacing { get; set; } = 56;

    public double ComponentSpacing { get; set; } = 48;

    public bool LeftToRight { get; set; }

    /// <summary>Swimlane mode: the minimum width of each lane (e.g. for its title); null for the normal layout.</summary>
    public IReadOnlyList<double>? Lanes { get; set; }

    public double LanePadding { get; set; } = 20;

    /// <summary>Output (lane mode): left edge and width of each lane.</summary>
    public List<(double X, double Width)> LaneBounds { get; } = [];

    public Size Run(IReadOnlyList<LayoutNode> nodes, IReadOnlyList<LayoutEdge> edges)
    {
        LaneBounds.Clear();
        if (nodes.Count == 0)
            return default;

        for (var i = 0; i < nodes.Count; i++)
            nodes[i].Index = i;
        if (LeftToRight)
            Transpose(nodes, edges, sizesOnly: true);

        var laneMode = Lanes is { Count: > 0 };
        var components = laneMode ? [nodes.ToList()] : FindComponents(nodes, edges);
        var placed = new List<(List<LayoutNode> Nodes, List<LayoutEdge> Edges, Rect Bounds)>();
        foreach (var component in components)
        {
            var set = component.ToHashSet();
            var componentEdges = edges.Where(e => set.Contains(e.From) && set.Contains(e.To)).ToList();
            var bounds = LayoutComponent(component, componentEdges, laneMode);
            placed.Add((component, componentEdges, bounds));
        }

        Pack(placed);
        ApplyFixedPositions(nodes, edges);
        var size = Normalize(nodes, edges);

        if (LeftToRight)
        {
            Transpose(nodes, edges, sizesOnly: false);
            size = new Size(size.Height, size.Width);
        }
        return size;
    }

    // ---- components ----

    private static List<List<LayoutNode>> FindComponents(IReadOnlyList<LayoutNode> nodes, IReadOnlyList<LayoutEdge> edges)
    {
        var parent = nodes.ToDictionary(n => n, n => n);
        LayoutNode Root(LayoutNode n)
        {
            while (parent[n] != n)
                n = parent[n] = parent[parent[n]];
            return n;
        }
        foreach (var e in edges.Where(e => !e.IsSelfLoop && parent.ContainsKey(e.From) && parent.ContainsKey(e.To)))
            parent[Root(e.From)] = Root(e.To);
        return nodes.GroupBy(Root).Select(g => g.ToList()).ToList();
    }

    private Rect LayoutComponent(List<LayoutNode> nodes, List<LayoutEdge> edges, bool laneMode)
    {
        var real = edges.Where(e => !e.IsSelfLoop).ToList();
        AssignLayers(nodes, real);
        var layers = BuildLayers(nodes, real);
        Order(layers, real, laneMode);
        var bounds = laneMode ? AssignLaneCoordinates(layers, real) : AssignCoordinates(layers, real);
        RouteEdges(real);
        return bounds;
    }

    // ---- layering ----

    private static void AssignLayers(List<LayoutNode> nodes, List<LayoutEdge> edges)
    {
        // Layering direction of each edge (u above v); back edges of cycles are flipped.
        var directed = edges.Select(e => e.Reverse ? (U: e.To, V: e.From, e) : (U: e.From, V: e.To, e)).ToList();
        var outgoing = nodes.ToDictionary(n => n, _ => new List<int>());
        for (var i = 0; i < directed.Count; i++)
            outgoing[directed[i].U].Add(i);

        // Iterative DFS in input order; an edge to a node on the stack closes a cycle.
        var state = nodes.ToDictionary(n => n, _ => 0);
        var flipped = new HashSet<int>();
        foreach (var start in nodes)
        {
            if (state[start] != 0)
                continue;
            var stack = new Stack<(LayoutNode Node, int Next)>();
            stack.Push((start, 0));
            state[start] = 1;
            while (stack.Count > 0)
            {
                var (node, next) = stack.Pop();
                var list = outgoing[node];
                if (next < list.Count)
                {
                    stack.Push((node, next + 1));
                    var edgeIndex = list[next];
                    var target = directed[edgeIndex].V;
                    if (state[target] == 1)
                        flipped.Add(edgeIndex);
                    else if (state[target] == 0)
                    {
                        state[target] = 1;
                        stack.Push((target, 0));
                    }
                }
                else
                    state[node] = 2;
            }
        }

        var dag = directed.Select((d, i) => flipped.Contains(i) ? (U: d.V, V: d.U, d.e) : (d.U, d.V, d.e)).ToList();

        // Longest path in topological order.
        var inDegree = nodes.ToDictionary(n => n, _ => 0);
        foreach (var (_, v, _) in dag)
            inDegree[v]++;
        var queue = new Queue<LayoutNode>(nodes.Where(n => inDegree[n] == 0));
        var topological = new List<LayoutNode>();
        foreach (var n in nodes)
            n.Layer = 0;
        var successors = nodes.ToDictionary(n => n, _ => new List<(LayoutNode V, int MinLength)>());
        foreach (var (u, v, e) in dag)
            successors[u].Add((v, e.MinLength));
        while (queue.Count > 0)
        {
            var u = queue.Dequeue();
            topological.Add(u);
            foreach (var (v, minLength) in successors[u])
            {
                v.Layer = Math.Max(v.Layer, u.Layer + minLength);
                if (--inDegree[v] == 0)
                    queue.Enqueue(v);
            }
        }

        // Sources are pulled down next to their first successor (keeps notes and leaves close).
        var predecessors = nodes.ToDictionary(n => n, _ => 0);
        foreach (var (_, v, _) in dag)
            predecessors[v]++;
        for (var i = topological.Count - 1; i >= 0; i--)
        {
            var u = topological[i];
            if (predecessors[u] > 0 || successors[u].Count == 0)
                continue;
            u.Layer = successors[u].Min(s => s.V.Layer - s.MinLength);
        }

        var min = nodes.Min(n => n.Layer);
        foreach (var n in nodes)
            n.Layer -= min;

        // Edges that ended up within one layer are drawn side by side.
        foreach (var (u, v, e) in dag)
            e.Flat = u.Layer == v.Layer;
    }

    private static List<List<LayoutNode>> BuildLayers(List<LayoutNode> nodes, List<LayoutEdge> edges)
    {
        var count = nodes.Max(n => n.Layer) + 1;
        var layers = Enumerable.Range(0, count).Select(_ => new List<LayoutNode>()).ToList();
        foreach (var n in nodes.OrderBy(n => n.Index))
        {
            n.Upper.Clear();
            n.Lower.Clear();
            layers[n.Layer].Add(n);
        }

        foreach (var e in edges)
        {
            e.Chain.Clear();
            if (e.Flat)
                continue;
            var (u, v) = e.From.Layer < e.To.Layer ? (e.From, e.To) : (e.To, e.From);
            e.Chain.Add(u);
            for (var layer = u.Layer + 1; layer < v.Layer; layer++)
            {
                var dummy = new LayoutNode(new Size(DummyWidth + e.LabelSize.Width * (layer == (u.Layer + v.Layer) / 2 ? 1 : 0), 0))
                {
                    IsDummy = true,
                    Layer = layer,
                    Index = u.Index,
                    Lane = u.Lane,
                };
                layers[layer].Add(dummy);
                e.Chain.Add(dummy);
            }
            e.Chain.Add(v);
            for (var i = 0; i + 1 < e.Chain.Count; i++)
            {
                e.Chain[i].Lower.Add(e.Chain[i + 1]);
                e.Chain[i + 1].Upper.Add(e.Chain[i]);
            }
        }
        return layers;
    }

    // ---- ordering ----

    private static void Order(List<List<LayoutNode>> layers, List<LayoutEdge> edges, bool laneMode)
    {
        foreach (var layer in layers)
            SetOrder(layer);

        var best = layers.Select(l => l.ToList()).ToList();
        var bestCrossings = CountCrossings(layers);
        for (var iteration = 0; iteration < 12 && bestCrossings > 0; iteration++)
        {
            var down = iteration % 2 == 0;
            for (var k = 1; k < layers.Count; k++)
            {
                var index = down ? k : layers.Count - 1 - k;
                var layer = layers[index];
                var keys = layer.ToDictionary(n => n, n =>
                {
                    var neighbors = down ? n.Upper : n.Lower;
                    return neighbors.Count == 0 ? n.Order : neighbors.Average(m => (double)m.Order);
                });
                layer.Sort((a, b) =>
                {
                    if (laneMode && a.Lane != b.Lane)
                        return a.Lane.CompareTo(b.Lane);
                    var c = keys[a].CompareTo(keys[b]);
                    return c != 0 ? c : a.Order.CompareTo(b.Order);
                });
                ApplyFlatConstraints(layer, edges);
                SetOrder(layer);
            }

            var crossings = CountCrossings(layers);
            if (crossings < bestCrossings)
            {
                bestCrossings = crossings;
                best = layers.Select(l => l.ToList()).ToList();
            }
        }

        for (var i = 0; i < layers.Count; i++)
        {
            layers[i].Clear();
            layers[i].AddRange(best[i]);
            if (laneMode)
                layers[i].Sort((a, b) => a.Lane != b.Lane ? a.Lane.CompareTo(b.Lane) : a.Order.CompareTo(b.Order));
            ApplyFlatConstraints(layers[i], edges);
            SetOrder(layers[i]);
        }
    }

    private static void SetOrder(List<LayoutNode> layer)
    {
        for (var i = 0; i < layer.Count; i++)
            layer[i].Order = i;
    }

    /// <summary>
    /// Side by side edges: the nodes they connect form a contiguous block, To right after (or before) From. The block
    /// stays where its first member was.
    /// </summary>
    private static void ApplyFlatConstraints(List<LayoutNode> layer, List<LayoutEdge> edges)
    {
        var flat = edges.Where(e => e.Flat && e.From.Layer == e.To.Layer && layer.Contains(e.From) && layer.Contains(e.To)).ToList();
        if (flat.Count == 0)
            return;

        var group = layer.ToDictionary(n => n, n => n);
        LayoutNode Root(LayoutNode n)
        {
            while (group[n] != n)
                n = group[n] = group[group[n]];
            return n;
        }
        foreach (var e in flat)
            group[Root(e.From)] = Root(e.To);

        // Within a block: topological order of "first before second", ties by the current order.
        var position = layer.Select((n, i) => (n, i)).ToDictionary(p => p.n, p => p.i);
        var blocks = new Dictionary<LayoutNode, List<LayoutNode>>();
        foreach (var members in layer.GroupBy(Root).Where(g => g.Count() > 1))
        {
            var set = members.ToHashSet();
            var before = members.ToDictionary(m => m, _ => new List<LayoutNode>());
            foreach (var e in flat.Where(e => set.Contains(e.From)))
            {
                var (first, second) = e.OrderHint >= 0 ? (e.From, e.To) : (e.To, e.From);
                before[second].Add(first);
            }
            var ordered = new List<LayoutNode>();
            var remaining = members.OrderBy(m => position[m]).ToList();
            while (remaining.Count > 0)
            {
                var next = remaining.FirstOrDefault(m => before[m].All(ordered.Contains)) ?? remaining[0];
                ordered.Add(next);
                remaining.Remove(next);
            }
            blocks[members.Key] = ordered;
        }

        var result = new List<LayoutNode>(layer.Count);
        var emitted = new HashSet<LayoutNode>();
        foreach (var n in layer)
        {
            if (emitted.Contains(n))
                continue;
            if (blocks.TryGetValue(Root(n), out var block))
            {
                result.AddRange(block);
                emitted.UnionWith(block);
            }
            else
            {
                result.Add(n);
                emitted.Add(n);
            }
        }
        layer.Clear();
        layer.AddRange(result);
    }

    private static int CountCrossings(List<List<LayoutNode>> layers)
    {
        var total = 0;
        for (var i = 0; i + 1 < layers.Count; i++)
        {
            var segments = new List<(int A, int B)>();
            foreach (var n in layers[i])
                foreach (var m in n.Lower)
                    segments.Add((n.Order, m.Order));
            for (var a = 0; a < segments.Count; a++)
                for (var b = a + 1; b < segments.Count; b++)
                    if ((segments[a].A - segments[b].A) * (segments[a].B - segments[b].B) < 0)
                        total++;
        }
        return total;
    }

    // ---- coordinates ----

    private (double[] Top, double[] Height) LayerRows(List<List<LayoutNode>> layers, List<LayoutEdge> edges)
    {
        var heights = layers.Select(l => l.Count == 0 ? 0 : l.Max(n => n.Size.Height)).ToArray();
        // Room for the labels of the edges between two layers.
        var gaps = new double[layers.Count];
        for (var i = 0; i < gaps.Length; i++)
            gaps[i] = LayerSpacing;
        foreach (var e in edges.Where(e => !e.Flat && e.LabelSize.Height > 0 && e.Chain.Count >= 2))
        {
            var span = e.Chain[^1].Layer - e.Chain[0].Layer;
            var mid = e.Chain[0].Layer + (span - 1) / 2;
            gaps[mid] = Math.Max(gaps[mid], e.LabelSize.Height + 28);
        }

        var tops = new double[layers.Count];
        double y = 0;
        for (var i = 0; i < layers.Count; i++)
        {
            tops[i] = y;
            y += heights[i] + gaps[i];
        }
        return (tops, heights);
    }

    private double Separation(LayoutNode a, LayoutNode b, List<LayoutEdge> edges)
    {
        var spacing = a.IsDummy && b.IsDummy ? NodeSpacing / 3 : a.IsDummy || b.IsDummy ? NodeSpacing / 1.6 : NodeSpacing;
        var extra = 0.0;
        foreach (var e in edges)
            if (e.Flat && ((e.From == a && e.To == b) || (e.From == b && e.To == a)))
                extra = Math.Max(extra, e.LabelSize.Width + 16);
        return (a.Size.Width + b.Size.Width) / 2 + Math.Max(spacing, extra);
    }

    private Rect AssignCoordinates(List<List<LayoutNode>> layers, List<LayoutEdge> edges)
    {
        var flatEdges = edges.Where(e => e.Flat).ToList();
        foreach (var layer in layers)
        {
            double x = 0;
            for (var i = 0; i < layer.Count; i++)
            {
                if (i > 0)
                    x += Separation(layer[i - 1], layer[i], flatEdges);
                layer[i].X = x;
            }
        }

        for (var iteration = 0; iteration < 8; iteration++)
        {
            var down = iteration % 2 == 0;
            for (var k = 0; k < layers.Count; k++)
            {
                var layer = layers[down ? k : layers.Count - 1 - k];
                var desired = layer.Select(n =>
                {
                    var neighbors = down ? n.Upper : n.Lower;
                    if (neighbors.Count == 0)
                        neighbors = down ? n.Lower : n.Upper;
                    if (neighbors.Count > 0)
                        return Median(neighbors.Select(m => m.X));
                    // Only side by side neighbors (e.g. a note): stay next to them.
                    foreach (var e in flatEdges)
                    {
                        var partner = e.From == n ? e.To : e.To == n ? e.From : null;
                        if (partner is null || partner.Upper.Count + partner.Lower.Count == 0)
                            continue;
                        var after = (e.To == n) == (e.OrderHint >= 0);
                        return partner.X + (after ? 1 : -1) * Separation(partner, n, flatEdges);
                    }
                    return n.X;
                }).ToArray();
                Place(layer, desired, flatEdges);
            }
        }

        var (tops, heights) = LayerRows(layers, edges);
        var minX = layers.SelectMany(l => l).Min(n => n.X - n.Size.Width / 2);
        foreach (var layer in layers)
            foreach (var n in layer)
                n.Position = new Point(n.X - n.Size.Width / 2 - minX, tops[n.Layer] + (heights[n.Layer] - n.Size.Height) / 2);
        return BoundsOf(layers.SelectMany(l => l));
    }

    /// <summary>Moves the nodes of a layer as close to their desired centers as the order and spacing allow.</summary>
    private void Place(List<LayoutNode> layer, double[] desired, List<LayoutEdge> flatEdges)
    {
        var n = layer.Count;
        if (n == 0)
            return;
        var left = new double[n];
        var right = new double[n];
        for (var i = 0; i < n; i++)
            left[i] = i == 0 ? desired[0] : Math.Max(desired[i], left[i - 1] + Separation(layer[i - 1], layer[i], flatEdges));
        for (var i = n - 1; i >= 0; i--)
            right[i] = i == n - 1 ? desired[i] : Math.Min(desired[i], right[i + 1] - Separation(layer[i], layer[i + 1], flatEdges));
        for (var i = 0; i < n; i++)
        {
            var x = (left[i] + right[i]) / 2;
            if (i > 0)
                x = Math.Max(x, layer[i - 1].X + Separation(layer[i - 1], layer[i], flatEdges));
            layer[i].X = x;
        }
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    private Rect AssignLaneCoordinates(List<List<LayoutNode>> layers, List<LayoutEdge> edges)
    {
        var laneCount = Math.Max(Lanes!.Count, layers.SelectMany(l => l).Max(n => n.Lane) + 1);
        var flatEdges = edges.Where(e => e.Flat).ToList();
        double Row(IList<LayoutNode> nodes)
        {
            double width = 0;
            for (var i = 0; i < nodes.Count; i++)
                width += i == 0 ? nodes[i].Size.Width : Separation(nodes[i - 1], nodes[i], flatEdges) - nodes[i - 1].Size.Width / 2 + nodes[i].Size.Width / 2;
            return width;
        }

        var widths = new double[laneCount];
        for (var lane = 0; lane < laneCount; lane++)
        {
            var min = lane < Lanes.Count ? Lanes[lane] : 0;
            var content = layers.Select(l => Row(l.Where(n => n.Lane == lane).ToList())).DefaultIfEmpty(0).Max();
            widths[lane] = Math.Max(min, content + LanePadding * 2);
        }

        double x0 = 0;
        for (var lane = 0; lane < laneCount; lane++)
        {
            LaneBounds.Add((x0, widths[lane]));
            x0 += widths[lane];
        }

        var (tops, heights) = LayerRows(layers, edges);
        foreach (var layer in layers)
        {
            foreach (var group in layer.GroupBy(n => n.Lane))
            {
                var nodes = group.OrderBy(n => n.Order).ToList();
                var (laneX, laneWidth) = LaneBounds[Math.Clamp(group.Key, 0, laneCount - 1)];
                var x = laneX + (laneWidth - Row(nodes)) / 2;
                for (var i = 0; i < nodes.Count; i++)
                {
                    if (i > 0)
                        x += Separation(nodes[i - 1], nodes[i], flatEdges) - nodes[i - 1].Size.Width / 2 - nodes[i].Size.Width / 2;
                    var node = nodes[i];
                    node.Position = new Point(x, tops[node.Layer] + (heights[node.Layer] - node.Size.Height) / 2);
                    x += node.Size.Width;
                }
            }
        }
        var bounds = BoundsOf(layers.SelectMany(l => l));
        return new Rect(0, bounds.Y, Math.Max(bounds.Right, x0), bounds.Height);
    }

    private static void RouteEdges(List<LayoutEdge> edges)
    {
        foreach (var e in edges)
        {
            e.Points.Clear();
            if (e.Flat || e.Chain.Count < 2)
            {
                e.Points.Add(e.From.Center);
                e.Points.Add(e.To.Center);
                continue;
            }
            var points = e.Chain.Select(n => n.IsDummy ? new Point(n.Position.X + n.Size.Width / 2, n.Position.Y) : n.Center).ToList();
            if (e.Chain[0] != e.From)
                points.Reverse();
            e.Points.AddRange(points);
        }
    }

    private static Rect BoundsOf(IEnumerable<LayoutNode> nodes)
    {
        var list = nodes.ToList();
        var left = list.Min(n => n.Position.X);
        var top = list.Min(n => n.Position.Y);
        var right = list.Max(n => n.Position.X + n.Size.Width);
        var bottom = list.Max(n => n.Position.Y + n.Size.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    // ---- packing, fixed positions, normalization ----

    private void Pack(List<(List<LayoutNode> Nodes, List<LayoutEdge> Edges, Rect Bounds)> components)
    {
        if (components.Count <= 1)
            return;
        var totalArea = components.Sum(c => (c.Bounds.Width + ComponentSpacing) * (c.Bounds.Height + ComponentSpacing));
        var rowWidth = Math.Max(components.Max(c => c.Bounds.Width), Math.Sqrt(totalArea) * 1.6);

        double x = 0, y = 0, rowHeight = 0;
        foreach (var (nodes, edges, bounds) in components.OrderBy(c => c.Nodes.Min(n => n.Index)))
        {
            if (x > 0 && x + bounds.Width > rowWidth)
            {
                x = 0;
                y += rowHeight + ComponentSpacing;
                rowHeight = 0;
            }
            var offset = new Vector(x - bounds.X, y - bounds.Y);
            foreach (var n in nodes)
                n.Position += offset;
            foreach (var e in edges)
                for (var i = 0; i < e.Points.Count; i++)
                    e.Points[i] += offset;
            x += bounds.Width + ComponentSpacing;
            rowHeight = Math.Max(rowHeight, bounds.Height);
        }
    }

    private void ApplyFixedPositions(IReadOnlyList<LayoutNode> nodes, IReadOnlyList<LayoutEdge> edges)
    {
        var moved = new HashSet<LayoutNode>();
        foreach (var n in nodes)
        {
            if (n.Fixed is not { } fixedPosition)
                continue;
            // In left-to-right mode the layout runs transposed.
            n.Position = LeftToRight ? new Point(fixedPosition.Y, fixedPosition.X) : fixedPosition;
            moved.Add(n);
        }
        foreach (var e in edges.Where(e => !e.IsSelfLoop && (moved.Contains(e.From) || moved.Contains(e.To))))
        {
            e.Points.Clear();
            e.Points.Add(e.From.Center);
            e.Points.Add(e.To.Center);
        }
    }

    private static Size Normalize(IReadOnlyList<LayoutNode> nodes, IReadOnlyList<LayoutEdge> edges)
    {
        var bounds = BoundsOf(nodes);
        var offset = new Vector(-Math.Min(0, bounds.X), -Math.Min(0, bounds.Y));
        if (offset != default)
        {
            foreach (var n in nodes)
                n.Position += offset;
            foreach (var e in edges)
                for (var i = 0; i < e.Points.Count; i++)
                    e.Points[i] += offset;
        }
        return new Size(bounds.Right + offset.X, bounds.Bottom + offset.Y);
    }

    private static void Transpose(IReadOnlyList<LayoutNode> nodes, IReadOnlyList<LayoutEdge> edges, bool sizesOnly)
    {
        foreach (var n in nodes)
        {
            n.Size = new Size(n.Size.Height, n.Size.Width);
            if (!sizesOnly)
                n.Position = new Point(n.Position.Y, n.Position.X);
        }
        if (sizesOnly)
        {
            foreach (var e in edges)
                e.LabelSize = new Size(e.LabelSize.Height, e.LabelSize.Width);
            return;
        }
        foreach (var e in edges)
        {
            e.LabelSize = new Size(e.LabelSize.Height, e.LabelSize.Width);
            for (var i = 0; i < e.Points.Count; i++)
                e.Points[i] = new Point(e.Points[i].Y, e.Points[i].X);
        }
    }
}
