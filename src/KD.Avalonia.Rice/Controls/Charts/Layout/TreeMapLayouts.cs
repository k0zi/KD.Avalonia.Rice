using System.Globalization;
using Avalonia;

namespace KD.Avalonia.Rice.Controls.Charts;

/// <summary>Layout of the treemap: squarified rectangles, groups with a label strip over their children.</summary>
internal static class TreeMapLayouts
{
    private const double Gap = 1;

    public static ChartScene TreeMap(ChartLayoutContext ctx, IReadOnlyList<TreeMapNode>? nodes, bool showValues)
    {
        var scene = new ChartScene();
        if (nodes is null || nodes.All(n => n.Total <= 0))
        {
            ChartFrame.Empty(ctx, scene, new Rect(ctx.Size));
            return scene;
        }

        var area = ChartFrame.Begin(ctx, scene, nodes.Select((n, i) => (n.Label, ChartPaint.Series(i, n.Color))).ToList());
        var values = nodes.Select((n, i) => ctx.IsHidden(i) ? 0 : n.Total).ToList();
        var total = values.Sum();
        var rects = Squarify(values, area);
        for (var i = 0; i < nodes.Count; i++)
            if (values[i] > 0)
                Node(ctx, scene, nodes[i], rects[i], ChartPaint.Series(i, nodes[i].Color), total, nodes[i].Label, showValues);
        return scene;
    }

    private static void Node(ChartLayoutContext ctx, ChartScene scene, TreeMapNode node, Rect slot, ChartPaint paint, double total,
        string path, bool showValues)
    {
        var style = ctx.Style;
        var rect = slot.Width > Gap * 2 && slot.Height > Gap * 2 ? slot.Deflate(Gap) : slot;
        var value = node.Total;
        var share = total > 0 ? value / total : 0;
        var tooltip = $"{style.Format(value)} ({share.ToString("P1", CultureInfo.CurrentCulture)})";
        var line = style.SmallLine;
        var header = line + 4;
        var children = node.Children.Where(c => c.Total > 0).ToList();

        // A group: tinted, its label in a strip on top and the children below, when they have room.
        if (children.Count > 0 && rect.Width >= 24 && rect.Height >= header + 12)
        {
            var strip = new Rect(rect.X, rect.Y, rect.Width, header);
            var item = scene.AddItem(new ChartHitItem(node, tooltip) { TooltipTitle = path, Paint = paint, Bounds = strip, CornerRadius = 3 });
            scene.Add(new RectPrim { Rect = rect, Radius = Math.Min(3, rect.Width / 4), Fill = paint, Opacity = 0.18 }, item);
            var label = style.Fit(node.Label, rect.Width - 8, bold: true, small: true);
            if (label.Length > 0)
                scene.Add(style.Text(label, new Point(rect.X + 4, rect.Y + 2), bold: true, small: true), item);

            var inner = new Rect(rect.X + 2, rect.Y + header, rect.Width - 4, rect.Height - header - 2);
            var rects = Squarify(children.Select(c => c.Total).ToList(), inner);
            for (var k = 0; k < children.Count; k++)
            {
                var child = children[k];
                var childPaint = child.Color is { } color ? paint with { Color = color } : paint;
                Node(ctx, scene, child, rects[k], childPaint, total, $"{path} › {child.Label}", showValues);
            }
            return;
        }

        // A leaf (or a group too small to show its children): grows from its center.
        var p = ctx.Progress;
        var grown = new Rect(rect.Center.X - rect.Width * p / 2, rect.Center.Y - rect.Height * p / 2, rect.Width * p, rect.Height * p);
        var radius = Math.Min(3, Math.Min(rect.Width, rect.Height) / 4);
        var leaf = scene.AddItem(new ChartHitItem(node, tooltip) { TooltipTitle = path, Paint = paint, Bounds = rect, CornerRadius = radius });
        scene.Add(new RectPrim { Rect = grown, Radius = radius, Fill = paint }, leaf);
        if (p < 1)
            return;

        // Label (and value) in the top-left corner when they fit.
        var name = style.Fit(node.Label, rect.Width - 8, bold: true, small: true);
        if (name.Length == 0 || rect.Height < line + 4 || name != node.Label && name.Length < 4)
            return;
        scene.Add(style.Text(name, new Point(rect.X + 4, rect.Y + 2), bold: true, small: true, on: paint), leaf);
        var text = style.Format(value);
        if (showValues && rect.Height >= line * 2 + 4 && style.Measure(text, small: true).Width <= rect.Width - 8)
            scene.Add(style.Text(text, new Point(rect.X + 4, rect.Y + 2 + line), small: true, on: paint), leaf);
    }

    /// <summary>
    /// Squarified treemap (Bruls, Huizing, van Wijk): rectangles filling <paramref name="rect"/> with areas in proportion
    /// to the values, as close to squares as the order allows. The result is in the order of the values; a value ≤ 0
    /// gets an empty rectangle.
    /// </summary>
    public static Rect[] Squarify(IReadOnlyList<double> values, Rect rect)
    {
        var result = Enumerable.Repeat(new Rect(rect.Position, default(Size)), values.Count).ToArray();
        var order = Enumerable.Range(0, values.Count).Where(i => values[i] > 0 && double.IsFinite(values[i])).OrderByDescending(i => values[i]).ToList();
        var total = order.Sum(i => values[i]);
        if (total <= 0 || rect.Width <= 0 || rect.Height <= 0)
            return result;

        var scale = rect.Width * rect.Height / total;
        var free = rect;
        var row = new List<int>();
        var k = 0;
        while (k < order.Count)
        {
            var side = Math.Min(free.Width, free.Height);
            if (row.Count == 0 || Worst([.. row, order[k]], side) <= Worst(row, side))
                row.Add(order[k++]);
            else
            {
                free = Place(row, free, last: false);
                row.Clear();
            }
        }
        Place(row, free, last: true);
        return result;

        double Worst(IReadOnlyList<int> indices, double side)
        {
            var sum = indices.Sum(i => values[i]) * scale;
            var max = indices.Max(i => values[i]) * scale;
            var min = indices.Min(i => values[i]) * scale;
            return Math.Max(side * side * max / (sum * sum), sum * sum / (side * side * min));
        }

        // Lays the row along the shorter side of the free room and returns what is left; the last row takes all of it.
        Rect Place(List<int> indices, Rect room, bool last)
        {
            var sum = indices.Sum(i => values[i]) * scale;
            var at = room.Position;
            if (room.Width >= room.Height)
            {
                var width = last ? room.Width : Math.Min(room.Width, sum / room.Height);
                foreach (var i in indices)
                {
                    var height = values[i] * scale / sum * room.Height;
                    result[i] = new Rect(at.X, at.Y, width, height);
                    at = new Point(at.X, at.Y + height);
                }
                return new Rect(room.X + width, room.Y, room.Width - width, room.Height);
            }
            else
            {
                var height = last ? room.Height : Math.Min(room.Height, sum / room.Width);
                foreach (var i in indices)
                {
                    var width = values[i] * scale / sum * room.Width;
                    result[i] = new Rect(at.X, at.Y, width, height);
                    at = new Point(at.X + width, at.Y);
                }
                return new Rect(room.X, room.Y + height, room.Width, room.Height - height);
            }
        }
    }
}
