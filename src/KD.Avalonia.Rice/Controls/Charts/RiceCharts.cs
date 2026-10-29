using Avalonia;

namespace KD.Avalonia.Rice.Controls.Charts;

// ---- category charts ----

/// <summary>Base of the charts drawn from a <see cref="ChartCategoryData"/>: categories and series of values.</summary>
public abstract class RiceCategoryChartBase : RiceChartBase
{
    public static readonly StyledProperty<ChartCategoryData?> DataProperty =
        AvaloniaProperty.Register<RiceCategoryChartBase, ChartCategoryData?>(nameof(Data));

    static RiceCategoryChartBase() => RegisterData(DataProperty);

    public ChartCategoryData? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }
}

/// <summary>Vertical bars of the first series, one per category.</summary>
public class RiceColumnChart : RiceCategoryChartBase
{
    private protected override ChartScene BuildScene(ChartLayoutContext context) =>
        CategoryLayouts.Bars(context, Data, BarMode.Single, horizontal: false, ShowValues);
}

/// <summary>Vertical bars of every series side by side in each category.</summary>
public class RiceClusteredColumnChart : RiceCategoryChartBase
{
    private protected override ChartScene BuildScene(ChartLayoutContext context) =>
        CategoryLayouts.Bars(context, Data, BarMode.Clustered, horizontal: false, ShowValues);
}

/// <summary>Vertical bars of the series stacked in each category; <see cref="IsPercent"/> scales them to 100 %.</summary>
public class RiceStackedColumnChart : RiceCategoryChartBase
{
    public static readonly StyledProperty<bool> IsPercentProperty =
        AvaloniaProperty.Register<RiceStackedColumnChart, bool>(nameof(IsPercent));

    static RiceStackedColumnChart() => RegisterLayout(IsPercentProperty);

    public bool IsPercent
    {
        get => GetValue(IsPercentProperty);
        set => SetValue(IsPercentProperty, value);
    }

    private protected override ChartScene BuildScene(ChartLayoutContext context) =>
        CategoryLayouts.Bars(context, Data, IsPercent ? BarMode.Percent : BarMode.Stacked, horizontal: false, ShowValues);
}

/// <summary>Horizontal bars of the first series, one per category.</summary>
public class RiceBarChart : RiceCategoryChartBase
{
    private protected override ChartScene BuildScene(ChartLayoutContext context) =>
        CategoryLayouts.Bars(context, Data, BarMode.Single, horizontal: true, ShowValues);
}

/// <summary>Horizontal bars of every series side by side in each category.</summary>
public class RiceClusteredBarChart : RiceCategoryChartBase
{
    private protected override ChartScene BuildScene(ChartLayoutContext context) =>
        CategoryLayouts.Bars(context, Data, BarMode.Clustered, horizontal: true, ShowValues);
}

/// <summary>Horizontal bars of the series stacked in each category; <see cref="IsPercent"/> scales them to 100 %.</summary>
public class RiceStackedBarChart : RiceCategoryChartBase
{
    public static readonly StyledProperty<bool> IsPercentProperty =
        RiceStackedColumnChart.IsPercentProperty.AddOwner<RiceStackedBarChart>();

    public bool IsPercent
    {
        get => GetValue(IsPercentProperty);
        set => SetValue(IsPercentProperty, value);
    }

    private protected override ChartScene BuildScene(ChartLayoutContext context) =>
        CategoryLayouts.Bars(context, Data, IsPercent ? BarMode.Percent : BarMode.Stacked, horizontal: true, ShowValues);
}

/// <summary>
/// Horizontal bars stacked outward from a central zero line: negative values to the left, positive to the right
/// (e.g. survey answers from "disagree" to "agree").
/// </summary>
public class RiceDivergingBarChart : RiceCategoryChartBase
{
    private protected override ChartScene BuildScene(ChartLayoutContext context) =>
        CategoryLayouts.Bars(context, Data, BarMode.Diverging, horizontal: true, ShowValues);
}

/// <summary>A line per series through its values.</summary>
public class RiceLineChart : RiceCategoryChartBase
{
    public static readonly StyledProperty<bool> IsSmoothProperty =
        AvaloniaProperty.Register<RiceLineChart, bool>(nameof(IsSmooth));

    public static readonly StyledProperty<bool> ShowMarkersProperty =
        AvaloniaProperty.Register<RiceLineChart, bool>(nameof(ShowMarkers), true);

    static RiceLineChart() => RegisterLayout(IsSmoothProperty, ShowMarkersProperty);

    /// <summary>A smooth curve instead of straight segments.</summary>
    public bool IsSmooth
    {
        get => GetValue(IsSmoothProperty);
        set => SetValue(IsSmoothProperty, value);
    }

    /// <summary>A dot on each value.</summary>
    public bool ShowMarkers
    {
        get => GetValue(ShowMarkersProperty);
        set => SetValue(ShowMarkersProperty, value);
    }

    private protected override ChartScene BuildScene(ChartLayoutContext context) =>
        CategoryLayouts.Lines(context, Data, fill: false, stacked: false, IsSmooth, ShowMarkers);
}

/// <summary>Lines with the area under them filled; <see cref="IsStacked"/> stacks the series.</summary>
public class RiceAreaChart : RiceCategoryChartBase
{
    public static readonly StyledProperty<bool> IsSmoothProperty = RiceLineChart.IsSmoothProperty.AddOwner<RiceAreaChart>();

    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<RiceAreaChart, bool>(nameof(IsStacked));

    static RiceAreaChart() => RegisterLayout(IsStackedProperty);

    public bool IsSmooth
    {
        get => GetValue(IsSmoothProperty);
        set => SetValue(IsSmoothProperty, value);
    }

    public bool IsStacked
    {
        get => GetValue(IsStackedProperty);
        set => SetValue(IsStackedProperty, value);
    }

    private protected override ChartScene BuildScene(ChartLayoutContext context) =>
        CategoryLayouts.Lines(context, Data, fill: true, IsStacked, IsSmooth, markers: false);
}

/// <summary>A polygon per series on spokes, one spoke per category (at least three).</summary>
public class RiceRadarChart : RiceCategoryChartBase
{
    public static readonly StyledProperty<bool> IsFilledProperty =
        AvaloniaProperty.Register<RiceRadarChart, bool>(nameof(IsFilled), true);

    static RiceRadarChart() => RegisterLayout(IsFilledProperty);

    public bool IsFilled
    {
        get => GetValue(IsFilledProperty);
        set => SetValue(IsFilledProperty, value);
    }

    private protected override ChartScene BuildScene(ChartLayoutContext context) => CategoryLayouts.Radar(context, Data, IsFilled);
}

// ---- part of a whole ----

/// <summary>Base of the charts drawn from <see cref="ChartSlice"/>s: pie, donut, pyramid, funnel.</summary>
public abstract class RiceSliceChartBase : RiceChartBase
{
    public static readonly StyledProperty<IList<ChartSlice>?> SlicesProperty =
        AvaloniaProperty.Register<RiceSliceChartBase, IList<ChartSlice>?>(nameof(Slices));

    static RiceSliceChartBase() => RegisterData(SlicesProperty);

    /// <summary>The slices; a <see cref="ChartCollection{T}"/> is followed when it changes.</summary>
    public IList<ChartSlice>? Slices
    {
        get => GetValue(SlicesProperty);
        set => SetValue(SlicesProperty, value);
    }

    private protected IReadOnlyList<ChartSlice>? SliceList => Slices?.ToList();
}

public class RicePieChart : RiceSliceChartBase
{
    static RicePieChart() => LegendPositionProperty.OverrideDefaultValue<RicePieChart>(ChartLegendPosition.Right);

    private protected override ChartScene BuildScene(ChartLayoutContext context) =>
        SliceLayouts.Pie(context, SliceList, 0, null, null);
}

/// <summary>A pie with a hole; the hole shows <see cref="CenterText"/> (by default the total).</summary>
public class RiceDonutChart : RiceSliceChartBase
{
    public static readonly StyledProperty<double> InnerRadiusRatioProperty =
        AvaloniaProperty.Register<RiceDonutChart, double>(nameof(InnerRadiusRatio), 0.62);

    public static readonly StyledProperty<string?> CenterTextProperty =
        AvaloniaProperty.Register<RiceDonutChart, string?>(nameof(CenterText));

    public static readonly StyledProperty<string?> CenterLabelProperty =
        AvaloniaProperty.Register<RiceDonutChart, string?>(nameof(CenterLabel));

    static RiceDonutChart()
    {
        LegendPositionProperty.OverrideDefaultValue<RiceDonutChart>(ChartLegendPosition.Right);
        RegisterLayout(InnerRadiusRatioProperty, CenterTextProperty, CenterLabelProperty);
    }

    /// <summary>Radius of the hole relative to the outer radius, 0–0.9.</summary>
    public double InnerRadiusRatio
    {
        get => GetValue(InnerRadiusRatioProperty);
        set => SetValue(InnerRadiusRatioProperty, value);
    }

    /// <summary>Big text in the hole; null shows the total of the slices.</summary>
    public string? CenterText
    {
        get => GetValue(CenterTextProperty);
        set => SetValue(CenterTextProperty, value);
    }

    /// <summary>Small text under <see cref="CenterText"/>; by default "Total" under the total.</summary>
    public string? CenterLabel
    {
        get => GetValue(CenterLabelProperty);
        set => SetValue(CenterLabelProperty, value);
    }

    private protected override ChartScene BuildScene(ChartLayoutContext context) =>
        SliceLayouts.Pie(context, SliceList, InnerRadiusRatio, CenterText, CenterLabel);
}

/// <summary>A triangle cut into layers whose heights follow the values, the first slice at the top.</summary>
public class RicePyramidChart : RiceSliceChartBase
{
    private protected override ChartScene BuildScene(ChartLayoutContext context) => SliceLayouts.Stages(context, SliceList, pyramid: true);
}

/// <summary>Stages narrowing from the first to the last, widths following the values (e.g. a sales pipeline).</summary>
public class RiceFunnelChart : RiceSliceChartBase
{
    private protected override ChartScene BuildScene(ChartLayoutContext context) => SliceLayouts.Stages(context, SliceList, pyramid: false);
}

// ---- points ----

/// <summary>Base of the charts drawn from <see cref="ChartPointSeries"/>: scatter and bubble.</summary>
public abstract class RicePointChartBase : RiceChartBase
{
    public static readonly StyledProperty<IList<ChartPointSeries>?> SeriesProperty =
        AvaloniaProperty.Register<RicePointChartBase, IList<ChartPointSeries>?>(nameof(Series));

    static RicePointChartBase() => RegisterData(SeriesProperty);

    public IList<ChartPointSeries>? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }
}

/// <summary>A dot per (x, y) point.</summary>
public class RiceScatterChart : RicePointChartBase
{
    private protected override ChartScene BuildScene(ChartLayoutContext context) => XYLayouts.Points(context, Series?.ToList(), bubbles: false);
}

/// <summary>A circle per (x, y) point whose area follows <see cref="ChartPoint.Size"/>.</summary>
public class RiceBubbleChart : RicePointChartBase
{
    private protected override ChartScene BuildScene(ChartLayoutContext context) => XYLayouts.Points(context, Series?.ToList(), bubbles: true);
}

// ---- others ----

/// <summary>A grid of cells colored by their value, from the background to the accent.</summary>
public class RiceHeatmapChart : RiceChartBase
{
    public static readonly StyledProperty<HeatmapData?> DataProperty =
        AvaloniaProperty.Register<RiceHeatmapChart, HeatmapData?>(nameof(Data));

    static RiceHeatmapChart() => RegisterData(DataProperty);

    public HeatmapData? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    private protected override ChartScene BuildScene(ChartLayoutContext context) => XYLayouts.Heatmap(context, Data, ShowValues);
}

/// <summary>Running total: each step a bar from the previous total, up (positive) or down (negative); totals from zero.</summary>
public class RiceWaterfallChart : RiceChartBase
{
    public static readonly StyledProperty<IList<WaterfallStep>?> StepsProperty =
        AvaloniaProperty.Register<RiceWaterfallChart, IList<WaterfallStep>?>(nameof(Steps));

    static RiceWaterfallChart() => RegisterData(StepsProperty);

    public IList<WaterfallStep>? Steps
    {
        get => GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    private protected override ChartScene BuildScene(ChartLayoutContext context) => XYLayouts.Waterfall(context, Steps?.ToList(), ShowValues);
}

/// <summary>Distribution of values: bars counting the values in equal, round-width bins.</summary>
public class RiceHistogramChart : RiceChartBase
{
    public static readonly StyledProperty<IList<double>?> ValuesProperty =
        AvaloniaProperty.Register<RiceHistogramChart, IList<double>?>(nameof(Values));

    public static readonly StyledProperty<int> BinCountProperty =
        AvaloniaProperty.Register<RiceHistogramChart, int>(nameof(BinCount));

    public static readonly StyledProperty<double> BinWidthProperty =
        AvaloniaProperty.Register<RiceHistogramChart, double>(nameof(BinWidth), double.NaN);

    static RiceHistogramChart()
    {
        RegisterData(ValuesProperty);
        RegisterLayout(BinCountProperty, BinWidthProperty);
    }

    /// <summary>The raw values; a <see cref="ChartCollection{T}"/> is followed when it changes.</summary>
    public IList<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>Wanted number of bins (rounded to a round bin width); 0 picks one from the number of values.</summary>
    public int BinCount
    {
        get => GetValue(BinCountProperty);
        set => SetValue(BinCountProperty, value);
    }

    /// <summary>Exact bin width; NaN (default) derives it from <see cref="BinCount"/>.</summary>
    public double BinWidth
    {
        get => GetValue(BinWidthProperty);
        set => SetValue(BinWidthProperty, value);
    }

    /// <summary>The bins as drawn.</summary>
    public IReadOnlyList<HistogramBin> Bins => Values is null ? [] : HistogramBinner.Bin(Values, BinCount, BinWidth);

    private protected override ChartScene BuildScene(ChartLayoutContext context) =>
        XYLayouts.Histogram(context, Values?.ToList(), BinCount, BinWidth, ShowValues);
}

/// <summary>
/// Nested rectangles whose areas follow the values: a color per top-level node, its children drawn inside it under
/// its label.
/// </summary>
public class RiceTreeMapChart : RiceChartBase
{
    public static readonly StyledProperty<IList<TreeMapNode>?> NodesProperty =
        AvaloniaProperty.Register<RiceTreeMapChart, IList<TreeMapNode>?>(nameof(Nodes));

    static RiceTreeMapChart() => RegisterData(NodesProperty);

    /// <summary>The top-level nodes; a <see cref="ChartCollection{T}"/> is followed when it changes.</summary>
    public IList<TreeMapNode>? Nodes
    {
        get => GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    private protected override ChartScene BuildScene(ChartLayoutContext context) => TreeMapLayouts.TreeMap(context, Nodes?.ToList(), ShowValues);
}

/// <summary>Tasks as bars on a time axis, grouped, with progress, milestones and dependency arrows.</summary>
public class RiceGanttChart : RiceChartBase
{
    public static readonly StyledProperty<IList<GanttTask>?> TasksProperty =
        AvaloniaProperty.Register<RiceGanttChart, IList<GanttTask>?>(nameof(Tasks));

    public static readonly StyledProperty<bool> ShowTodayProperty =
        AvaloniaProperty.Register<RiceGanttChart, bool>(nameof(ShowToday), true);

    static RiceGanttChart()
    {
        RegisterData(TasksProperty);
        RegisterLayout(ShowTodayProperty);
    }

    public IList<GanttTask>? Tasks
    {
        get => GetValue(TasksProperty);
        set => SetValue(TasksProperty, value);
    }

    /// <summary>A dashed line at today's date, when it is in the shown range.</summary>
    public bool ShowToday
    {
        get => GetValue(ShowTodayProperty);
        set => SetValue(ShowTodayProperty, value);
    }

    private protected override ChartScene BuildScene(ChartLayoutContext context) =>
        ProgressLayouts.Gantt(context, Tasks?.ToList(), ShowToday ? DateTime.Today : null);
}

/// <summary>A compact bar per measure against a target mark and qualitative ranges.</summary>
public class RiceBulletChart : RiceChartBase
{
    public static readonly StyledProperty<IList<BulletItem>?> ItemsProperty =
        AvaloniaProperty.Register<RiceBulletChart, IList<BulletItem>?>(nameof(Items));

    static RiceBulletChart() => RegisterData(ItemsProperty);

    public IList<BulletItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    private protected override ChartScene BuildScene(ChartLayoutContext context) => ProgressLayouts.Bullet(context, Items?.ToList());
}

/// <summary>A half circle dial: colored <see cref="Bands"/> with a needle, or without bands an arc filled up to the value.</summary>
public class RiceGaugeChart : RiceChartBase
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<RiceGaugeChart, double>(nameof(Value));

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<RiceGaugeChart, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<RiceGaugeChart, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<IList<GaugeBand>?> BandsProperty =
        AvaloniaProperty.Register<RiceGaugeChart, IList<GaugeBand>?>(nameof(Bands));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<RiceGaugeChart, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueTextProperty =
        AvaloniaProperty.Register<RiceGaugeChart, string?>(nameof(ValueText));

    // The needle swings from the previous value to the new one.
    private double _from = double.NaN;
    private double _shown;

    static RiceGaugeChart()
    {
        RegisterData(BandsProperty);
        RegisterLayout(MinimumProperty, MaximumProperty, LabelProperty, ValueTextProperty);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public IList<GaugeBand>? Bands
    {
        get => GetValue(BandsProperty);
        set => SetValue(BandsProperty, value);
    }

    /// <summary>Small text under the value, e.g. the unit or the measure.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Shown instead of the formatted <see cref="Value"/>.</summary>
    public string? ValueText
    {
        get => GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            _from = _shown;
            Replay();
            InvalidateScene();
        }
    }

    private protected override ChartScene BuildScene(ChartLayoutContext context)
    {
        var from = double.IsNaN(_from) ? Minimum : _from;
        var shown = from + (Value - from) * context.Progress;
        if (context.Progress >= 1)
            _from = double.NaN;
        _shown = shown;
        return ProgressLayouts.Gauge(context, Value, Minimum, Maximum, Bands?.ToList(), Label, ValueText, shown);
    }
}
