using Avalonia;

namespace KD.Avalonia.Rice.Controls.Charts;

internal enum BarMode
{
    /// <summary>The first series only.</summary>
    Single,
    /// <summary>The series side by side.</summary>
    Clustered,
    /// <summary>The series on top of each other; negative values stack below zero.</summary>
    Stacked,
    /// <summary>Stacked, each category scaled to 100 %.</summary>
    Percent,
    /// <summary>Stacked from a central zero line, negative to the left, with a symmetric scale.</summary>
    Diverging,
}

/// <summary>Layouts of the category charts: column / bar (single, clustered, stacked, 100 %, diverging), line, area, radar.</summary>
internal static class CategoryLayouts
{
    private static double Value(ChartSeries series, int index)
    {
        if (index >= series.Values.Count)
            return 0;
        var value = series.Values[index];
        return double.IsFinite(value) ? value : 0;
    }

    private static IReadOnlyList<(string, ChartPaint)> Legend(ChartCategoryData data) =>
        data.Series.Select((s, i) => (s.Name, ChartPaint.Series(i, s.Color))).ToList();

    private static List<int> Visible(ChartLayoutContext ctx, ChartCategoryData data) =>
        Enumerable.Range(0, data.Series.Count).Where(i => !ctx.IsHidden(i)).ToList();

    // ---- bars ----

    public static ChartScene Bars(ChartLayoutContext ctx, ChartCategoryData? data, BarMode mode, bool horizontal, bool showValues)
    {
        var scene = new ChartScene();
        var style = ctx.Style;
        if (data is null || data.Categories.Count == 0 || data.Series.Count == 0)
        {
            ChartFrame.Empty(ctx, scene, new Rect(ctx.Size));
            return scene;
        }

        var area = ChartFrame.Begin(ctx, scene, mode == BarMode.Single ? [] : Legend(data));
        var categories = data.Categories;
        var series = mode == BarMode.Single ? new List<int> { 0 } : Visible(ctx, data);
        var count = categories.Count;

        // The value of each (series, category) as drawn: shares for 100 %.
        var values = series.ToDictionary(s => s, s => Enumerable.Range(0, count).Select(i => Value(data.Series[s], i)).ToArray());
        if (mode == BarMode.Percent)
            for (var i = 0; i < count; i++)
            {
                var total = series.Sum(s => Math.Abs(values[s][i]));
                foreach (var s in series)
                    values[s][i] = total > 0 ? values[s][i] / total * 100 : 0;
            }

        double min = 0, max = 0;
        var stacked = mode is BarMode.Stacked or BarMode.Percent or BarMode.Diverging;
        for (var i = 0; i < count; i++)
        {
            if (stacked)
            {
                max = Math.Max(max, series.Sum(s => Math.Max(0, values[s][i])));
                min = Math.Min(min, series.Sum(s => Math.Min(0, values[s][i])));
            }
            else
                foreach (var s in series)
                {
                    max = Math.Max(max, values[s][i]);
                    min = Math.Min(min, values[s][i]);
                }
        }
        if (mode == BarMode.Percent)
            (min, max) = (min < 0 ? -100 : 0, max > 0 ? 100 : 0);
        if (mode == BarMode.Diverging)
            (min, max) = (-Math.Max(-min, max), Math.Max(-min, max));

        Func<double, string> axisFormat = mode switch
        {
            BarMode.Percent => v => style.Format(v, "0'%'"),
            BarMode.Diverging => v => style.Format(Math.Abs(v)),
            _ => v => style.Format(v),
        };
        // Room for the value labels beyond the bar ends.
        var valueRoom = 0.0;
        if (showValues && !stacked)
            valueRoom = horizontal
                ? series.SelectMany(s => Enumerable.Range(0, count).Select(i => style.Measure(style.Format(Value(data.Series[s], i)), small: true).Width)).DefaultIfEmpty(0).Max() + 6
                : style.SmallLine + 4;
        var frame = CartesianFrame.Category(scene, style, area, categories, min, max, horizontal, axisFormat, valueRoom: valueRoom);
        var p = ctx.Progress;
        var groupSize = frame.Band * (stacked || mode == BarMode.Single ? 0.62 : 0.78);
        var barSize = mode == BarMode.Clustered && series.Count > 0 ? groupSize / series.Count : groupSize;
        var gap = mode == BarMode.Clustered && series.Count > 1 ? Math.Min(3, barSize * 0.15) : 0;
        var offset = (frame.Band - groupSize) / 2;

        var spans = new (double From, double To)[series.Count];
        for (var i = 0; i < count; i++)
        {
            // Stacks grow outward from zero, positive and negative separately. Diverging bars stack the negative
            // series in reverse, so the first one ("strongly disagree") ends up outermost.
            double positive = 0, negative = 0;
            for (var k = 0; k < series.Count; k++)
            {
                var value = values[series[k]][i] * p;
                if (!stacked)
                    spans[k] = (0, value);
                else if (value >= 0)
                    spans[k] = (positive, positive += value);
            }
            if (stacked)
                foreach (var k in mode == BarMode.Diverging ? Enumerable.Range(0, series.Count).Reverse() : Enumerable.Range(0, series.Count))
                    if (values[series[k]][i] * p is var value and < 0)
                        spans[k] = (negative, negative += value);

            for (var k = 0; k < series.Count; k++)
            {
                var s = series[k];
                var raw = Value(data.Series[s], i);
                var (from, to) = spans[k];

                var thickness = Math.Max(1, barSize - gap);
                var rect = frame.BarRect(i, offset + (mode == BarMode.Clustered ? k * barSize + gap / 2 : 0), thickness, from, to);
                // Stacked parts get a hairline gap between them.
                if (stacked && rect.Width > 2 && rect.Height > 2)
                    rect = horizontal ? rect.Deflate(new Thickness(0.5, 0)) : rect.Deflate(new Thickness(0, 0.5));
                var paint = ChartPaint.Series(s, data.Series[s].Color);
                var point = new ChartDataPoint(data.Series[s], i, categories[i], raw);
                var tooltip = mode == BarMode.Percent
                    ? $"{categories[i]}: {style.Format(raw)} ({style.Format(values[s][i], "0.#'%'")})"
                    : $"{categories[i]}: {style.Format(raw)}";
                var hit = horizontal
                    ? new Rect(rect.X, frame.BandStart(i) + offset + (mode == BarMode.Clustered ? k * barSize : 0), Math.Max(rect.Width, 4), barSize)
                    : new Rect(frame.BandStart(i) + offset + (mode == BarMode.Clustered ? k * barSize : 0), rect.Y, barSize, Math.Max(rect.Height, 4));
                var item = scene.AddItem(new ChartHitItem(point, tooltip)
                {
                    TooltipTitle = mode == BarMode.Single ? null : data.Series[s].Name, Paint = paint, Bounds = hit, CornerRadius = 3,
                });
                scene.Add(new RectPrim { Rect = rect, Radius = Math.Min(3, Math.Min(rect.Width, rect.Height) / 2), Fill = paint }, item);

                if (showValues && p >= 1 && raw != 0)
                    AddValueLabel(scene, style, frame, rect, raw, mode, values[s][i], paint, item);
            }
        }
        return scene;
    }

    private static void AddValueLabel(ChartScene scene, ChartStyle style, CartesianFrame frame, Rect rect, double raw, BarMode mode,
        double shown, ChartPaint paint, int item)
    {
        var text = mode == BarMode.Percent ? style.Format(shown, "0'%'") : style.Format(raw);
        var size = style.Measure(text, small: true);
        if (mode is BarMode.Stacked or BarMode.Percent or BarMode.Diverging)
        {
            // Inside the part, when it fits.
            if (size.Width + 4 <= rect.Width && size.Height + 2 <= rect.Height)
                scene.Add(style.CenteredText(text, rect.Center, small: true, on: paint), item);
            return;
        }
        var negative = raw < 0;
        var at = frame.Horizontal
            ? new Point(negative ? rect.Left - 4 - size.Width : rect.Right + 4, rect.Center.Y - size.Height / 2)
            : new Point(rect.Center.X - size.Width / 2, negative ? rect.Bottom + 2 : rect.Top - 2 - size.Height);
        if (size.Width <= frame.Band * 1.2 || frame.Horizontal)
            scene.Add(style.Text(text, at, small: true, brush: ChartBrush.MutedText), item);
    }

    // ---- line and area ----

    public static ChartScene Lines(ChartLayoutContext ctx, ChartCategoryData? data, bool fill, bool stacked, bool smooth, bool markers)
    {
        var scene = new ChartScene();
        var style = ctx.Style;
        if (data is null || data.Categories.Count == 0 || data.Series.Count == 0)
        {
            ChartFrame.Empty(ctx, scene, new Rect(ctx.Size));
            return scene;
        }

        var area = ChartFrame.Begin(ctx, scene, Legend(data));
        var categories = data.Categories;
        var series = Visible(ctx, data);
        var count = categories.Count;
        var tops = new Dictionary<int, double[]>();
        var bottoms = new Dictionary<int, double[]>();
        var running = new double[count];
        foreach (var s in series)
        {
            var bottom = (double[])running.Clone();
            var top = Enumerable.Range(0, count).Select(i => (stacked ? running[i] : 0) + Value(data.Series[s], i)).ToArray();
            if (stacked)
                running = (double[])top.Clone();
            bottoms[s] = stacked ? bottom : new double[count];
            tops[s] = top;
        }
        var all = tops.Values.SelectMany(v => v).Concat(bottoms.Values.SelectMany(v => v)).DefaultIfEmpty(0).ToList();
        var frame = CartesianFrame.Category(scene, style, area, categories, all.Min(), all.Max(), false, includeZero: fill || all.Min() >= 0);

        var p = ctx.Progress;
        var baseline = Math.Clamp(0, frame.Scale.Min, frame.Scale.Max);
        Point At(int i, double value) => new(frame.BandCenter(i), frame.Value(baseline + (value - baseline) * p));

        // Areas first (bottom series last on top would hide the lines), then lines, then markers.
        if (fill)
            foreach (var s in series.AsEnumerable().Reverse())
            {
                var top = Enumerable.Range(0, count).Select(i => At(i, tops[s][i])).ToList();
                var bottom = Enumerable.Range(0, count).Select(i => At(i, bottoms[s][i])).Reverse().ToList();
                scene.Add(new PathPrim
                {
                    Points = [.. top, .. bottom], Closed = true, SmoothCount = smooth ? count : 0,
                    Fill = ChartPaint.Series(s, data.Series[s].Color), Stroke = ChartPaint.None, Opacity = stacked ? 0.55 : 0.22,
                });
            }
        foreach (var s in series)
        {
            var points = Enumerable.Range(0, count).Select(i => At(i, tops[s][i])).ToList();
            if (count > 1)
                scene.Add(new PathPrim { Points = points, SmoothCount = smooth ? count : 0, Stroke = ChartPaint.Series(s, data.Series[s].Color), Thickness = 2.25 });
        }
        foreach (var s in series)
        {
            var paint = ChartPaint.Series(s, data.Series[s].Color);
            for (var i = 0; i < count; i++)
            {
                var point = At(i, tops[s][i]);
                var raw = Value(data.Series[s], i);
                var item = scene.AddItem(new ChartHitItem(new ChartDataPoint(data.Series[s], i, categories[i], raw), $"{categories[i]}: {style.Format(raw)}")
                {
                    TooltipTitle = data.Series[s].Name, Paint = paint, Shape = ChartHitShape.Circle, Center = point,
                    Radius = Math.Max(6, Math.Min(12, frame.Band / 2)), Bounds = new Rect(point, point).Inflate(6),
                });
                if (markers || count == 1)
                    scene.Add(new CirclePrim { Center = point, Radius = 3.5, Fill = paint, Stroke = ChartBrush.Separator }, item);
            }
        }
        return scene;
    }

    // ---- radar ----

    public static ChartScene Radar(ChartLayoutContext ctx, ChartCategoryData? data, bool fill)
    {
        var scene = new ChartScene();
        var style = ctx.Style;
        if (data is null || data.Categories.Count < 3 || data.Series.Count == 0)
        {
            ChartFrame.Empty(ctx, scene, new Rect(ctx.Size));
            return scene;
        }

        var area = ChartFrame.Begin(ctx, scene, Legend(data));
        var categories = data.Categories;
        var count = categories.Count;
        var series = Visible(ctx, data);
        var max = series.SelectMany(s => Enumerable.Range(0, count).Select(i => Value(data.Series[s], i))).DefaultIfEmpty(0).Max();
        var line = style.SmallLine;
        var labelWidth = categories.Select(c => style.Measure(c, small: true).Width).Max();
        var radius = Math.Max(10, Math.Min(area.Width / 2 - Math.Min(labelWidth, area.Width * 0.2) - 8, area.Height / 2 - line - 6));
        var center = area.Center;
        var scale = ChartAxis.Nice(0, max, ChartAxis.TickCount(radius, line * 2.2));

        Point At(double i, double r)
        {
            var angle = (i * 360.0 / count - 90) * Math.PI / 180;
            return new Point(center.X + r * Math.Cos(angle), center.Y + r * Math.Sin(angle));
        }

        // Web: a polygon per tick, a spoke per category.
        foreach (var tick in scale.Ticks.Where(t => t > 0))
        {
            var r = scale.Map(tick, 0, radius);
            scene.Add(new PathPrim { Points = Enumerable.Range(0, count).Select(i => At(i, r)).ToList(), Closed = true, Stroke = ChartBrush.Grid });
            // Between the first two spokes, away from the category labels.
            var anchor = At(0.5, r * Math.Cos(Math.PI / count));
            scene.Add(style.Text(style.Format(tick), new Point(anchor.X + 3, anchor.Y - line + 2), small: true, brush: ChartBrush.MutedText));
        }
        for (var i = 0; i < count; i++)
        {
            scene.Add(new PathPrim { Points = [center, At(i, radius)], Stroke = ChartBrush.Grid });
            var anchor = At(i, radius + 8);
            var text = style.Fit(categories[i], Math.Max(40, area.Width / 2 - radius - 12), small: true);
            var size = style.Measure(text, small: true);
            var dx = anchor.X - center.X;
            var x = Math.Abs(dx) < 1 ? anchor.X - size.Width / 2 : dx > 0 ? anchor.X : anchor.X - size.Width;
            var dy = anchor.Y - center.Y;
            var y = Math.Abs(dy) < radius * 0.2 ? anchor.Y - size.Height / 2 : dy > 0 ? anchor.Y : anchor.Y - size.Height;
            scene.Add(style.Text(text, new Point(x, y), small: true, brush: ChartBrush.MutedText));
        }

        var p = ctx.Progress;
        foreach (var s in series)
        {
            var paint = ChartPaint.Series(s, data.Series[s].Color);
            var points = Enumerable.Range(0, count).Select(i => At(i, scale.Map(Math.Max(0, Value(data.Series[s], i)), 0, radius) * p)).ToList();
            scene.Add(new PathPrim { Points = points, Closed = true, Fill = fill ? paint : ChartPaint.None, Stroke = ChartPaint.None, Opacity = 0.2 });
            scene.Add(new PathPrim { Points = points, Closed = true, Stroke = paint, Thickness = 2 });
            for (var i = 0; i < count; i++)
            {
                var raw = Value(data.Series[s], i);
                var item = scene.AddItem(new ChartHitItem(new ChartDataPoint(data.Series[s], i, categories[i], raw), $"{categories[i]}: {style.Format(raw)}")
                {
                    TooltipTitle = data.Series[s].Name, Paint = paint, Shape = ChartHitShape.Circle, Center = points[i], Radius = 7,
                    Bounds = new Rect(points[i], points[i]).Inflate(7),
                });
                scene.Add(new CirclePrim { Center = points[i], Radius = 3, Fill = paint, Stroke = ChartBrush.Separator }, item);
            }
        }
        return scene;
    }
}
