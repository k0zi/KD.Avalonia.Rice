using Avalonia;

namespace KD.Avalonia.Rice.Controls.Charts;

/// <summary>Everything a chart layout gets besides its data.</summary>
internal sealed class ChartLayoutContext(Size size, ChartStyle style)
{
    public Size Size { get; } = size;

    public ChartStyle Style { get; } = style;

    /// <summary>Load animation, 0 (nothing grown yet) to 1 (final); already eased.</summary>
    public double Progress { get; init; } = 1;

    /// <summary>Series (or slices) switched off in the legend.</summary>
    public IReadOnlySet<int> Hidden { get; init; } = new HashSet<int>();

    public string? Title { get; init; }

    public bool ShowLegend { get; init; } = true;

    public ChartLegendPosition LegendPosition { get; init; } = ChartLegendPosition.Bottom;

    public bool IsHidden(int index) => Hidden.Contains(index);
}

/// <summary>Title and legend around the plot area.</summary>
internal static class ChartFrame
{
    public const double Padding = 4;
    private const double Swatch = 10;

    /// <summary>
    /// Draws the title and the legend (when shown and there is more than one entry, or <paramref name="alwaysLegend"/>)
    /// and returns the room left for the plot. <paramref name="plotWidth"/> gives the width a plot really uses in a
    /// given room (e.g. a pie: its diameter); a side legend then stays next to it, the two centered together.
    /// </summary>
    public static Rect Begin(ChartLayoutContext ctx, ChartScene scene, IReadOnlyList<(string Label, ChartPaint Paint)> legend,
        bool alwaysLegend = false, Func<Rect, double>? plotWidth = null)
    {
        var style = ctx.Style;
        var area = new Rect(ctx.Size).Deflate(Padding);
        if (!string.IsNullOrWhiteSpace(ctx.Title))
        {
            var title = style.Fit(ctx.Title, area.Width, bold: true);
            var text = style.Text(title, default, bold: true, fontSize: style.TitleFontSize);
            scene.Add(style.Text(title, new Point(area.Center.X - text.Size.Width / 2, area.Y), bold: true, fontSize: style.TitleFontSize));
            area = new Rect(area.X, area.Y + text.Size.Height + 8, area.Width, Math.Max(0, area.Height - text.Size.Height - 8));
        }

        if (!ctx.ShowLegend || legend.Count == 0 || legend.Count == 1 && !alwaysLegend)
            return area;

        var line = style.SmallLine;
        var rowHeight = Math.Max(line, Swatch) + 6;
        var entries = legend.Select((e, i) => (e.Label, e.Paint, Index: i, Width: Swatch + 6 + style.Measure(e.Label, small: true).Width)).ToList();
        switch (ctx.LegendPosition)
        {
            case ChartLegendPosition.Right or ChartLegendPosition.Left:
            {
                const double gap = 16;
                var width = Math.Min(entries.Max(e => e.Width), area.Width * 0.35);
                var height = entries.Count * rowHeight;
                var room = Math.Max(0, area.Width - width - gap);
                var used = plotWidth is null ? room : Math.Clamp(plotWidth(new Rect(area.X, area.Y, room, area.Height)), 0, room);
                var left = area.X + (room - used) / 2;
                var right = ctx.LegendPosition == ChartLegendPosition.Right;
                var x = right ? left + used + gap : left;
                var y = area.Y + Math.Max(0, (area.Height - height) / 2);
                foreach (var e in entries)
                {
                    AddEntry(ctx, scene, e.Label, e.Paint, e.Index, new Point(x, y), width, rowHeight);
                    y += rowHeight;
                }
                return new Rect(right ? left : left + width + gap, area.Y, used, area.Height);
            }
            default:
            {
                // Rows of entries, centered, wrapped at the width.
                const double gap = 16;
                var rows = new List<List<(string Label, ChartPaint Paint, int Index, double Width)>> { new() };
                var rowWidth = 0.0;
                foreach (var e in entries)
                {
                    var width = Math.Min(e.Width, area.Width);
                    if (rows[^1].Count > 0 && rowWidth + gap + width > area.Width)
                    {
                        rows.Add([]);
                        rowWidth = 0;
                    }
                    rowWidth += (rows[^1].Count > 0 ? gap : 0) + width;
                    rows[^1].Add(e with { Width = width });
                }
                var height = rows.Count * rowHeight;
                var y = ctx.LegendPosition == ChartLegendPosition.Top ? area.Y : area.Bottom - height;
                foreach (var row in rows)
                {
                    var total = row.Sum(e => e.Width) + gap * (row.Count - 1);
                    var x = area.Center.X - total / 2;
                    foreach (var e in row)
                    {
                        AddEntry(ctx, scene, e.Label, e.Paint, e.Index, new Point(x, y), e.Width, rowHeight);
                        x += e.Width + gap;
                    }
                    y += rowHeight;
                }
                return ctx.LegendPosition == ChartLegendPosition.Top
                    ? new Rect(area.X, area.Y + height + 8, area.Width, Math.Max(0, area.Height - height - 8))
                    : new Rect(area.X, area.Y, area.Width, Math.Max(0, area.Height - height - 8));
            }
        }
    }

    private static void AddEntry(ChartLayoutContext ctx, ChartScene scene, string label, ChartPaint paint, int index, Point at,
        double width, double rowHeight)
    {
        var style = ctx.Style;
        var hidden = ctx.IsHidden(index);
        var opacity = hidden ? 0.35 : 1;
        var text = style.Fit(label, width - Swatch - 6, small: true);
        var size = style.Measure(text, small: true);
        scene.Add(new RectPrim { Rect = new Rect(at.X, at.Y + (rowHeight - Swatch) / 2, Swatch, Swatch), Radius = 2.5, Fill = paint, Opacity = opacity });
        var prim = style.Text(text, new Point(at.X + Swatch + 6, at.Y + (rowHeight - size.Height) / 2), small: true,
            brush: hidden ? ChartBrush.MutedText : ChartBrush.Text);
        prim.Opacity = opacity;
        scene.Add(prim);
        scene.Legend.Add(new ChartLegendItem(label, paint, index) { Bounds = new Rect(at.X - 4, at.Y, width + 8, rowHeight) });
    }

    /// <summary>A short "no data" note in the middle of the area.</summary>
    public static void Empty(ChartLayoutContext ctx, ChartScene scene, Rect area) =>
        scene.Add(ctx.Style.CenteredText(RiceStrings.ChartNoData, area.Center, small: true, brush: ChartBrush.MutedText));
}

/// <summary>
/// Value axis plus a category axis (or a second value axis) around a plot rectangle, with grid lines and labels.
/// Vertical: categories along the bottom, values up. Horizontal (bar charts): categories down the left, values right.
/// </summary>
internal sealed class CartesianFrame
{
    private CartesianFrame(Rect plot, ChartScale scale, bool horizontal, int count, ChartScale? xScale)
    {
        Plot = plot;
        Scale = scale;
        Horizontal = horizontal;
        Count = Math.Max(1, count);
        XScale = xScale;
    }

    public Rect Plot { get; }

    public ChartScale Scale { get; }

    public bool Horizontal { get; }

    public int Count { get; }

    /// <summary>Numeric scale of the horizontal axis (scatter, bubble, histogram), else null.</summary>
    public ChartScale? XScale { get; }

    /// <summary>Size of one category band along the category axis.</summary>
    public double Band => (Horizontal ? Plot.Height : Plot.Width) / Count;

    /// <summary>Pixel of a value on the value axis.</summary>
    public double Value(double value) =>
        Horizontal ? Scale.Map(value, Plot.Left, Plot.Right) : Scale.Map(value, Plot.Bottom, Plot.Top);

    /// <summary>Pixel of a value on the numeric horizontal axis.</summary>
    public double X(double value) => XScale is { } x ? x.Map(value, Plot.Left, Plot.Right) : Plot.Left;

    public double BandStart(int index) => (Horizontal ? Plot.Top : Plot.Left) + index * Band;

    public double BandCenter(int index) => BandStart(index) + Band / 2;

    /// <summary>The value axis baseline: 0, or the scale end nearest to it.</summary>
    public double Baseline => Value(Math.Clamp(0, Scale.Min, Scale.Max));

    /// <summary>A rectangle from <paramref name="from"/> to <paramref name="to"/> (values) across part of a band.</summary>
    public Rect BarRect(int index, double offset, double thickness, double from, double to)
    {
        var a = Value(from);
        var b = Value(to);
        var start = BandStart(index) + offset;
        return Horizontal
            ? new Rect(Math.Min(a, b), start, Math.Abs(b - a), thickness)
            : new Rect(start, Math.Min(a, b), thickness, Math.Abs(b - a));
    }

    /// <summary>
    /// Lays out the axes of a category chart and draws grid lines and labels. <paramref name="valueRoom"/> is kept free
    /// beyond the ends of the value axis, for labels next to the bars.
    /// </summary>
    public static CartesianFrame Category(ChartScene scene, ChartStyle style, Rect area, IReadOnlyList<string> categories,
        double min, double max, bool horizontal, Func<double, string>? format = null, bool includeZero = true, double valueRoom = 0)
    {
        format ??= v => style.Format(v);
        var line = style.SmallLine;
        if (!horizontal)
        {
            var scale = ChartAxis.Nice(min, max, ChartAxis.TickCount(area.Height - line * 2, line * 2.6), includeZero);
            var labelWidth = scale.Ticks.Select(t => style.Measure(format(t), small: true).Width).DefaultIfEmpty().Max();
            var top = Math.Max(line / 2, valueRoom);
            var bottom = min < 0 ? valueRoom : 0;
            var plot = new Rect(area.X + labelWidth + 8, area.Y + top, Math.Max(1, area.Width - labelWidth - 12),
                Math.Max(1, area.Height - top - bottom - line - 8));
            var frame = new CartesianFrame(plot, scale, false, categories.Count, null);
            frame.DrawValueAxis(scene, style, format);
            frame.DrawCategoryLabels(scene, style, categories, 0);
            return frame;
        }
        else
        {
            var labelWidth = Math.Min(categories.Select(c => style.Measure(c, small: true).Width).DefaultIfEmpty().Max(), area.Width * 0.3);
            var scale = ChartAxis.Nice(min, max, ChartAxis.TickCount(area.Width - labelWidth, style.Measure("0000000", small: true).Width), includeZero);
            var right = Math.Max(style.Measure(format(scale.Max), small: true).Width / 2, valueRoom);
            var left = min < 0 ? Math.Max(valueRoom, style.Measure(format(scale.Min), small: true).Width / 2) : 0;
            var plot = new Rect(area.X + labelWidth + 8 + left, area.Y, Math.Max(1, area.Width - labelWidth - 8 - left - right),
                Math.Max(1, area.Height - line - 8));
            var frame = new CartesianFrame(plot, scale, true, categories.Count, null);
            frame.DrawValueAxis(scene, style, format);
            frame.DrawCategoryLabels(scene, style, categories, labelWidth);
            return frame;
        }
    }

    /// <summary>Lays out two value axes (scatter, bubble, histogram) and draws grid lines and labels.</summary>
    public static CartesianFrame Numeric(ChartScene scene, ChartStyle style, Rect area, double xMin, double xMax, double yMin,
        double yMax, Func<double, string>? xFormat = null, Func<double, string>? yFormat = null, ChartScale? xScale = null,
        bool xIncludeZero = false, bool yIncludeZero = true)
    {
        xFormat ??= v => style.Format(v);
        yFormat ??= v => style.Format(v);
        var line = style.SmallLine;
        var yScale = ChartAxis.Nice(yMin, yMax, ChartAxis.TickCount(area.Height - line * 2, line * 2.6), yIncludeZero);
        var labelWidth = yScale.Ticks.Select(t => style.Measure(yFormat(t), small: true).Width).DefaultIfEmpty().Max();
        var x = xScale ?? ChartAxis.Nice(xMin, xMax, ChartAxis.TickCount(area.Width - labelWidth, style.Measure("0000000", small: true).Width), xIncludeZero);
        var lastLabel = style.Measure(xFormat(x.Max), small: true).Width;
        var plot = new Rect(area.X + labelWidth + 8, area.Y + line / 2, Math.Max(1, area.Width - labelWidth - 8 - lastLabel / 2),
            Math.Max(1, area.Height - line / 2 - line - 8));
        var frame = new CartesianFrame(plot, yScale, false, 1, x);
        frame.DrawValueAxis(scene, style, yFormat);
        frame.DrawXAxis(scene, style, xFormat);
        return frame;
    }

    private void DrawValueAxis(ChartScene scene, ChartStyle style, Func<double, string> format)
    {
        foreach (var tick in Scale.Ticks)
        {
            var p = Value(tick);
            var isZero = Math.Abs(tick) < 1e-12;
            var text = format(tick);
            var size = style.Measure(text, small: true);
            if (Horizontal)
            {
                scene.Add(new PathPrim { Points = [new Point(p, Plot.Top), new Point(p, Plot.Bottom)], Stroke = isZero ? ChartBrush.Axis : ChartBrush.Grid });
                scene.Add(style.Text(text, new Point(p - size.Width / 2, Plot.Bottom + 6), small: true, brush: ChartBrush.MutedText));
            }
            else
            {
                scene.Add(new PathPrim { Points = [new Point(Plot.Left, p), new Point(Plot.Right, p)], Stroke = isZero ? ChartBrush.Axis : ChartBrush.Grid });
                scene.Add(style.Text(text, new Point(Plot.Left - 8 - size.Width, p - size.Height / 2), small: true, brush: ChartBrush.MutedText));
            }
        }
    }

    private void DrawXAxis(ChartScene scene, ChartStyle style, Func<double, string> format)
    {
        if (XScale is not { } scale)
            return;
        var lastRight = double.NegativeInfinity;
        foreach (var tick in scale.Ticks)
        {
            var x = X(tick);
            scene.Add(new PathPrim { Points = [new Point(x, Plot.Top), new Point(x, Plot.Bottom)], Stroke = ChartBrush.Grid });
            var text = format(tick);
            var size = style.Measure(text, small: true);
            if (x - size.Width / 2 < lastRight + 6)
                continue;
            scene.Add(style.Text(text, new Point(x - size.Width / 2, Plot.Bottom + 6), small: true, brush: ChartBrush.MutedText));
            lastRight = x + size.Width / 2;
        }
        scene.Add(new PathPrim { Points = [Plot.BottomLeft, Plot.BottomRight], Stroke = ChartBrush.Axis });
    }

    private void DrawCategoryLabels(ChartScene scene, ChartStyle style, IReadOnlyList<string> categories, double width)
    {
        if (categories.Count == 0)
            return;
        if (Horizontal)
        {
            var line = style.SmallLine;
            var every = Math.Max(1, (int)Math.Ceiling((line + 2) / Math.Max(1, Band)));
            for (var i = 0; i < categories.Count; i += every)
            {
                var text = style.Fit(categories[i], width + 1, small: true);
                var size = style.Measure(text, small: true);
                scene.Add(style.Text(text, new Point(Plot.Left - 8 - size.Width, BandCenter(i) - size.Height / 2), small: true, brush: ChartBrush.MutedText));
            }
            return;
        }

        // Labels that do not fit their band are thinned out: every 2nd, 3rd, … one is shown.
        var widest = categories.Max(c => style.Measure(c, small: true).Width);
        var step = Math.Max(1, (int)Math.Ceiling((Math.Min(widest, 120) + 8) / Math.Max(1, Band)));
        for (var i = 0; i < categories.Count; i += step)
        {
            var text = style.Fit(categories[i], Band * step - 6, small: true);
            var size = style.Measure(text, small: true);
            scene.Add(style.Text(text, new Point(BandCenter(i) - size.Width / 2, Plot.Bottom + 6), small: true, brush: ChartBrush.MutedText));
        }
    }
}
