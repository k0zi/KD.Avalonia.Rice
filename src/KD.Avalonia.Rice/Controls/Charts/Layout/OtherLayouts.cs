using System.Globalization;
using Avalonia;

namespace KD.Avalonia.Rice.Controls.Charts;

/// <summary>Layouts of scatter, bubble, heatmap, waterfall and histogram charts.</summary>
internal static class XYLayouts
{
    // ---- scatter and bubble ----

    public static ChartScene Points(ChartLayoutContext ctx, IReadOnlyList<ChartPointSeries>? series, bool bubbles)
    {
        var scene = new ChartScene();
        var style = ctx.Style;
        var all = series?.SelectMany(s => s.Points).Where(Valid).ToList() ?? [];
        if (series is null || all.Count == 0)
        {
            ChartFrame.Empty(ctx, scene, new Rect(ctx.Size));
            return scene;
        }

        var area = ChartFrame.Begin(ctx, scene, series.Select((s, i) => (s.Name, ChartPaint.Series(i, s.Color))).ToList());
        var visible = Enumerable.Range(0, series.Count).Where(i => !ctx.IsHidden(i)).ToList();
        var points = visible.SelectMany(i => series[i].Points.Where(Valid)).DefaultIfEmpty(new ChartPoint()).ToList();
        double xMin = points.Min(pt => pt.X), xMax = points.Max(pt => pt.X), yMin = points.Min(pt => pt.Y), yMax = points.Max(pt => pt.Y);
        // Room around the points, so the markers (and bubbles) are not cut at the edges.
        var padX = (xMax - xMin) * (bubbles ? 0.08 : 0.04);
        var padY = (yMax - yMin) * (bubbles ? 0.1 : 0.05);
        var frame = CartesianFrame.Numeric(scene, style, area, xMin - padX, xMax + padX, yMin - padY, yMax + padY, yIncludeZero: false);
        var maxSize = points.Select(pt => Math.Abs(pt.Size)).DefaultIfEmpty(0).Max();
        var maxRadius = Math.Clamp(Math.Min(frame.Plot.Width, frame.Plot.Height) * 0.09, 8, 44);
        var p = ctx.Progress;

        foreach (var i in visible)
        {
            var paint = ChartPaint.Series(i, series[i].Color);
            foreach (var point in series[i].Points.Where(Valid))
            {
                var center = new Point(frame.X(point.X), frame.Value(point.Y));
                var radius = bubbles
                    ? Math.Max(3, maxSize > 0 ? Math.Sqrt(Math.Abs(point.Size) / maxSize) * maxRadius : 6)
                    : 4.5;
                var text = $"x: {style.Format(point.X)}, y: {style.Format(point.Y)}" + (bubbles ? $", {style.Format(point.Size)}" : "");
                var item = scene.AddItem(new ChartHitItem(point, point.Label is { Length: > 0 } label ? $"{label}\n{text}" : text)
                {
                    TooltipTitle = series[i].Name, Paint = paint, Shape = ChartHitShape.Circle, Center = center, Radius = Math.Max(radius, 6),
                    Bounds = new Rect(center, center).Inflate(Math.Max(radius, 6)),
                });
                scene.Add(new CirclePrim
                {
                    Center = center, Radius = radius * p, Fill = paint, Opacity = bubbles ? 0.6 : 0.85,
                    Stroke = bubbles ? paint : ChartPaint.None, Thickness = 1.25,
                }, item);
            }
        }
        return scene;
    }

    private static bool Valid(ChartPoint p) => double.IsFinite(p.X) && double.IsFinite(p.Y);

    // ---- heatmap ----

    public static ChartScene Heatmap(ChartLayoutContext ctx, HeatmapData? data, bool showValues)
    {
        var scene = new ChartScene();
        var style = ctx.Style;
        var columns = data is null ? 0 : Math.Max(data.ColumnLabels.Count, data.Rows.Select(r => r.Values.Count).DefaultIfEmpty(0).Max());
        if (data is null || data.Rows.Count == 0 || columns == 0)
        {
            ChartFrame.Empty(ctx, scene, new Rect(ctx.Size));
            return scene;
        }

        var area = ChartFrame.Begin(ctx, scene, []);
        var values = data.Rows.SelectMany(r => r.Values).Where(double.IsFinite).DefaultIfEmpty(0).ToList();
        double min = values.Min(), max = values.Max();
        var line = style.SmallLine;

        // Color scale under the grid.
        var scaleHeight = ctx.ShowLegend ? line + 14 : 0;
        if (ctx.ShowLegend)
        {
            const int steps = 12;
            var width = Math.Min(area.Width * 0.5, 240);
            var minText = style.Format(min);
            var maxText = style.Format(max);
            var minSize = style.Measure(minText, small: true);
            var x = area.Center.X - width / 2;
            var y = area.Bottom - line - 2;
            scene.Add(style.Text(minText, new Point(x - minSize.Width - 8, y), small: true, brush: ChartBrush.MutedText));
            scene.Add(style.Text(maxText, new Point(x + width + 8, y), small: true, brush: ChartBrush.MutedText));
            for (var i = 0; i < steps; i++)
            {
                var r = new Rect(x + i * width / steps, y + 2, width / steps + 0.5, line - 4);
                scene.Add(new RectPrim { Rect = r, Fill = ChartBrush.HeatLow });
                scene.Add(new RectPrim { Rect = r, Fill = ChartBrush.HeatHigh, Opacity = (i + 0.5) / steps });
            }
        }

        var rowLabelWidth = Math.Min(data.Rows.Max(r => style.Measure(r.Label, small: true).Width), area.Width * 0.3);
        string Column(int c) => c < data.ColumnLabels.Count ? data.ColumnLabels[c] : "";
        var grid = new Rect(area.X + rowLabelWidth + 8, area.Y + line + 6, Math.Max(1, area.Width - rowLabelWidth - 8),
            Math.Max(1, area.Height - line - 6 - scaleHeight));
        var cellWidth = grid.Width / columns;
        var cellHeight = grid.Height / data.Rows.Count;
        const double gap = 2;

        var every = Math.Max(1, (int)Math.Ceiling((Enumerable.Range(0, columns).Max(c => style.Measure(Column(c), small: true).Width) + 6) / cellWidth));
        for (var c = 0; c < columns; c += every)
        {
            var text = style.Fit(Column(c), cellWidth * every - 4, small: true);
            scene.Add(style.CenteredText(text, new Point(grid.X + (c + 0.5) * cellWidth, area.Y + line / 2), small: true, brush: ChartBrush.MutedText));
        }

        var p = ctx.Progress;
        for (var r = 0; r < data.Rows.Count; r++)
        {
            var row = data.Rows[r];
            var label = style.Fit(row.Label, rowLabelWidth + 1, small: true);
            var size = style.Measure(label, small: true);
            if (cellHeight >= line * 0.8)
                scene.Add(style.Text(label, new Point(grid.X - 8 - size.Width, grid.Y + (r + 0.5) * cellHeight - size.Height / 2), small: true, brush: ChartBrush.MutedText));
            for (var c = 0; c < columns; c++)
            {
                var value = c < row.Values.Count ? row.Values[c] : double.NaN;
                var cell = new Rect(grid.X + c * cellWidth + gap / 2, grid.Y + r * cellHeight + gap / 2, Math.Max(1, cellWidth - gap), Math.Max(1, cellHeight - gap));
                var radius = Math.Min(3, Math.Min(cell.Width, cell.Height) / 4);
                if (!double.IsFinite(value))
                {
                    scene.Add(new RectPrim { Rect = cell, Radius = radius, Fill = ChartBrush.Track });
                    continue;
                }
                var t = max > min ? (value - min) / (max - min) : 1;
                var model = new HeatmapCell(row, c, Column(c), value);
                var item = scene.AddItem(new ChartHitItem(model, $"{Column(c)}: {style.Format(value)}")
                {
                    TooltipTitle = row.Label, Paint = ChartBrush.HeatHigh, Bounds = cell, CornerRadius = radius,
                });
                scene.Add(new RectPrim { Rect = cell, Radius = radius, Fill = ChartBrush.HeatLow }, item);
                scene.Add(new RectPrim { Rect = cell, Radius = radius, Fill = ChartBrush.HeatHigh, Opacity = (0.04 + 0.96 * t) * p }, item);
                if (showValues && p >= 1)
                {
                    var text = style.Format(value);
                    var textSize = style.Measure(text, small: true);
                    if (textSize.Width + 4 <= cell.Width && textSize.Height <= cell.Height)
                        scene.Add(style.CenteredText(text, cell.Center, small: true,
                            on: t > 0.55 ? ChartBrush.HeatHigh : ChartBrush.HeatLow), item);
                }
            }
        }
        return scene;
    }

    // ---- waterfall ----

    public static ChartScene Waterfall(ChartLayoutContext ctx, IReadOnlyList<WaterfallStep>? steps, bool showValues)
    {
        var scene = new ChartScene();
        var style = ctx.Style;
        if (steps is null || steps.Count == 0)
        {
            ChartFrame.Empty(ctx, scene, new Rect(ctx.Size));
            return scene;
        }

        var area = ChartFrame.Begin(ctx, scene, []);
        var bars = Running(steps);
        var min = bars.Min(b => Math.Min(b.From, b.To));
        var max = bars.Max(b => Math.Max(b.From, b.To));
        var frame = CartesianFrame.Category(scene, style, area, steps.Select(s => s.Label).ToList(), min, max, false);
        var p = ctx.Progress;
        var width = frame.Band * 0.62;
        var offset = (frame.Band - width) / 2;

        for (var i = 0; i < steps.Count; i++)
        {
            var (from, to) = (bars[i].From * p, bars[i].To * p);
            var step = steps[i];
            ChartPaint paint = step.IsTotal ? ChartBrush.Series0 : to >= from ? ChartBrush.Positive : ChartBrush.Negative;
            var rect = frame.BarRect(i, offset, width, from, to);
            if (rect.Height < 1)
                rect = new Rect(rect.X, rect.Y - 0.5, rect.Width, 1);
            var text = step.IsTotal ? style.Format(bars[i].To) : $"{(step.Value >= 0 ? "+" : "")}{style.Format(step.Value)} → {style.Format(bars[i].To)}";
            var item = scene.AddItem(new ChartHitItem(step, text) { TooltipTitle = step.Label, Paint = paint, Bounds = rect.Inflate(new Thickness(0, 2)), CornerRadius = 3 });
            scene.Add(new RectPrim { Rect = rect, Radius = Math.Min(3, Math.Min(rect.Width, rect.Height) / 2), Fill = paint }, item);

            // Dashed connector from the end of this bar to the next one.
            if (i + 1 < steps.Count)
            {
                var y = frame.Value(to);
                scene.Add(new PathPrim { Points = [new Point(rect.Right, y), new Point(frame.BandStart(i + 1) + offset, y)], Stroke = ChartBrush.Axis, Dashed = true });
            }
            if (showValues && p >= 1)
            {
                var label = step.IsTotal ? style.Format(bars[i].To) : (step.Value >= 0 ? "+" : "") + style.Format(step.Value);
                var size = style.Measure(label, small: true);
                var below = !step.IsTotal && step.Value < 0;
                if (size.Width <= frame.Band * 1.1)
                    scene.Add(style.Text(label, new Point(rect.Center.X - size.Width / 2, below ? rect.Bottom + 2 : rect.Top - 2 - size.Height),
                        small: true, brush: ChartBrush.MutedText), item);
            }
        }
        return scene;
    }

    /// <summary>Start and end of each waterfall bar: a change starts at the running total, a total starts at zero.</summary>
    public static List<(double From, double To)> Running(IReadOnlyList<WaterfallStep> steps)
    {
        var result = new List<(double, double)>(steps.Count);
        var total = 0.0;
        foreach (var step in steps)
        {
            var value = double.IsFinite(step.Value) ? step.Value : 0;
            if (step.IsTotal)
                result.Add((0, total));
            else
            {
                result.Add((total, total + value));
                total += value;
            }
        }
        return result;
    }

    // ---- histogram ----

    public static ChartScene Histogram(ChartLayoutContext ctx, IReadOnlyList<double>? values, int binCount, double binWidth, bool showValues)
    {
        var scene = new ChartScene();
        var style = ctx.Style;
        var bins = values is null ? [] : HistogramBinner.Bin(values, binCount, binWidth);
        if (bins.Count == 0)
        {
            ChartFrame.Empty(ctx, scene, new Rect(ctx.Size));
            return scene;
        }

        var area = ChartFrame.Begin(ctx, scene, []);
        var start = bins[0].From;
        var end = bins[^1].To;
        var step = bins[0].To - bins[0].From;
        // Ticks on bin edges, every n-th edge so the labels fit.
        var labelWidth = Math.Max(style.Measure(style.Format(start), small: true).Width, style.Measure(style.Format(end), small: true).Width) + 12;
        var every = Math.Max(1, (int)Math.Ceiling(labelWidth / Math.Max(1, area.Width / bins.Count)));
        var xScale = new ChartScale(start, start + Math.Ceiling(bins.Count / (double)every) * every * step, step * every);
        var frame = CartesianFrame.Numeric(scene, style, area, start, end, 0, bins.Max(b => b.Count), xScale: xScale,
            yFormat: v => Math.Abs(v - Math.Round(v)) < 1e-9 ? style.Format(v, "0") : "");
        var p = ctx.Progress;
        foreach (var bin in bins)
        {
            var left = frame.X(bin.From) + 0.75;
            var right = frame.X(bin.To) - 0.75;
            var top = frame.Value(bin.Count * p);
            var rect = new Rect(left, top, Math.Max(1, right - left), Math.Max(0, frame.Baseline - top));
            var item = scene.AddItem(new ChartHitItem(bin, $"{bin.Count}")
            {
                TooltipTitle = $"{style.Format(bin.From)} – {style.Format(bin.To)}", Paint = ChartBrush.Series0,
                Bounds = new Rect(left, frame.Plot.Top, rect.Width, frame.Plot.Height), CornerRadius = 2,
            });
            scene.Add(new RectPrim { Rect = rect, Radius = Math.Min(2, rect.Width / 2), Fill = ChartBrush.Series0 }, item);
            if (showValues && p >= 1 && bin.Count > 0)
            {
                var text = bin.Count.ToString(CultureInfo.CurrentCulture);
                var size = style.Measure(text, small: true);
                if (size.Width <= rect.Width + 4)
                    scene.Add(style.Text(text, new Point(rect.Center.X - size.Width / 2, rect.Top - 2 - size.Height), small: true, brush: ChartBrush.MutedText), item);
            }
        }
        return scene;
    }
}

/// <summary>Sorts values into equal, round-width bins.</summary>
internal static class HistogramBinner
{
    /// <summary>
    /// Bins of <paramref name="binWidth"/>, or (NaN or ≤ 0) of a round width giving about <paramref name="binCount"/> bins
    /// (0 picks the count by Sturges' rule).
    /// </summary>
    public static List<HistogramBin> Bin(IEnumerable<double> values, int binCount = 0, double binWidth = double.NaN)
    {
        var data = values.Where(double.IsFinite).ToList();
        if (data.Count == 0)
            return [];
        var min = data.Min();
        var max = data.Max();
        double width;
        if (double.IsFinite(binWidth) && binWidth > 0)
            width = binWidth;
        else
        {
            var count = binCount > 0 ? binCount : (int)Math.Ceiling(Math.Log2(data.Count) + 1);
            width = max > min ? ChartAxis.NiceStep((max - min) / count) : Math.Abs(min) > 0 ? ChartAxis.NiceStep(Math.Abs(min) * 0.1) : 1;
        }

        var start = Math.Floor(min / width) * width;
        var bins = Math.Max(1, (int)Math.Ceiling((max - start) / width - 1e-9));
        // The maximum falls on the end of the last bin: it is counted in it (bins are [from, to), the last [from, to]).
        if (start + bins * width <= max - width * 1e-9)
            bins++;
        bins = Math.Min(bins, 1000);
        var counts = new int[bins];
        foreach (var value in data)
            counts[Math.Clamp((int)Math.Floor((value - start) / width + 1e-9), 0, bins - 1)]++;
        return Enumerable.Range(0, bins)
            .Select(i => new HistogramBin(Math.Round(start + i * width, 10), Math.Round(start + (i + 1) * width, 10), counts[i]))
            .ToList();
    }
}
