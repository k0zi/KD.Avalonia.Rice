using Avalonia;
using KD.Avalonia.Rice.Controls.Charts;
using KD.Avalonia.Rice.Controls.Uml;
using KD.Avalonia.Rice.Demo;

namespace KD.Avalonia.Rice.Tests;

public class ChartLayoutTests
{
    private sealed class FakeMeasurer : IUmlTextMeasurer
    {
        public Size Measure(string text, double fontSize, bool bold = false, bool italic = false)
        {
            var lines = text.Split('\n');
            return new Size(lines.Max(l => l.Length) * fontSize * (bold ? 0.6 : 0.55), lines.Length * fontSize * 1.3);
        }
    }

    private static readonly FakeMeasurer Measurer = new();
    private static readonly Size Size = new(480, 300);

    private static ChartLayoutContext Context(double progress = 1, params int[] hidden) =>
        new(Size, new ChartStyle(Measurer, 13)) { Progress = progress, Hidden = hidden.ToHashSet() };

    public static TheoryData<string> SampleNames => new(ChartSamples.All.Select(s => s.Name));

    private static IEnumerable<Point> PointsOf(ChartPrim prim) => prim switch
    {
        RectPrim r => [r.Rect.TopLeft, r.Rect.BottomRight],
        PathPrim p => p.Points,
        CirclePrim c => [c.Center],
        ArcPrim a => [a.Center],
        LabelPrim t => [t.Origin],
        _ => [],
    };

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void Every_sample_is_laid_out_inside_its_bounds(string name)
    {
        var chart = ChartSamples.All.Single(s => s.Name == name).Create(new Random(1));

        var scene = chart.Layout(Size, Measurer);

        Assert.NotEmpty(scene.Prims);
        Assert.NotEmpty(scene.Items);
        var bounds = new Rect(Size).Inflate(1);
        foreach (var point in scene.Prims.SelectMany(PointsOf))
        {
            Assert.True(double.IsFinite(point.X) && double.IsFinite(point.Y), $"{name}: NaN in the scene");
            Assert.True(bounds.Contains(point), $"{name}: {point} is outside the chart");
        }
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void Every_sample_survives_a_tiny_size_and_the_start_of_the_animation(string name)
    {
        var chart = ChartSamples.All.Single(s => s.Name == name).Create(new Random(2));

        chart.Layout(new Size(30, 20), Measurer);
        var scene = chart.Layout(Size, Measurer, progress: 0);

        Assert.All(scene.Prims.SelectMany(PointsOf), p => Assert.True(double.IsFinite(p.X) && double.IsFinite(p.Y)));
    }

    [Fact]
    public void Nice_scale_uses_round_steps()
    {
        var scale = ChartAxis.Nice(0, 97, 6);

        Assert.Equal([0, 20, 40, 60, 80, 100], scale.Ticks);
    }

    [Fact]
    public void Nice_scale_covers_negative_values_and_a_single_value()
    {
        var negative = ChartAxis.Nice(-38, 12, 6);
        Assert.True(negative.Min <= -38 && negative.Max >= 12);
        Assert.Contains(0.0, negative.Ticks);

        var single = ChartAxis.Nice(5, 5, includeZero: false);
        Assert.True(single.Min < 5 && single.Max > 5);
        var zero = ChartAxis.Nice(0, 0);
        Assert.True(zero.Max > 0);
    }

    private static ChartCategoryData TwoSeries() => new(["A", "B"], new ChartSeries("S1", 10, 20), new ChartSeries("S2", 30, -5));

    private static List<RectPrim> Bars(ChartScene scene) =>
        scene.Items.Select((item, i) => scene.Prims.OfType<RectPrim>().Single(p => p.Item == i)).ToList();

    [Fact]
    public void Stacked_bars_end_at_the_sum()
    {
        var scene = CategoryLayouts.Bars(Context(), TwoSeries(), BarMode.Stacked, horizontal: false, showValues: false);

        var points = scene.Items.Select(i => (ChartDataPoint)i.Model).ToList();
        var bars = Bars(scene);
        // Category A: S1 (10) at the bottom, S2 (30) on top of it, so S2 ends where 40 is.
        var a1 = bars[points.FindIndex(p => p is { Index: 0, Series.Name: "S1" })];
        var a2 = bars[points.FindIndex(p => p is { Index: 0, Series.Name: "S2" })];
        Assert.True(a2.Rect.Bottom <= a1.Rect.Top + 1.01);
        Assert.Equal(3, a2.Rect.Height / a1.Rect.Height, 1);
        // Category B: the negative value goes below the baseline.
        var b1 = bars[points.FindIndex(p => p is { Index: 1, Series.Name: "S1" })];
        var b2 = bars[points.FindIndex(p => p is { Index: 1, Series.Name: "S2" })];
        Assert.True(b2.Rect.Top >= b1.Rect.Bottom - 1);
    }

    [Fact]
    public void Percent_bars_fill_the_whole_category()
    {
        var data = new ChartCategoryData(["A", "B"], new ChartSeries("S1", 1, 3), new ChartSeries("S2", 3, 9));

        var scene = CategoryLayouts.Bars(Context(), data, BarMode.Percent, horizontal: true, showValues: false);

        var bars = Bars(scene);
        var a = bars.Where((_, i) => ((ChartDataPoint)scene.Items[i].Model).Index == 0).Sum(r => r.Rect.Width);
        var b = bars.Where((_, i) => ((ChartDataPoint)scene.Items[i].Model).Index == 1).Sum(r => r.Rect.Width);
        Assert.Equal(a, b, 0);
        var s1 = bars.Where((_, i) => scene.Items[i].Model is ChartDataPoint { Index: 0, Series.Name: "S1" }).Single();
        Assert.Equal(0.25, s1.Rect.Width / a, 1);
    }

    [Fact]
    public void Diverging_bars_put_negative_values_left_of_zero()
    {
        var data = new ChartCategoryData(["A"], new ChartSeries("No", -20), new ChartSeries("Yes", 60));

        var scene = CategoryLayouts.Bars(Context(), data, BarMode.Diverging, horizontal: true, showValues: false);

        var bars = Bars(scene);
        var no = bars[0].Rect;
        var yes = bars[1].Rect;
        Assert.True(no.Right <= yes.Left + 1.01);
        Assert.Equal(3, yes.Width / no.Width, 1);
    }

    [Fact]
    public void Bars_start_at_zero_height_and_grow()
    {
        var start = Bars(CategoryLayouts.Bars(Context(progress: 0), TwoSeries(), BarMode.Clustered, false, false));
        var end = Bars(CategoryLayouts.Bars(Context(), TwoSeries(), BarMode.Clustered, false, false));

        Assert.All(start, b => Assert.True(b.Rect.Height <= 1));
        Assert.All(end, b => Assert.True(b.Rect.Height > 5));
    }

    [Fact]
    public void Hidden_series_leave_the_scale()
    {
        var data = new ChartCategoryData(["A"], new ChartSeries("Small", 10), new ChartSeries("Big", 1000));

        var all = Bars(CategoryLayouts.Bars(Context(), data, BarMode.Clustered, false, false));
        var hidden = CategoryLayouts.Bars(Context(1, 1), data, BarMode.Clustered, false, false);

        Assert.Single(hidden.Items);
        Assert.True(Bars(hidden)[0].Rect.Height > all[0].Rect.Height * 10);
        // The legend still lists the hidden series, so it can be switched on again.
        Assert.Equal(2, hidden.Legend.Count);
    }

    [Fact]
    public void Pie_slices_cover_the_circle()
    {
        var slices = new[] { new ChartSlice("A", 1), new ChartSlice("B", 2), new ChartSlice("C", 5) };

        var arcs = SliceLayouts.Pie(Context(), slices, 0, null, null).Prims.OfType<ArcPrim>().ToList();

        Assert.Equal(3, arcs.Count);
        Assert.Equal(360, arcs.Sum(a => a.SweepAngle), 6);
        Assert.Equal(45, arcs[0].SweepAngle, 6);
        Assert.Equal(arcs[0].SweepAngle, arcs[1].StartAngle, 6);
    }

    [Fact]
    public void Waterfall_keeps_a_running_total()
    {
        var steps = new[] { new WaterfallStep("Start", 100), new WaterfallStep("In", 50), new WaterfallStep("Out", -70), new WaterfallStep("End", 0, true) };

        var bars = XYLayouts.Running(steps);

        Assert.Equal([(0.0, 100.0), (100.0, 150.0), (150.0, 80.0), (0.0, 80.0)], bars);
    }

    [Fact]
    public void Histogram_bins_have_round_edges_and_count_every_value()
    {
        double[] values = [1, 2, 2, 3, 7, 8, 9, 9.5, 10];

        var bins = HistogramBinner.Bin(values, binCount: 5);

        Assert.Equal(0, bins[0].From);
        Assert.All(bins, b => Assert.Equal(2, b.To - b.From, 9));
        Assert.Equal(values.Length, bins.Sum(b => b.Count));
        // The maximum lands in the last bin.
        Assert.True(bins[^1].To >= 10);
        Assert.Equal(1, bins.First(b => b.From == 0).Count);
    }

    [Fact]
    public void Histogram_of_equal_values_is_one_bin()
    {
        var bins = HistogramBinner.Bin([4, 4, 4]);

        Assert.Single(bins);
        Assert.Equal(3, bins[0].Count);
    }

    [Fact]
    public void Gantt_groups_tasks_and_draws_milestones_and_dependencies()
    {
        var start = new DateTime(2026, 1, 5);
        GanttTask[] tasks =
        [
            new("Design", start, start.AddDays(5), "A"),
            new("Build", start.AddDays(5), start.AddDays(15), "B") { DependsOn = "Design", Progress = 0.5 },
            new("Ship", start.AddDays(16), start.AddDays(16), "B"),
        ];

        var scene = ProgressLayouts.Gantt(Context(), tasks, today: null);

        Assert.Equal(3, scene.Items.Count);
        Assert.Contains(scene.Prims.OfType<PathPrim>(), p => p.Closed && p.Points.Count == 4 && p.Item >= 0);   // milestone diamond
        Assert.Contains(scene.Prims.OfType<PathPrim>(), p => p.Item < 0 && p.Points.Count == 2 && !p.Closed);   // touching: straight down
        Assert.Contains(scene.Prims.OfType<PathPrim>(), p => p.Item < 0 && p.Points.Count == 3 && p.Closed);    // arrow head
        Assert.Equal(["A", "B"], ProgressLayouts.Groups(tasks));

        // With room between the tasks the arrow goes across and into the start.
        tasks[1].Start = start.AddDays(9);
        var spaced = ProgressLayouts.Gantt(Context(), tasks, today: null);
        Assert.Contains(spaced.Prims.OfType<PathPrim>(), p => p.Item < 0 && p.Points.Count == 4 && !p.Closed);
    }

    [Fact]
    public void Gauge_needle_follows_the_value()
    {
        var low = ProgressLayouts.Gauge(Context(), 0, 0, 100, [new GaugeBand(0, 100)], null, null);
        var high = ProgressLayouts.Gauge(Context(), 100, 0, 100, [new GaugeBand(0, 100)], null, null);

        Point Tip(ChartScene scene) => scene.Prims.OfType<PathPrim>().Single(p => p.Closed && p.Points.Count == 3).Points[0];
        Assert.True(Tip(low).X < Size.Width / 2 - 50);
        Assert.True(Tip(high).X > Size.Width / 2 + 50);
    }

    [Fact]
    public void Data_points_are_equal_by_series_and_index()
    {
        var series = new ChartSeries("S", 1, 2);

        Assert.Equal(new ChartDataPoint(series, 1, "B", 2), new ChartDataPoint(series, 1, "B", 5));
        Assert.NotEqual(new ChartDataPoint(series, 0, "A", 1), new ChartDataPoint(series, 1, "B", 2));
    }

    [Fact]
    public void Collections_report_changes_of_their_items()
    {
        var data = TwoSeries();
        var changes = 0;
        data.Changed += (_, _) => changes++;

        data.Series[0].Values[1] = 42;
        data.Series[1].Name = "Renamed";
        data.Categories.Add("C");

        Assert.Equal(3, changes);
    }

    [Fact]
    public void Empty_charts_say_so()
    {
        var scene = CategoryLayouts.Bars(Context(), null, BarMode.Single, false, false);

        Assert.Contains(scene.Prims.OfType<LabelPrim>(), t => t.Text == RiceStrings.ChartNoData);
    }
}
