using Avalonia;

namespace KD.Avalonia.Rice.Controls.Charts;

/// <summary>Layouts of the part-of-a-whole charts: pie, donut, pyramid, funnel.</summary>
internal static class SliceLayouts
{
    private static double Value(ChartSlice slice) => double.IsFinite(slice.Value) ? Math.Max(0, slice.Value) : 0;

    private static IReadOnlyList<(string, ChartPaint)> Legend(IReadOnlyList<ChartSlice> slices) =>
        slices.Select((s, i) => (s.Label, ChartPaint.Series(i, s.Color))).ToList();

    // ---- pie and donut ----

    /// <summary>
    /// <paramref name="innerRatio"/> is 0 for a pie, hole / outer radius for a donut; <paramref name="centerText"/> is
    /// shown in the hole (null: the total).
    /// </summary>
    public static ChartScene Pie(ChartLayoutContext ctx, IReadOnlyList<ChartSlice>? slices, double innerRatio, string? centerText,
        string? centerLabel)
    {
        var scene = new ChartScene();
        var style = ctx.Style;
        if (slices is null || slices.Count == 0)
        {
            ChartFrame.Empty(ctx, scene, new Rect(ctx.Size));
            return scene;
        }

        var area = ChartFrame.Begin(ctx, scene, Legend(slices), alwaysLegend: true, plotWidth: room => Math.Min(room.Width, room.Height));
        var visible = Enumerable.Range(0, slices.Count).Where(i => !ctx.IsHidden(i)).ToList();
        var total = visible.Sum(i => Value(slices[i]));
        var radius = Math.Max(4, Math.Min(area.Width, area.Height) / 2 - 2);
        var inner = radius * Math.Clamp(innerRatio, 0, 0.9);
        var center = area.Center;
        if (total <= 0)
        {
            scene.Add(new ArcPrim { Center = center, Radius = radius, InnerRadius = inner, SweepAngle = 360, Fill = ChartBrush.Track, Stroke = ChartPaint.None });
            return scene;
        }

        var p = ctx.Progress;
        var angle = 0.0;
        foreach (var i in visible)
        {
            var slice = slices[i];
            var share = Value(slice) / total;
            var sweep = share * 360 * p;
            var paint = ChartPaint.Series(i, slice.Color);
            var item = scene.AddItem(new ChartHitItem(slice, $"{style.Format(slice.Value)} ({share.ToString("P1", System.Globalization.CultureInfo.CurrentCulture)})")
            {
                TooltipTitle = slice.Label, Paint = paint, Shape = ChartHitShape.Arc, Center = center, Radius = radius, InnerRadius = inner,
                StartAngle = angle, SweepAngle = sweep, Bounds = new Rect(center, center).Inflate(radius),
            });
            scene.Add(new ArcPrim
            {
                Center = center, Radius = radius, InnerRadius = inner, StartAngle = angle, SweepAngle = sweep, Fill = paint,
                Thickness = visible.Count > 1 ? 2 : 0, Stroke = visible.Count > 1 ? ChartBrush.Separator : ChartPaint.None,
            }, item);

            // Percent on the slice when there is room for it.
            var labelRadius = inner > 0 ? (inner + radius) / 2 : radius * 0.64;
            var text = share.ToString("P0", System.Globalization.CultureInfo.CurrentCulture);
            var size = style.Measure(text, bold: true, small: true);
            var arcLength = sweep * Math.PI / 180 * labelRadius;
            if (p >= 1 && arcLength > size.Width + 6 && radius - inner > size.Height + 4)
            {
                var middle = (angle + sweep / 2 - 90) * Math.PI / 180;
                var at = new Point(center.X + labelRadius * Math.Cos(middle), center.Y + labelRadius * Math.Sin(middle));
                scene.Add(style.CenteredText(text, at, bold: true, small: true, on: paint), item);
            }
            angle += sweep;
        }

        if (inner > 0)
        {
            var big = style.Fit(centerText ?? style.Format(total), inner * 1.7, bold: true, small: false);
            var bigSize = Math.Round(Math.Min(style.FontSize * 2, inner * 0.45));
            var bigMeasure = style.Measure(big, bold: true, fontSize: bigSize);
            if (bigMeasure.Width > inner * 1.7)
                bigSize = Math.Max(style.FontSize, bigSize * inner * 1.7 / bigMeasure.Width);
            var label = centerLabel ?? (centerText is null ? RiceStrings.ChartTotal : null);
            var labelHeight = string.IsNullOrEmpty(label) ? 0 : style.SmallLine;
            var bigHeight = style.Measure(big, bold: true, fontSize: bigSize).Height;
            var top = center.Y - (bigHeight + labelHeight) / 2;
            scene.Add(style.CenteredText(big, new Point(center.X, top + bigHeight / 2), bold: true, fontSize: bigSize));
            if (!string.IsNullOrEmpty(label))
                scene.Add(style.CenteredText(style.Fit(label, inner * 1.6, small: true), new Point(center.X, top + bigHeight + labelHeight / 2),
                    small: true, brush: ChartBrush.MutedText));
        }
        return scene;
    }

    // ---- pyramid and funnel ----

    /// <summary>
    /// Pyramid: a triangle cut into layers whose heights follow the values, the first slice at the top.
    /// Funnel: stages whose widths follow the values, narrowing to the next one.
    /// </summary>
    public static ChartScene Stages(ChartLayoutContext ctx, IReadOnlyList<ChartSlice>? slices, bool pyramid)
    {
        var scene = new ChartScene();
        var style = ctx.Style;
        if (slices is null || slices.Count == 0)
        {
            ChartFrame.Empty(ctx, scene, new Rect(ctx.Size));
            return scene;
        }

        var area = ChartFrame.Begin(ctx, scene, Legend(slices), alwaysLegend: false);
        var visible = Enumerable.Range(0, slices.Count).Where(i => !ctx.IsHidden(i)).ToList();
        if (visible.Count == 0)
            return scene;
        var total = visible.Sum(i => Value(slices[i]));
        var first = visible.Select(i => Value(slices[i])).DefaultIfEmpty(0).First();
        var max = visible.Select(i => Value(slices[i])).DefaultIfEmpty(0).Max();

        // Labels on the right: "label" and "value · share".
        string Detail(ChartSlice s) => pyramid || first <= 0
            ? $"{style.Format(s.Value)} · {(total > 0 ? Value(s) / total : 0).ToString("P0", System.Globalization.CultureInfo.CurrentCulture)}"
            : $"{style.Format(s.Value)} · {(Value(s) / first).ToString("P0", System.Globalization.CultureInfo.CurrentCulture)}";
        var labelWidth = visible.Max(i => Math.Max(style.Measure(slices[i].Label, small: true).Width, style.Measure(Detail(slices[i]), small: true).Width));
        labelWidth = Math.Min(labelWidth, area.Width * 0.4);
        var shapeWidth = Math.Max(10, area.Width - labelWidth - 16);
        var cx = area.X + shapeWidth / 2;
        var p = ctx.Progress;
        const double gap = 2;
        var y = area.Y;
        var height = area.Height - gap * (visible.Count - 1);

        for (var k = 0; k < visible.Count; k++)
        {
            var i = visible[k];
            var slice = slices[i];
            var paint = ChartPaint.Series(i, slice.Color);
            double topWidth, bottomWidth, stageHeight;
            if (pyramid)
            {
                // Width grows linearly with the distance from the apex.
                stageHeight = total > 0 ? Value(slice) / total * height : height / visible.Count;
                var top = y - area.Y - gap * k;
                topWidth = shapeWidth * top / Math.Max(1, height);
                bottomWidth = shapeWidth * (top + stageHeight) / Math.Max(1, height);
            }
            else
            {
                stageHeight = height / visible.Count;
                topWidth = max > 0 ? Math.Max(shapeWidth * 0.08, shapeWidth * Value(slice) / max) : shapeWidth * 0.08;
                var next = k + 1 < visible.Count ? Value(slices[visible[k + 1]]) : Value(slice) * 0.85;
                bottomWidth = max > 0 ? Math.Max(shapeWidth * 0.06, shapeWidth * next / max) : shapeWidth * 0.06;
                bottomWidth = Math.Min(bottomWidth, topWidth);
            }
            topWidth *= p;
            bottomWidth *= p;
            var bottomY = y + stageHeight;
            List<Point> points =
            [
                new(cx - topWidth / 2, y), new(cx + topWidth / 2, y),
                new(cx + bottomWidth / 2, bottomY), new(cx - bottomWidth / 2, bottomY),
            ];
            var bounds = new Rect(cx - Math.Max(topWidth, bottomWidth) / 2, y, Math.Max(Math.Max(topWidth, bottomWidth), 4), Math.Max(stageHeight, 2));
            var item = scene.AddItem(new ChartHitItem(slice, Detail(slice)) { TooltipTitle = slice.Label, Paint = paint, Bounds = bounds });
            scene.Add(new PathPrim { Points = points, Closed = true, Fill = paint, Stroke = ChartPaint.None }, item);

            // Label to the right, vertically centered on the stage when there is room for two lines.
            var line = style.SmallLine;
            var x = area.X + shapeWidth + 16;
            var label = style.Fit(slice.Label, labelWidth + 1, small: true);
            var detail = style.Fit(Detail(slice), labelWidth + 1, small: true);
            if (stageHeight >= line * 2)
            {
                scene.Add(style.Text(label, new Point(x, y + stageHeight / 2 - line), small: true), item);
                scene.Add(style.Text(detail, new Point(x, y + stageHeight / 2), small: true, brush: ChartBrush.MutedText), item);
            }
            else if (stageHeight >= line * 0.8)
                scene.Add(style.Text(label, new Point(x, y + (stageHeight - line) / 2), small: true), item);
            y = bottomY + gap;
        }
        return scene;
    }
}
