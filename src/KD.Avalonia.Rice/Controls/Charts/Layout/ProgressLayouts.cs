using System.Globalization;
using Avalonia;

namespace KD.Avalonia.Rice.Controls.Charts;

/// <summary>Layouts of gantt, bullet and gauge charts.</summary>
internal static class ProgressLayouts
{
    // ---- gantt ----

    /// <summary>The groups of the tasks in order of appearance; tasks without a group form the group "".</summary>
    public static List<string> Groups(IEnumerable<GanttTask> tasks) => tasks.Select(t => t.Group ?? "").Distinct().ToList();

    public static ChartScene Gantt(ChartLayoutContext ctx, IReadOnlyList<GanttTask>? tasks, DateTime? today)
    {
        var scene = new ChartScene();
        var style = ctx.Style;
        if (tasks is null || tasks.Count == 0)
        {
            ChartFrame.Empty(ctx, scene, new Rect(ctx.Size));
            return scene;
        }

        var groups = Groups(tasks);
        var area = ChartFrame.Begin(ctx, scene, groups.Select((g, i) => (g.Length > 0 ? g : "–", ChartPaint.Series(i))).ToList());
        var visible = tasks.Where(t => !ctx.IsHidden(groups.IndexOf(t.Group ?? ""))).ToList();
        if (visible.Count == 0)
            return scene;
        var start = visible.Min(t => t.Start).Date;
        var end = visible.Max(t => t.End > t.Start ? t.End : t.Start);
        if (end <= start)
            end = start.AddDays(1);
        // A little room after the last date, so a milestone diamond there is not cut.
        end = end.AddDays(Math.Max(0.5, (end - start).TotalDays * 0.03));

        // Rows: a header row per named group (when there are groups), then its tasks.
        var showGroups = groups.Count > 1 || groups[0].Length > 0;
        var rows = new List<(string? Group, GanttTask? Task)>();
        foreach (var group in groups)
        {
            var groupTasks = visible.Where(t => (t.Group ?? "") == group).ToList();
            if (groupTasks.Count == 0)
                continue;
            if (showGroups && group.Length > 0)
                rows.Add((group, null));
            rows.AddRange(groupTasks.Select(t => ((string?)null, (GanttTask?)t)));
        }

        var line = style.SmallLine;
        var nameWidth = Math.Min(visible.Max(t => style.Measure(t.Name, small: true).Width) + (showGroups ? 12 : 0), area.Width * 0.32);
        var plot = new Rect(area.X + nameWidth + 10, area.Y + line + 8, Math.Max(1, area.Width - nameWidth - 14), Math.Max(1, area.Height - line - 8));
        var rowHeight = plot.Height / rows.Count;
        var days = Math.Max(1, (end - start).TotalDays);
        double X(DateTime time) => plot.X + (time - start).TotalDays / days * plot.Width;

        // Time axis on top, dotted lines down.
        var (ticks, format) = ChartAxis.DateTicks(start, end, plot.Width, style.Measure("Sep 30", small: true).Width + 16);
        var lastRight = double.NegativeInfinity;
        foreach (var tick in ticks)
        {
            var x = X(tick);
            scene.Add(new PathPrim { Points = [new Point(x, plot.Top), new Point(x, plot.Bottom)], Stroke = ChartBrush.Grid });
            var text = tick.ToString(format, CultureInfo.CurrentCulture);
            var size = style.Measure(text, small: true);
            if (x - size.Width / 2 < lastRight + 6 || x + size.Width / 2 > area.Right + 2)
                continue;
            scene.Add(style.Text(text, new Point(Math.Max(plot.X - size.Width / 2, x - size.Width / 2), area.Y), small: true, brush: ChartBrush.MutedText));
            lastRight = x + size.Width / 2;
        }

        var p = ctx.Progress;
        var centers = new Dictionary<GanttTask, (double Y, double Start, double End, double Half)>();
        for (var r = 0; r < rows.Count; r++)
        {
            var (group, task) = rows[r];
            var top = plot.Y + r * rowHeight;
            var middle = top + rowHeight / 2;
            if (task is null)
            {
                var size = style.Measure(group!, bold: true, small: true);
                scene.Add(new RectPrim { Rect = new Rect(area.X, top, area.Width, rowHeight), Fill = ChartBrush.Track, Opacity = 0.6 });
                scene.Add(style.Text(style.Fit(group!, area.Width - 8, bold: true, small: true), new Point(area.X + 4, middle - size.Height / 2),
                    bold: true, small: true, brush: ChartBrush.MutedText));
                continue;
            }

            var groupIndex = groups.IndexOf(task.Group ?? "");
            var paint = ChartPaint.Series(groupIndex, task.Color);
            var name = style.Fit(task.Name, nameWidth - (showGroups ? 12 : 0) + 1, small: true);
            var nameSize = style.Measure(name, small: true);
            if (rowHeight >= line * 0.8)
                scene.Add(style.Text(name, new Point(area.X + (showGroups ? 12 : 0), middle - nameSize.Height / 2), small: true));

            var x0 = X(task.Start);
            var x1 = X(task.End);
            var barHeight = Math.Max(2, Math.Min(rowHeight * 0.62, 26));
            var dates = task.IsMilestone
                ? task.Start.ToString("d", CultureInfo.CurrentCulture)
                : $"{task.Start.ToString("d", CultureInfo.CurrentCulture)} – {task.End.ToString("d", CultureInfo.CurrentCulture)}";
            var tooltip = task.Progress > 0 && !task.IsMilestone ? $"{dates}\n{Math.Clamp(task.Progress, 0, 1).ToString("P0", CultureInfo.CurrentCulture)}" : dates;
            if (task.IsMilestone)
            {
                var d = barHeight * 0.55 * p;
                var item = scene.AddItem(new ChartHitItem(task, tooltip)
                {
                    TooltipTitle = task.Name, Paint = paint, Bounds = new Rect(x0 - barHeight / 2, middle - barHeight / 2, barHeight, barHeight),
                });
                scene.Add(new PathPrim
                {
                    Points = [new Point(x0, middle - d), new Point(x0 + d, middle), new Point(x0, middle + d), new Point(x0 - d, middle)],
                    Closed = true, Fill = paint, Stroke = ChartPaint.None,
                }, item);
                centers[task] = (middle, x0, x0, barHeight / 2);
            }
            else
            {
                var width = Math.Max(2, (x1 - x0) * p);
                var bar = new Rect(x0, middle - barHeight / 2, width, barHeight);
                var radius = Math.Min(4, barHeight / 2);
                var item = scene.AddItem(new ChartHitItem(task, tooltip) { TooltipTitle = task.Name, Paint = paint, Bounds = bar, CornerRadius = radius });
                var progress = Math.Clamp(task.Progress, 0, 1);
                if (progress > 0)
                {
                    scene.Add(new RectPrim { Rect = bar, Radius = radius, Fill = paint, Opacity = 0.4 }, item);
                    scene.Add(new RectPrim { Rect = bar.WithWidth(Math.Max(radius * 2, bar.Width * progress)), Radius = radius, Fill = paint }, item);
                }
                else
                    scene.Add(new RectPrim { Rect = bar, Radius = radius, Fill = paint }, item);
                centers[task] = (middle, x0, x0 + width, barHeight / 2);
            }
        }

        // Dependencies: from the end of the earlier task, down (or up) and into the start of the later one.
        foreach (var task in visible)
        {
            if (task.DependsOn is not { Length: > 0 } dependsOn || !centers.TryGetValue(task, out var to))
                continue;
            var from = centers.FirstOrDefault(c => c.Key.Name == dependsOn).Value;
            if (centers.All(c => c.Key.Name != dependsOn))
                continue;
            Point tip;
            Vector direction;
            if (to.Start >= from.End + 14)
            {
                // Room between them: out of the end, across, into the start.
                var elbow = to.Start - 8;
                tip = new Point(to.Start - 1, to.Y);
                direction = new Vector(1, 0);
                scene.Add(new PathPrim { Points = [new Point(from.End, from.Y), new Point(elbow, from.Y), new Point(elbow, to.Y), tip], Stroke = ChartBrush.MutedText, Thickness = 1.25 });
            }
            else
            {
                // Overlapping: straight down (or up) from the end of the earlier bar onto the later one.
                var x = Math.Clamp(from.End - 6, Math.Min(from.Start + 2, from.End), from.End);
                x = Math.Clamp(x, to.Start + 3, Math.Max(to.Start + 3, to.End - 3));
                var down = to.Y > from.Y;
                tip = new Point(x, down ? to.Y - to.Half - 1 : to.Y + to.Half + 1);
                direction = new Vector(0, down ? 1 : -1);
                scene.Add(new PathPrim { Points = [new Point(x, down ? from.Y + from.Half : from.Y - from.Half), tip], Stroke = ChartBrush.MutedText, Thickness = 1.25 });
            }
            var normal = new Vector(-direction.Y, direction.X);
            scene.Add(new PathPrim
            {
                Points = [tip, tip - direction * 6 + normal * 3.5, tip - direction * 6 - normal * 3.5],
                Closed = true, Fill = ChartBrush.MutedText, Stroke = ChartPaint.None,
            });
        }

        if (today is { } now && now >= start && now <= end)
        {
            var x = X(now);
            scene.Add(new PathPrim { Points = [new Point(x, plot.Top - 2), new Point(x, plot.Bottom)], Stroke = ChartBrush.Negative, Thickness = 1.5, Dashed = true });
        }
        return scene;
    }

    // ---- bullet ----

    public static ChartScene Bullet(ChartLayoutContext ctx, IReadOnlyList<BulletItem>? items, bool horizontalLabels = true)
    {
        var scene = new ChartScene();
        var style = ctx.Style;
        if (items is null || items.Count == 0)
        {
            ChartFrame.Empty(ctx, scene, new Rect(ctx.Size));
            return scene;
        }

        var area = ChartFrame.Begin(ctx, scene, []);
        var line = style.SmallLine;
        var labelWidth = Math.Min(items.Max(i => style.Measure(i.Label, bold: true, small: true).Width), area.Width * 0.3);
        var rowHeight = area.Height / items.Count;
        var showTicks = rowHeight >= line * 3.2;
        var p = ctx.Progress;

        for (var r = 0; r < items.Count; r++)
        {
            var bullet = items[r];
            var top = area.Y + r * rowHeight;
            var lastTick = showTicks ? style.Measure("0,000", small: true).Width / 2 : 0;
            var barArea = new Rect(area.X + labelWidth + 12, top + 4, Math.Max(1, area.Width - labelWidth - 12 - Math.Max(8, lastTick)), Math.Max(1, rowHeight - 8 - (showTicks ? line + 4 : 0)));
            var barHeight = Math.Min(barArea.Height, 40);
            var band = new Rect(barArea.X, barArea.Y + (barArea.Height - barHeight) / 2, barArea.Width, barHeight);
            var ranges = bullet.Ranges.Where(double.IsFinite).OrderBy(v => v).ToList();
            var max = double.IsFinite(bullet.Maximum) && bullet.Maximum > 0
                ? bullet.Maximum
                : new[] { bullet.Value, bullet.Target }.Concat(ranges).Where(double.IsFinite).DefaultIfEmpty(1).Max();
            var scale = ChartAxis.Nice(0, max, ChartAxis.TickCount(band.Width, style.Measure("00000", small: true).Width + 12));
            double X(double v) => scale.Map(Math.Clamp(v, scale.Min, scale.Max), band.Left, band.Right);

            var label = style.Fit(bullet.Label, labelWidth + 1, bold: true, small: true);
            var labelSize = style.Measure(label, bold: true, small: true);
            scene.Add(style.Text(label, new Point(area.X + labelWidth - labelSize.Width, band.Center.Y - labelSize.Height / 2), bold: true, small: true));

            // Qualitative ranges: the lowest is the darkest.
            scene.Add(new RectPrim { Rect = band, Radius = 2, Fill = ChartBrush.Track });
            for (var k = ranges.Count - 1; k >= 0; k--)
                scene.Add(new RectPrim { Rect = band.WithWidth(Math.Max(0, X(ranges[k]) - band.Left)), Radius = 2, Fill = ChartBrush.MutedText, Opacity = 0.1 + 0.1 * (ranges.Count - 1 - k) });

            var paint = ChartPaint.Series(0);
            var value = new Rect(band.Left, band.Center.Y - band.Height / 6, Math.Max(0, X(bullet.Value * p) - band.Left), band.Height / 3);
            var item = scene.AddItem(new ChartHitItem(bullet, $"{style.Format(bullet.Value)} / {style.Format(bullet.Target)}")
            {
                TooltipTitle = bullet.Label, Paint = paint, Bounds = band, CornerRadius = 2,
            });
            scene.Add(new RectPrim { Rect = value, Radius = Math.Min(2, value.Height / 2), Fill = paint }, item);
            if (double.IsFinite(bullet.Target))
            {
                var x = X(bullet.Target);
                scene.Add(new RectPrim { Rect = new Rect(x - 1.5, band.Center.Y - band.Height * 0.35, 3, band.Height * 0.7), Radius = 1, Fill = ChartBrush.Text }, item);
            }

            if (showTicks)
                foreach (var tick in scale.Ticks)
                {
                    var text = style.Format(tick);
                    var size = style.Measure(text, small: true);
                    var x = X(tick);
                    scene.Add(new PathPrim { Points = [new Point(x, band.Bottom), new Point(x, band.Bottom + 3)], Stroke = ChartBrush.Axis });
                    scene.Add(style.Text(text, new Point(x - size.Width / 2, band.Bottom + 4), small: true, brush: ChartBrush.MutedText));
                }
        }
        return scene;
    }

    // ---- gauge ----

    /// <summary>
    /// Half circle scale with colored bands and a needle, or (without bands) a filled arc up to the value.
    /// <paramref name="shown"/> is where the needle (or arc) is during the animation; by default it grows from the minimum.
    /// </summary>
    public static ChartScene Gauge(ChartLayoutContext ctx, double value, double minimum, double maximum, IReadOnlyList<GaugeBand>? bands,
        string? label, string? valueText, double? shown = null)
    {
        var scene = new ChartScene();
        var style = ctx.Style;
        var area = ChartFrame.Begin(ctx, scene, []);
        if (!(maximum > minimum))
            maximum = minimum + 1;
        if (!double.IsFinite(value))
            value = minimum;

        var line = style.SmallLine;
        var hasBands = bands?.Any(b => double.IsFinite(b.From) && double.IsFinite(b.To) && b.To > b.From) == true;
        var big = valueText ?? style.Format(value);
        var valueSize = Math.Round(style.FontSize * 2);
        var bigHeight = style.Measure(big, bold: true, fontSize: valueSize).Height;
        // With a needle the value goes under the hub, otherwise into the arc; the ends of the scale under the arc.
        var below = line + 6 + (hasBands ? bigHeight + 4 + (string.IsNullOrEmpty(label) ? 0 : line) : 0);
        var radius = Math.Max(10, Math.Min(area.Width / 2 - 4, area.Height - below - 4));
        var thickness = Math.Max(6, radius * 0.2);
        var center = new Point(area.Center.X, area.Y + (area.Height - below + radius) / 2);
        double Angle(double v) => -90 + (Math.Clamp(v, minimum, maximum) - minimum) / (maximum - minimum) * 180;
        var needle = shown ?? minimum + (value - minimum) * ctx.Progress;
        var inner = radius - thickness;

        var validBands = bands?.Where(b => double.IsFinite(b.From) && double.IsFinite(b.To) && b.To > b.From).ToList() ?? [];
        if (validBands.Count == 0)
        {
            scene.Add(new ArcPrim { Center = center, Radius = radius, InnerRadius = inner, StartAngle = -90, SweepAngle = 180, Fill = ChartBrush.Track, Stroke = ChartPaint.None });
            var item = scene.AddItem(new ChartHitItem(value, style.Format(value))
            {
                TooltipTitle = label, Paint = ChartBrush.Series0, Shape = ChartHitShape.Arc, Center = center, Radius = radius,
                InnerRadius = inner, StartAngle = -90, SweepAngle = 180, Bounds = new Rect(center.X - radius, center.Y - radius, radius * 2, radius),
            });
            scene.Add(new ArcPrim { Center = center, Radius = radius, InnerRadius = inner, StartAngle = -90, SweepAngle = Angle(needle) + 90, Fill = ChartBrush.Series0, Stroke = ChartPaint.None }, item);
        }
        else
        {
            scene.Add(new ArcPrim { Center = center, Radius = radius, InnerRadius = inner, StartAngle = -90, SweepAngle = 180, Fill = ChartBrush.Track, Stroke = ChartPaint.None });
            for (var i = 0; i < validBands.Count; i++)
            {
                var band = validBands[i];
                var from = Angle(band.From);
                var sweep = Angle(band.To) - from;
                var paint = ChartPaint.Series(i + 1, band.Color);
                var text = $"{style.Format(band.From)} – {style.Format(band.To)}";
                var item = scene.AddItem(new ChartHitItem(band, text)
                {
                    TooltipTitle = band.Label, Paint = paint, Shape = ChartHitShape.Arc, Center = center, Radius = radius,
                    InnerRadius = inner, StartAngle = from, SweepAngle = sweep, Bounds = new Rect(center.X - radius, center.Y - radius, radius * 2, radius),
                });
                scene.Add(new ArcPrim { Center = center, Radius = radius, InnerRadius = inner, StartAngle = from, SweepAngle = sweep, Fill = paint, Thickness = 1.5 }, item);
            }

            // Needle: a thin triangle from the hub to just inside the scale.
            var a = (Angle(needle) - 90) * Math.PI / 180;
            var dir = new Vector(Math.Cos(a), Math.Sin(a));
            var normal = new Vector(-dir.Y, dir.X);
            var hub = Math.Max(4, thickness * 0.35);
            var tip = center + dir * (radius - thickness * 0.3);
            scene.Add(new PathPrim { Points = [tip, center + normal * hub * 0.6, center - normal * hub * 0.6], Closed = true, Fill = ChartBrush.Text, Stroke = ChartPaint.None });
            scene.Add(new CirclePrim { Center = center, Radius = hub, Fill = ChartBrush.Text });
        }

        var inside = inner * 1.6;
        var bigWidth = style.Measure(big, bold: true, fontSize: valueSize).Width;
        var fontSize = !hasBands && bigWidth > inside ? Math.Max(style.FontSize, valueSize * inside / bigWidth) : valueSize;
        bigHeight = style.Measure(big, bold: true, fontSize: fontSize).Height;
        var hubRadius = Math.Max(4, thickness * 0.35);
        var scaleY = center.Y + 4 + line / 2;
        if (hasBands)
        {
            var valueY = center.Y + hubRadius + 4 + bigHeight / 2;
            scene.Add(style.CenteredText(big, new Point(center.X, valueY), bold: true, fontSize: fontSize));
            if (!string.IsNullOrEmpty(label))
                scene.Add(style.CenteredText(style.Fit(label, radius * 1.6, small: true), new Point(center.X, valueY + bigHeight / 2 + line / 2),
                    small: true, brush: ChartBrush.MutedText));
        }
        else
        {
            var labelHeight = string.IsNullOrEmpty(label) ? 0 : line;
            scene.Add(style.CenteredText(big, new Point(center.X, center.Y - labelHeight - bigHeight / 2), bold: true, fontSize: fontSize));
            if (labelHeight > 0)
                scene.Add(style.CenteredText(style.Fit(label!, inside, small: true), new Point(center.X, center.Y - line / 2), small: true, brush: ChartBrush.MutedText));
        }
        scene.Add(style.CenteredText(style.Format(minimum), new Point(center.X - radius + thickness / 2, scaleY), small: true, brush: ChartBrush.MutedText));
        scene.Add(style.CenteredText(style.Format(maximum), new Point(center.X + radius - thickness / 2, scaleY), small: true, brush: ChartBrush.MutedText));
        return scene;
    }
}
