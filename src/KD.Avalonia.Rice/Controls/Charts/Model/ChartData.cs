using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls.Charts;

// ---- categories: column, bar, line, area, radar, diverging bar ----

/// <summary>A named row of values, one per category; drawn in the chart colors unless <see cref="Color"/> is set.</summary>
public sealed class ChartSeries : ChartModel
{
    private string _name;
    private Color? _color;

    public ChartSeries() : this("")
    {
    }

    public ChartSeries(string name, params IEnumerable<double> values)
    {
        _name = name;
        Values = Track(new ChartCollection<double>(values));
    }

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    /// <summary>Own color instead of the theme's chart color.</summary>
    public Color? Color
    {
        get => _color;
        set => Set(ref _color, value);
    }

    /// <summary>The values in the order of <see cref="ChartCategoryData.Categories"/>; a missing value counts as 0.</summary>
    public ChartCollection<double> Values { get; }

    public override string ToString() => Name;
}

/// <summary>Data of the category charts: the category labels and one or more series of values.</summary>
public sealed class ChartCategoryData : ChartModel
{
    public ChartCategoryData()
    {
        Categories = Track(new ChartCollection<string>());
        Series = Track(new ChartCollection<ChartSeries>());
    }

    public ChartCategoryData(IEnumerable<string> categories, params IEnumerable<ChartSeries> series) : this()
    {
        Categories.Reset(categories);
        Series.Reset(series);
    }

    public ChartCollection<string> Categories { get; }

    public ChartCollection<ChartSeries> Series { get; }
}

/// <summary>
/// One value of a category chart, the <see cref="RiceChartBase.SelectedItem"/> of column, bar, line, area, radar and
/// diverging bar charts. Two points are equal when they are the same index of the same series.
/// </summary>
public sealed record ChartDataPoint(ChartSeries Series, int Index, string Category, double Value)
{
    public bool Equals(ChartDataPoint? other) =>
        other is not null && ReferenceEquals(Series, other.Series) && Index == other.Index;

    public override int GetHashCode() => HashCode.Combine(Series, Index);
}

// ---- slices: pie, donut, pyramid, funnel ----

public sealed class ChartSlice : ChartModel
{
    private string _label;
    private double _value;
    private Color? _color;

    public ChartSlice() : this("", 0)
    {
    }

    public ChartSlice(string label, double value)
    {
        _label = label;
        _value = value;
    }

    public string Label
    {
        get => _label;
        set => Set(ref _label, value);
    }

    public double Value
    {
        get => _value;
        set => Set(ref _value, value);
    }

    public Color? Color
    {
        get => _color;
        set => Set(ref _color, value);
    }

    public override string ToString() => Label;
}

// ---- points: scatter, bubble ----

public sealed class ChartPoint : ChartModel
{
    private double _x;
    private double _y;
    private double _size;
    private string? _label;

    public ChartPoint()
    {
    }

    public ChartPoint(double x, double y, double size = 0, string? label = null)
    {
        _x = x;
        _y = y;
        _size = size;
        _label = label;
    }

    public double X
    {
        get => _x;
        set => Set(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => Set(ref _y, value);
    }

    /// <summary>Bubble charts: the area of the bubble is proportional to it.</summary>
    public double Size
    {
        get => _size;
        set => Set(ref _size, value);
    }

    /// <summary>Shown in the tooltip.</summary>
    public string? Label
    {
        get => _label;
        set => Set(ref _label, value);
    }
}

public sealed class ChartPointSeries : ChartModel
{
    private string _name;
    private Color? _color;

    public ChartPointSeries() : this("")
    {
    }

    public ChartPointSeries(string name, params IEnumerable<ChartPoint> points)
    {
        _name = name;
        Points = Track(new ChartCollection<ChartPoint>(points));
    }

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public Color? Color
    {
        get => _color;
        set => Set(ref _color, value);
    }

    public ChartCollection<ChartPoint> Points { get; }

    public override string ToString() => Name;
}

// ---- heatmap ----

public sealed class HeatmapRow : ChartModel
{
    private string _label;

    public HeatmapRow() : this("")
    {
    }

    public HeatmapRow(string label, params IEnumerable<double> values)
    {
        _label = label;
        Values = Track(new ChartCollection<double>(values));
    }

    public string Label
    {
        get => _label;
        set => Set(ref _label, value);
    }

    /// <summary>One value per column; <see cref="double.NaN"/> is an empty cell.</summary>
    public ChartCollection<double> Values { get; }

    public override string ToString() => Label;
}

public sealed class HeatmapData : ChartModel
{
    public HeatmapData()
    {
        ColumnLabels = Track(new ChartCollection<string>());
        Rows = Track(new ChartCollection<HeatmapRow>());
    }

    public HeatmapData(IEnumerable<string> columnLabels, params IEnumerable<HeatmapRow> rows) : this()
    {
        ColumnLabels.Reset(columnLabels);
        Rows.Reset(rows);
    }

    public ChartCollection<string> ColumnLabels { get; }

    public ChartCollection<HeatmapRow> Rows { get; }
}

/// <summary>A cell of a heatmap, its <see cref="RiceChartBase.SelectedItem"/>; equal by row and column.</summary>
public sealed record HeatmapCell(HeatmapRow Row, int Column, string ColumnLabel, double Value)
{
    public bool Equals(HeatmapCell? other) => other is not null && ReferenceEquals(Row, other.Row) && Column == other.Column;

    public override int GetHashCode() => HashCode.Combine(Row, Column);
}

// ---- waterfall ----

public sealed class WaterfallStep : ChartModel
{
    private string _label;
    private double _value;
    private bool _isTotal;

    public WaterfallStep() : this("", 0)
    {
    }

    public WaterfallStep(string label, double value, bool isTotal = false)
    {
        _label = label;
        _value = value;
        _isTotal = isTotal;
    }

    public string Label
    {
        get => _label;
        set => Set(ref _label, value);
    }

    /// <summary>The change (positive or negative); ignored for a total.</summary>
    public double Value
    {
        get => _value;
        set => Set(ref _value, value);
    }

    /// <summary>A bar from zero to the running total instead of a change.</summary>
    public bool IsTotal
    {
        get => _isTotal;
        set => Set(ref _isTotal, value);
    }

    public override string ToString() => Label;
}

// ---- histogram ----

/// <summary>A bar of a histogram: the values in [<see cref="From"/>, <see cref="To"/>).</summary>
public sealed record HistogramBin(double From, double To, int Count);

// ---- gantt ----

public sealed class GanttTask : ChartModel
{
    private string _name;
    private string? _group;
    private DateTime _start;
    private DateTime _end;
    private double _progress;
    private string? _dependsOn;
    private Color? _color;

    public GanttTask() : this("", DateTime.Today, DateTime.Today.AddDays(1))
    {
    }

    public GanttTask(string name, DateTime start, DateTime end, string? group = null)
    {
        _name = name;
        _start = start;
        _end = end;
        _group = group;
    }

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    /// <summary>Tasks with the same group are drawn in one band, in the same color.</summary>
    public string? Group
    {
        get => _group;
        set => Set(ref _group, value);
    }

    public DateTime Start
    {
        get => _start;
        set => Set(ref _start, value);
    }

    /// <summary>Equal to <see cref="Start"/>: a milestone (diamond).</summary>
    public DateTime End
    {
        get => _end;
        set => Set(ref _end, value);
    }

    /// <summary>Done part, 0–1, drawn as a darker part of the bar.</summary>
    public double Progress
    {
        get => _progress;
        set => Set(ref _progress, value);
    }

    /// <summary>Name of the task this one follows; drawn as an arrow.</summary>
    public string? DependsOn
    {
        get => _dependsOn;
        set => Set(ref _dependsOn, value);
    }

    public Color? Color
    {
        get => _color;
        set => Set(ref _color, value);
    }

    public bool IsMilestone => End <= Start;

    public override string ToString() => Name;
}

// ---- bullet ----

public sealed class BulletItem : ChartModel
{
    private string _label;
    private double _value;
    private double _target;
    private double _maximum = double.NaN;

    public BulletItem() : this("", 0, 0)
    {
    }

    public BulletItem(string label, double value, double target, params IEnumerable<double> ranges)
    {
        _label = label;
        _value = value;
        _target = target;
        Ranges = Track(new ChartCollection<double>(ranges));
    }

    public string Label
    {
        get => _label;
        set => Set(ref _label, value);
    }

    public double Value
    {
        get => _value;
        set => Set(ref _value, value);
    }

    /// <summary>Drawn as a vertical mark.</summary>
    public double Target
    {
        get => _target;
        set => Set(ref _target, value);
    }

    /// <summary>Upper bounds of the qualitative ranges (e.g. poor, fair, good), ascending.</summary>
    public ChartCollection<double> Ranges { get; }

    /// <summary>End of the scale; NaN takes the largest of value, target and ranges.</summary>
    public double Maximum
    {
        get => _maximum;
        set => Set(ref _maximum, value);
    }

    public override string ToString() => Label;
}

// ---- gauge ----

/// <summary>A colored part of the gauge scale, e.g. the red zone.</summary>
public sealed class GaugeBand : ChartModel
{
    private double _from;
    private double _to;
    private Color? _color;
    private string? _label;

    public GaugeBand()
    {
    }

    public GaugeBand(double from, double to, Color? color = null, string? label = null)
    {
        _from = from;
        _to = to;
        _color = color;
        _label = label;
    }

    public double From
    {
        get => _from;
        set => Set(ref _from, value);
    }

    public double To
    {
        get => _to;
        set => Set(ref _to, value);
    }

    public Color? Color
    {
        get => _color;
        set => Set(ref _color, value);
    }

    public string? Label
    {
        get => _label;
        set => Set(ref _label, value);
    }
}

// ---- treemap ----

/// <summary>
/// A rectangle of a treemap. A node with <see cref="Children"/> is a group: its value is the sum of theirs, and they
/// are drawn inside it.
/// </summary>
public sealed class TreeMapNode : ChartModel
{
    private string _label;
    private double _value;
    private Color? _color;

    public TreeMapNode() : this("", 0)
    {
    }

    public TreeMapNode(string label, double value, params IEnumerable<TreeMapNode> children)
    {
        _label = label;
        _value = value;
        Children = Track(new ChartCollection<TreeMapNode>(children));
    }

    /// <summary>A group of <paramref name="children"/>.</summary>
    public TreeMapNode(string label, params IEnumerable<TreeMapNode> children) : this(label, 0, children)
    {
    }

    public string Label
    {
        get => _label;
        set => Set(ref _label, value);
    }

    /// <summary>The size of a leaf; ignored when the node has children.</summary>
    public double Value
    {
        get => _value;
        set => Set(ref _value, value);
    }

    /// <summary>Own color; the children take it unless they have their own.</summary>
    public Color? Color
    {
        get => _color;
        set => Set(ref _color, value);
    }

    public ChartCollection<TreeMapNode> Children { get; }

    /// <summary>The value as drawn: the sum of the children, or the own value (negative and NaN count as 0).</summary>
    public double Total => Children.Count > 0 ? Children.Sum(c => c.Total) : double.IsFinite(Value) ? Math.Max(0, Value) : 0;

    public override string ToString() => Label;
}
