using Avalonia;
using Avalonia.Media;
using KD.Avalonia.Rice.Controls.Uml;

namespace KD.Avalonia.Rice.Controls.Charts;

/// <summary>Color slots of a chart scene; the chart maps them to its (theme) brushes.</summary>
internal enum ChartBrush
{
    None,
    Text,
    MutedText,
    Axis,
    Grid,
    /// <summary>Lines between slices and around markers.</summary>
    Separator,
    /// <summary>Empty part of a scale: gauge and bullet backgrounds, empty heatmap cells.</summary>
    Track,
    Positive,
    Negative,
    HeatLow,
    HeatHigh,
    Series0,
    Series1,
    Series2,
    Series3,
    Series4,
    Series5,
    Series6,
    Series7,
}

/// <summary>A fill or stroke: a theme slot, or an own color (from the data) that wins over it.</summary>
internal readonly record struct ChartPaint(ChartBrush Brush, Color? Color = null)
{
    public static readonly ChartPaint None = new(ChartBrush.None);

    public static ChartPaint Series(int index, Color? color = null) => new(ChartBrush.Series0 + (index % 8 + 8) % 8, color);

    public bool IsNone => Brush == ChartBrush.None && Color is null;

    public static implicit operator ChartPaint(ChartBrush brush) => new(brush);
}

internal abstract class ChartPrim
{
    /// <summary>Index of the <see cref="ChartHitItem"/> this belongs to, -1 for none; dimmed with it.</summary>
    public int Item { get; set; } = -1;

    public double Opacity { get; set; } = 1;
}

internal sealed class RectPrim : ChartPrim
{
    public Rect Rect { get; init; }
    public double Radius { get; init; }
    public ChartPaint Fill { get; init; }
    public ChartPaint Stroke { get; init; } = ChartPaint.None;
    public double Thickness { get; init; } = 1;
}

/// <summary>Lines and polygons; the first <see cref="SmoothCount"/> points are joined by a smooth curve.</summary>
internal sealed class PathPrim : ChartPrim
{
    public required IReadOnlyList<Point> Points { get; init; }
    public bool Closed { get; init; }
    public int SmoothCount { get; init; }
    public ChartPaint Fill { get; init; } = ChartPaint.None;
    public ChartPaint Stroke { get; init; } = ChartBrush.Axis;
    public double Thickness { get; init; } = 1;
    public bool Dashed { get; init; }
}

/// <summary>Ring segment (pie slice when <see cref="InnerRadius"/> is 0); degrees clockwise from the top.</summary>
internal sealed class ArcPrim : ChartPrim
{
    public Point Center { get; init; }
    public double Radius { get; init; }
    public double InnerRadius { get; init; }
    public double StartAngle { get; init; }
    public double SweepAngle { get; init; }
    public ChartPaint Fill { get; init; }
    public ChartPaint Stroke { get; init; } = ChartBrush.Separator;
    public double Thickness { get; init; } = 2;
}

internal sealed class CirclePrim : ChartPrim
{
    public Point Center { get; init; }
    public double Radius { get; init; }
    public ChartPaint Fill { get; init; }
    public ChartPaint Stroke { get; init; } = ChartPaint.None;
    public double Thickness { get; init; } = 1.5;
}

internal sealed class LabelPrim : ChartPrim
{
    public required string Text { get; init; }
    /// <summary>Top-left corner.</summary>
    public Point Origin { get; init; }
    public Size Size { get; init; }
    public double FontSize { get; init; }
    public bool Bold { get; init; }
    public ChartPaint Brush { get; init; } = ChartBrush.Text;
    /// <summary>When set, the text is dark or white, whichever reads better on this fill.</summary>
    public ChartPaint? On { get; init; }
}

internal enum ChartHitShape
{
    Rect,
    /// <summary>Ring segment: <see cref="ChartHitItem.Center"/>, radii and angles.</summary>
    Arc,
    /// <summary>Within <see cref="ChartHitItem.Radius"/> of <see cref="ChartHitItem.Center"/>.</summary>
    Circle,
}

/// <summary>A hoverable, selectable part of the chart: a bar, a slice, a point, …</summary>
internal sealed class ChartHitItem(object model, string tooltip)
{
    public object Model { get; } = model;

    /// <summary>Title line of the tooltip (e.g. the series), may be empty.</summary>
    public string? TooltipTitle { get; init; }

    public string Tooltip { get; } = tooltip;

    /// <summary>The color shown in the tooltip.</summary>
    public ChartPaint Paint { get; init; }

    public ChartHitShape Shape { get; init; }

    public Rect Bounds { get; init; }

    public double CornerRadius { get; init; }

    public Point Center { get; init; }

    public double Radius { get; init; }

    public double InnerRadius { get; init; }

    public double StartAngle { get; init; }

    public double SweepAngle { get; init; }

    public bool Contains(Point p, double tolerance)
    {
        switch (Shape)
        {
            case ChartHitShape.Circle:
                return Distance(p, Center) <= Radius + tolerance;
            case ChartHitShape.Arc:
            {
                var d = Distance(p, Center);
                if (d > Radius + tolerance || d < InnerRadius - tolerance)
                    return false;
                var angle = (Math.Atan2(p.Y - Center.Y, p.X - Center.X) * 180 / Math.PI + 90 + 360) % 360;
                var from = ((StartAngle % 360) + 360) % 360;
                var delta = (angle - from + 360) % 360;
                return delta <= SweepAngle;
            }
            default:
                return Bounds.Inflate(tolerance).Contains(p);
        }
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}

/// <summary>A legend entry; clicking it hides or shows the series (or slice) <see cref="Index"/>.</summary>
internal sealed class ChartLegendItem(string label, ChartPaint paint, int index)
{
    public string Label { get; } = label;

    public ChartPaint Paint { get; } = paint;

    public int Index { get; } = index;

    public Rect Bounds { get; set; }
}

/// <summary>A laid out chart: drawing primitives in painting order, the interactive items and the legend.</summary>
internal sealed class ChartScene
{
    public List<ChartPrim> Prims { get; } = [];

    public List<ChartHitItem> Items { get; } = [];

    public List<ChartLegendItem> Legend { get; } = [];

    public int AddItem(ChartHitItem item)
    {
        Items.Add(item);
        return Items.Count - 1;
    }

    public T Add<T>(T prim, int item = -1) where T : ChartPrim
    {
        prim.Item = item;
        Prims.Add(prim);
        return prim;
    }
}

/// <summary>Font sizes, text measurement and number formatting shared by the chart layouts.</summary>
internal sealed class ChartStyle(IUmlTextMeasurer measurer, double fontSize, string? valueFormat = null)
{
    public IUmlTextMeasurer Measurer { get; } = measurer;

    public double FontSize { get; } = fontSize;

    public double SmallFontSize => Math.Max(8, FontSize - 2);

    public double TitleFontSize => Math.Round(FontSize * 1.15);

    /// <summary>Height of a line of small text.</summary>
    public double SmallLine => Measure("Ag", small: true).Height;

    public string? ValueFormat { get; } = valueFormat;

    public string Format(double value, string? format = null) => ChartAxis.Format(value, format ?? ValueFormat);

    public Size Measure(string text, bool bold = false, bool small = false, double? fontSize = null) =>
        string.IsNullOrEmpty(text) ? default : Measurer.Measure(text, fontSize ?? (small ? SmallFontSize : FontSize), bold);

    public LabelPrim Text(string text, Point origin, bool bold = false, bool small = false, ChartPaint? brush = null,
        double? fontSize = null, ChartPaint? on = null) => new()
    {
        Text = text,
        Origin = origin,
        Size = Measure(text, bold, small, fontSize),
        FontSize = fontSize ?? (small ? SmallFontSize : FontSize),
        Bold = bold,
        Brush = brush ?? ChartBrush.Text,
        On = on,
    };

    /// <summary>A text centered on <paramref name="center"/>.</summary>
    public LabelPrim CenteredText(string text, Point center, bool bold = false, bool small = false, ChartPaint? brush = null,
        double? fontSize = null, ChartPaint? on = null)
    {
        var size = Measure(text, bold, small, fontSize);
        return Text(text, new Point(center.X - size.Width / 2, center.Y - size.Height / 2), bold, small, brush, fontSize, on);
    }

    /// <summary>Cuts the text to the width with an ellipsis.</summary>
    public string Fit(string text, double width, bool bold = false, bool small = false)
    {
        if (Measure(text, bold, small).Width <= width)
            return text;
        for (var length = text.Length - 1; length > 0; length--)
        {
            var candidate = text[..length].TrimEnd() + "…";
            if (Measure(candidate, bold, small).Width <= width)
                return candidate;
        }
        return "";
    }
}
