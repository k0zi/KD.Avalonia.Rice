using System.Globalization;
using Avalonia;
using KD.Avalonia.Rice.Controls.Uml;

namespace KD.Avalonia.Rice.Controls.Markdowns;

/// <summary>Pie chart: slices in the chart colors with percentages, legend on the right.</summary>
internal sealed class PieSceneBuilder(MermaidPie pie, UmlStyle style)
{
    public void Build(UmlScene scene, Point origin)
    {
        var total = pie.Slices.Sum(s => s.Value);
        if (total <= 0)
            return;
        var radius = Math.Round(style.FontSize * 10);
        var center = new Point(origin.X + radius + 4, origin.Y + radius + 4);
        var angle = 0.0;
        var legendX = center.X + radius + 40;
        var rowHeight = style.Measure("Ag").Height + 10;
        var legendY = center.Y - pie.Slices.Count * rowHeight / 2;

        for (var i = 0; i < pie.Slices.Count; i++)
        {
            var slice = pie.Slices[i];
            var sweep = slice.Value / total * 360;
            var brush = UmlStyle.Series(i);
            var middle = (angle + sweep / 2 - 90) * Math.PI / 180;
            var labelPoint = new Point(center.X + radius * 0.66 * Math.Cos(middle), center.Y + radius * 0.66 * Math.Sin(middle));

            var legendRow = new Rect(legendX - 4, legendY + i * rowHeight - 3, 0, rowHeight);
            var label = pie.ShowData ? $"{slice.Label}  [{slice.Value.ToString("0.##", CultureInfo.CurrentCulture)}]" : slice.Label;
            var labelSize = style.Measure(label);
            legendRow = legendRow.WithWidth(labelSize.Width + 30);
            var hit = new Rect(labelPoint.X - radius * 0.2, labelPoint.Y - radius * 0.2, radius * 0.4, radius * 0.4);
            var item = scene.AddItem(new UmlHitItem(slice, hit) { CornerRadius = radius * 0.2 });

            scene.Add(new WedgePrim { Center = center, Radius = radius, StartAngle = angle, SweepAngle = sweep, Fill = brush }, item);
            var percent = (slice.Value / total).ToString("P0", CultureInfo.CurrentCulture);
            if (sweep >= 18)
            {
                var size = style.Measure(percent, bold: true, small: true);
                scene.Add(style.Text(percent, new Point(labelPoint.X - size.Width / 2, labelPoint.Y - size.Height / 2), bold: true, small: true, brush: UmlBrush.OnSeries), item);
            }

            // Legend
            var swatch = new Rect(legendX, legendY + i * rowHeight + (rowHeight - 14) / 2 - 3, 14, 14);
            scene.Add(new ShapePrim { Shape = UmlShape.RoundedRectangle, Rect = swatch, Radius = 3, Fill = brush, Stroke = UmlBrush.None }, item);
            scene.Add(style.Text(label, new Point(legendX + 22, legendY + i * rowHeight + (rowHeight - labelSize.Height) / 2 - 3)), item);
            angle += sweep;
        }
    }
}

/// <summary>Gantt chart: time axis on top, a band per section, a bar per task.</summary>
internal sealed class GanttSceneBuilder(MermaidGantt gantt, UmlStyle style)
{
    public void Build(UmlScene scene, Point origin)
    {
        var tasks = gantt.Sections.SelectMany(s => s.Tasks).ToList();
        if (tasks.Count == 0)
            return;
        var start = tasks.Min(t => t.Start).Date;
        var end = tasks.Max(t => t.End);
        if (end <= start)
            end = start.AddDays(1);
        var days = Math.Max(1, (end - start).TotalDays);
        var pxPerDay = Math.Clamp(760 / days, 3, 64);

        var rowHeight = Math.Round(style.FontSize * 2.3);
        var barHeight = Math.Round(rowHeight * 0.62);
        var labelWidth = gantt.Sections.Select(s => style.Measure(s.Name, bold: true, small: true).Width).DefaultIfEmpty().Max();
        labelWidth = labelWidth > 0 ? labelWidth + 24 : 0;
        var axisHeight = style.Measure("Ag", small: true).Height + 12;
        var chartLeft = origin.X + labelWidth;
        var chartTop = origin.Y + axisHeight;
        var width = days * pxPerDay;
        double X(DateTime time) => chartLeft + (time - start).TotalDays * pxPerDay;

        // Section bands
        var y = chartTop;
        for (var s = 0; s < gantt.Sections.Count; s++)
        {
            var section = gantt.Sections[s];
            var height = Math.Max(1, section.Tasks.Count) * rowHeight;
            var band = new Rect(origin.X, y, labelWidth + width, height);
            var item = scene.AddItem(new UmlHitItem(section, new Rect(origin.X, y, Math.Max(labelWidth, 1), height)) { CornerRadius = 0 });
            if (s % 2 == 0)
                scene.Add(new ShapePrim { Shape = UmlShape.Rectangle, Rect = band, Fill = UmlBrush.Lane, Stroke = UmlBrush.None }, item);
            if (section.Name.Length > 0)
            {
                var size = style.Measure(section.Name, bold: true, small: true);
                scene.Add(style.Text(section.Name, new Point(origin.X + 8, y + (height - size.Height) / 2), bold: true, small: true, brush: UmlBrush.MutedText), item);
            }
            y += height;
        }
        var chartBottom = y;

        // Time axis
        var (step, format) = days switch
        {
            <= 16 => (TimeSpan.FromDays(1), "MMM d"),
            <= 90 => (TimeSpan.FromDays(7), "MMM d"),
            <= 500 => (TimeSpan.Zero, "MMM yyyy"),
            _ => (TimeSpan.FromDays(-1), "yyyy"),
        };
        format = gantt.AxisFormat ?? format;
        var tick = step == TimeSpan.FromDays(7) ? start.AddDays(((int)DayOfWeek.Monday - (int)start.DayOfWeek + 7) % 7)
            : step == TimeSpan.Zero ? new DateTime(start.Year, start.Month, 1).AddMonths(start.Day > 1 ? 1 : 0)
            : step < TimeSpan.Zero ? new DateTime(start.Year + (start.DayOfYear > 1 ? 1 : 0), 1, 1)
            : start;
        var lastLabelRight = double.NegativeInfinity;
        while (tick <= end)
        {
            var x = X(tick);
            scene.Add(new LinePrim { Points = [new Point(x, chartTop), new Point(x, chartBottom)], Brush = UmlBrush.Border, Thickness = 1, Style = UmlStroke.Dotted });
            var text = tick.ToString(format, CultureInfo.CurrentCulture);
            var size = style.Measure(text, small: true);
            if (x - size.Width / 2 > lastLabelRight + 6)
            {
                scene.Add(style.Text(text, new Point(x - size.Width / 2, origin.Y), small: true, brush: UmlBrush.MutedText));
                lastLabelRight = x + size.Width / 2;
            }
            tick = step == TimeSpan.Zero ? tick.AddMonths(1) : step < TimeSpan.Zero ? tick.AddYears(1) : tick + step;
        }
        scene.Add(new LinePrim { Points = [new Point(chartLeft, chartTop), new Point(chartLeft + width, chartTop)], Brush = UmlBrush.Border, Thickness = 1 });

        // Bars
        y = chartTop;
        for (var s = 0; s < gantt.Sections.Count; s++)
        {
            foreach (var task in gantt.Sections[s].Tasks)
            {
                var top = y + (rowHeight - barHeight) / 2;
                var x1 = X(task.Start);
                var x2 = Math.Max(X(task.End), x1 + 2);
                var textSize = style.Measure(task.Name, small: true);
                if (task.IsMilestone)
                {
                    var diamond = new Rect(x1 - barHeight / 2, top, barHeight, barHeight);
                    var item = scene.AddItem(new UmlHitItem(task, diamond));
                    scene.Add(new ShapePrim { Shape = UmlShape.Diamond, Rect = diamond, Fill = task.IsCritical ? UmlBrush.Private : UmlStyle.Series(s + 1), Stroke = UmlBrush.Background, Thickness = 1.5 }, item);
                    scene.Add(style.Text(task.Name, new Point(diamond.Right + 6, y + (rowHeight - textSize.Height) / 2), small: true), item);
                }
                else
                {
                    var bar = new Rect(x1, top, x2 - x1, barHeight);
                    var item = scene.AddItem(new UmlHitItem(task, bar) { CornerRadius = 4 });
                    var fill = task.IsCritical ? UmlBrush.Private : task.IsDone ? UmlBrush.Header : UmlStyle.Series(s + 1);
                    scene.Add(new ShapePrim
                    {
                        Shape = UmlShape.RoundedRectangle,
                        Rect = bar,
                        Radius = 4,
                        Fill = fill,
                        Stroke = task.IsActive ? UmlBrush.Accent : task.IsDone ? UmlBrush.Border : UmlBrush.None,
                        Thickness = task.IsActive ? 2 : 1,
                    }, item);
                    var inside = textSize.Width + 12 <= bar.Width;
                    var textBrush = inside && !task.IsDone ? UmlBrush.OnSeries : UmlBrush.Text;
                    var textX = inside ? bar.Center.X - textSize.Width / 2 : bar.Right + 6;
                    scene.Add(style.Text(task.Name, new Point(textX, y + (rowHeight - textSize.Height) / 2), small: true, brush: textBrush), item);
                }
                y += rowHeight;
            }
            if (gantt.Sections[s].Tasks.Count == 0)
                y += rowHeight;
        }
    }
}

/// <summary>Mind map: the root in the middle, the branches to the left and right in their own colors.</summary>
internal sealed class MindmapSceneBuilder(MermaidMindmap map, UmlStyle style)
{
    private const double LevelGap = 44;
    private const double SiblingGap = 12;

    private readonly Dictionary<MermaidMindmapNode, Size> _sizes = [];
    private readonly Dictionary<MermaidMindmapNode, Rect> _rects = [];

    public void Build(UmlScene scene, Point origin)
    {
        if (map.Root is not { } root)
            return;
        Measure(root, 0);

        // Branches go to the side that is shorter so far.
        var right = new List<MermaidMindmapNode>();
        var left = new List<MermaidMindmapNode>();
        double rightHeight = 0, leftHeight = 0;
        foreach (var child in root.Children)
        {
            var height = SubtreeHeight(child);
            if (rightHeight <= leftHeight)
            {
                right.Add(child);
                rightHeight += height + SiblingGap;
            }
            else
            {
                left.Add(child);
                leftHeight += height + SiblingGap;
            }
        }

        var rootSize = _sizes[root];
        var rootRect = new Rect(-rootSize.Width / 2, -rootSize.Height / 2, rootSize.Width, rootSize.Height);
        _rects[root] = rootRect;
        PlaceChildren(right, rootRect, +1);
        PlaceChildren(left, rootRect, -1);

        // Move everything to the origin.
        var bounds = _rects.Values.Aggregate((a, b) => a.Union(b));
        var shift = new Vector(origin.X - bounds.X, origin.Y - bounds.Y);
        foreach (var node in _rects.Keys.ToList())
            _rects[node] = _rects[node].Translate(shift);

        var branch = 0;
        foreach (var child in root.Children)
            PaintBranch(scene, root, child, UmlStyle.Series(++branch), 1);
        PaintNode(scene, root, UmlStyle.Series(0), 0);
    }

    private void Measure(MermaidMindmapNode node, int depth)
    {
        var text = style.Measure(node.Text, bold: depth <= 1, small: depth >= 3);
        var pad = depth == 0 ? 18 : depth == 1 ? 12 : 4;
        var size = new Size(text.Width + 2 * pad, text.Height + (depth <= 1 ? 16 : 8));
        if (depth == 0 && node.Shape is MermaidMindmapShape.Circle or MermaidMindmapShape.Default)
        {
            var d = Math.Max(size.Width, size.Height) + 8;
            size = new Size(d, d);
        }
        else if (node.Shape is MermaidMindmapShape.Cloud or MermaidMindmapShape.Bang)
            size = new Size(size.Width + 28, size.Height + 18);
        else if (node.Shape == MermaidMindmapShape.Hexagon)
            size = size.WithWidth(size.Width + 20);
        else if (node.Shape == MermaidMindmapShape.Circle)
            size = new Size(Math.Max(size.Width, size.Height), Math.Max(size.Width, size.Height));
        _sizes[node] = size;
        foreach (var child in node.Children)
            Measure(child, depth + 1);
    }

    private double SubtreeHeight(MermaidMindmapNode node)
    {
        var children = node.Children.Sum(SubtreeHeight) + Math.Max(0, node.Children.Count - 1) * SiblingGap;
        return Math.Max(_sizes[node].Height, children);
    }

    private void PlaceChildren(List<MermaidMindmapNode> children, Rect parent, int side)
    {
        var total = children.Sum(SubtreeHeight) + Math.Max(0, children.Count - 1) * SiblingGap;
        var y = parent.Center.Y - total / 2;
        foreach (var child in children)
        {
            var height = SubtreeHeight(child);
            var size = _sizes[child];
            var x = side > 0 ? parent.Right + LevelGap : parent.X - LevelGap - size.Width;
            var rect = new Rect(x, y + (height - size.Height) / 2, size.Width, size.Height);
            _rects[child] = rect;
            PlaceChildren(child.Children, rect, side);
            y += height + SiblingGap;
        }
    }

    private void PaintBranch(UmlScene scene, MermaidMindmapNode parent, MermaidMindmapNode node, UmlBrush brush, int depth)
    {
        var from = _rects[parent];
        var to = _rects[node];
        var right = to.Center.X > from.Center.X;
        var underlined = depth >= 3 && parent.Shape == MermaidMindmapShape.Default;
        var start = new Point(right ? from.Right : from.X, underlined ? from.Bottom : from.Center.Y);
        // Deeper nodes are underlined text: the branch ends at the underline.
        var endY = depth >= 2 && node.Shape == MermaidMindmapShape.Default ? to.Bottom : to.Center.Y;
        var end = new Point(right ? to.X : to.Right, endY);
        var dx = (end.X - start.X) / 2;
        scene.Add(new LinePrim
        {
            Points = [start, new Point(start.X + dx, start.Y), new Point(end.X - dx, end.Y), end],
            Curve = UmlCurve.Bezier,
            Brush = brush,
            Thickness = Math.Max(1.5, 4 - depth),
        });
        foreach (var child in node.Children)
            PaintBranch(scene, node, child, brush, depth + 1);
        PaintNode(scene, node, brush, depth);
    }

    private void PaintNode(UmlScene scene, MermaidMindmapNode node, UmlBrush brush, int depth)
    {
        var r = _rects[node];
        var item = scene.AddItem(new UmlHitItem(node, r) { CornerRadius = r.Height / 2 });
        var text = style.Measure(node.Text, bold: depth <= 1, small: depth >= 3);
        var textTop = r.Center.Y - text.Height / 2;
        if (depth >= 2 && node.Shape == MermaidMindmapShape.Default)
        {
            scene.Add(new LinePrim { Points = [r.BottomLeft, r.BottomRight], Brush = brush, Thickness = Math.Max(1.5, 4 - depth) }, item);
            scene.Add(style.CenteredText(node.Text, r.Center.X, textTop - 2, small: depth >= 3), item);
            return;
        }
        var shape = node.Shape switch
        {
            MermaidMindmapShape.Square => UmlShape.Rectangle,
            MermaidMindmapShape.Circle => UmlShape.Ellipse,
            MermaidMindmapShape.Hexagon => UmlShape.Hexagon,
            MermaidMindmapShape.Cloud or MermaidMindmapShape.Bang => UmlShape.Cloud,
            MermaidMindmapShape.Default when depth == 0 => UmlShape.Ellipse,
            _ => UmlShape.RoundedRectangle,
        };
        var radius = node.Shape == MermaidMindmapShape.Rounded ? 8 : r.Height / 2;
        scene.Add(new ShapePrim { Shape = shape, Rect = r, Radius = radius, Fill = brush, Stroke = UmlBrush.None, Shadow = true }, item);
        scene.Add(style.CenteredText(node.Text, r.Center.X, textTop, bold: depth <= 1, small: depth >= 3, brush: UmlBrush.OnSeries), item);
    }
}
