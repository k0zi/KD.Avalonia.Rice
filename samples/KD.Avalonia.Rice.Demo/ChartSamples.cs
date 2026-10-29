using Avalonia.Media;
using KD.Avalonia.Rice.Controls.Charts;

namespace KD.Avalonia.Rice.Demo;

/// <summary>A sample of every chart type; the random source varies the data ("Randomize" in the demo).</summary>
public static class ChartSamples
{
    public sealed record Sample(string Name, string Description, Func<Random, RiceChartBase> Create)
    {
        public override string ToString() => Name;
    }

    private static readonly string[] Months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
    private static readonly string[] Quarters = ["Q1", "Q2", "Q3", "Q4"];
    private static readonly string[] Regions = ["Europe", "Americas", "Asia"];

    private static double Around(Random random, double value, double spread = 0.25) =>
        Math.Round(value * (1 + (random.NextDouble() * 2 - 1) * spread));

    private static ChartSeries Series(Random random, string name, params double[] values) =>
        new(name, values.Select(v => Around(random, v)));

    private static ChartCategoryData Sales(Random random) => new(Quarters,
        Series(random, "Europe", 42, 51, 48, 63),
        Series(random, "Americas", 35, 39, 46, 52),
        Series(random, "Asia", 22, 30, 37, 41));

    private static ChartCategoryData Monthly(Random random) => new(Months,
        Series(random, "Visitors", 120, 132, 151, 168, 175, 190, 212, 205, 188, 176, 160, 182),
        Series(random, "Sign-ups", 40, 46, 52, 63, 61, 72, 84, 80, 71, 65, 58, 69));

    public static IReadOnlyList<Sample> All { get; } =
    [
        new("Column", "One series, values on the bars", random => new RiceColumnChart
        {
            Title = "Revenue per quarter", ShowValues = true, ValueFormat = "0 'k€'",
            Data = new ChartCategoryData(Quarters, Series(random, "Revenue", 128, 141, 136, 162)),
        }),
        new("Clustered column", "Series side by side", random => new RiceClusteredColumnChart { Title = "Sales by region", Data = Sales(random) }),
        new("Stacked column", "Series on top of each other", random => new RiceStackedColumnChart { Title = "Sales by region", Data = Sales(random) }),
        new("Line", "Smooth lines with markers", random => new RiceLineChart { Title = "Traffic", IsSmooth = true, Data = Monthly(random) }),
        new("Pie", "Shares of a whole", random => new RicePieChart
        {
            Title = "Desktop share",
            Slices = new ChartCollection<ChartSlice>(
            [
                new("Ubuntu", Around(random, 34)), new("Fedora", Around(random, 18)), new("Arch", Around(random, 14)),
                new("Debian", Around(random, 12)), new("Other", Around(random, 9)),
            ]),
        }),
        new("Donut", "The total in the middle", random => new RiceDonutChart
        {
            Title = "Disk usage", ValueFormat = "0 'GB'", CenterLabel = "used",
            Slices = new ChartCollection<ChartSlice>(
            [
                new("System", Around(random, 28)), new("Apps", Around(random, 64)), new("Media", Around(random, 120)),
                new("Documents", Around(random, 22)),
            ]),
        }),
        new("Area", "Stacked areas", random => new RiceAreaChart { Title = "Traffic", IsStacked = true, IsSmooth = true, Data = Monthly(random) }),
        new("Bar", "Horizontal bars", random => new RiceBarChart
        {
            Title = "Stars per project", ShowValues = true,
            Data = new ChartCategoryData(["Avalonia", "Uno", "MAUI", "WinUI", "Eto"], Series(random, "Stars", 27000, 9000, 22000, 6500, 3600)),
        }),
        new("Clustered bar", "Series side by side, horizontal", random => new RiceClusteredBarChart { Title = "Sales by region", Data = Sales(random) }),
        new("Stacked bar 100 %", "Shares per category", random => new RiceStackedBarChart { Title = "Sales mix", IsPercent = true, ShowValues = true, Data = Sales(random) }),
        new("Bubble", "x, y and size", random => new RiceBubbleChart
        {
            Title = "Countries",
            Series = new ChartCollection<ChartPointSeries>(
            [
                new("Europe", Enumerable.Range(0, 6).Select(i => new ChartPoint(Around(random, 30 + i * 6), Around(random, 72 + i), Around(random, 10 + i * 12), $"EU {i + 1}"))),
                new("Asia", Enumerable.Range(0, 6).Select(i => new ChartPoint(Around(random, 10 + i * 7), Around(random, 66 + i * 1.5), Around(random, 20 + i * 25), $"AS {i + 1}"))),
            ]),
        }),
        new("Radar", "Several measures at a glance", random => new RiceRadarChart
        {
            Title = "Framework comparison",
            Data = new ChartCategoryData(["Speed", "Docs", "Community", "Tooling", "Platforms", "Stability"],
                Series(random, "Avalonia", 8, 7, 8, 7, 9, 8), Series(random, "Other", 6, 8, 6, 8, 5, 7)),
        }),
        new("Scatter", "Two measures per item", random => new RiceScatterChart
        {
            Title = "Height and weight",
            Series = new ChartCollection<ChartPointSeries>(
            [
                new("Group A", Enumerable.Range(0, 24).Select(_ => Point(random, 172, 70))),
                new("Group B", Enumerable.Range(0, 24).Select(_ => Point(random, 162, 58))),
            ]),
        }),
        new("Heatmap", "Values as color", random => new RiceHeatmapChart
        {
            Title = "Commits per weekday and hour", ShowValues = true,
            Data = new HeatmapData(["8", "10", "12", "14", "16", "18", "20"],
                new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }.Select((day, d) =>
                    new HeatmapRow(day, Enumerable.Range(0, 7).Select(h => Math.Max(0, Around(random, (d < 5 ? 12 : 3) + (h is > 0 and < 5 ? 8 : 0), 0.6)))))),
        }),
        new("Pyramid", "Layers of a whole", random => new RicePyramidChart
        {
            Title = "Test pyramid",
            Slices = new ChartCollection<ChartSlice>([new("End-to-end", Around(random, 10)), new("Integration", Around(random, 30)), new("Unit", Around(random, 60))]),
        }),
        new("Funnel", "Stages of a process", random => new RiceFunnelChart
        {
            Title = "Downloads to users",
            Slices = new ChartCollection<ChartSlice>(
            [
                new("Page views", Around(random, 12000, 0.1)), new("Downloads", Around(random, 5400, 0.1)), new("Installs", Around(random, 3900, 0.1)),
                new("First run", Around(random, 3100, 0.1)), new("Active users", Around(random, 1700, 0.1)),
            ]),
        }),
        new("Waterfall", "Running total of changes", random => new RiceWaterfallChart
        {
            Title = "Cash flow", ShowValues = true,
            Steps = new ChartCollection<WaterfallStep>(
            [
                new("Start", Around(random, 120)), new("Sales", Around(random, 80)), new("Services", Around(random, 35)),
                new("Salaries", -Around(random, 95)), new("Rent", -Around(random, 30)), new("Q1", 0, isTotal: true),
                new("Sales", Around(random, 70)), new("Taxes", -Around(random, 40)), new("End", 0, isTotal: true),
            ]),
        }),
        new("Gantt", "Tasks on a time line", random => new RiceGanttChart
        {
            Title = "Release plan",
            Tasks = new ChartCollection<GanttTask>(Plan(random)),
        }),
        new("Histogram", "Distribution of values", random => new RiceHistogramChart
        {
            Title = "Response times (ms)",
            Values = new ChartCollection<double>(Enumerable.Range(0, 400).Select(_ => Math.Round(Normal(random, 180, 35)))),
        }),
        new("Bullet", "Measures against targets", random => new RiceBulletChart
        {
            Title = "Quarter goals",
            Items = new ChartCollection<BulletItem>(
            [
                new("Revenue", Around(random, 270, 0.15), 250, 150, 225, 300),
                new("Profit", Around(random, 22, 0.3), 26, 10, 20, 30),
                new("Customers", Around(random, 1400, 0.2), 1600, 1000, 1500, 2000),
            ]),
        }),
        new("Gauge", "One value on a dial", random => new RiceGaugeChart
        {
            Title = "CPU temperature", Value = Around(random, 62, 0.3), Minimum = 20, Maximum = 100, ValueFormat = "0 '°C'", Label = "Package",
            Bands = new ChartCollection<GaugeBand>(
            [
                new(20, 60, Color.Parse("#36B37E"), "Normal"), new(60, 80, Color.Parse("#FFAB00"), "Warm"), new(80, 100, Color.Parse("#FF5630"), "Hot"),
            ]),
        }),
        new("Diverging bar", "Negative left, positive right", random => new RiceDivergingBarChart
        {
            Title = "Survey: the docs are clear",
            Data = new ChartCategoryData(["Beginners", "Hobbyists", "Professionals", "Students"],
                Series(random, "Disagree", -18, -12, -9, -15), Series(random, "Somewhat disagree", -14, -10, -8, -12),
                Series(random, "Somewhat agree", 30, 34, 31, 29), Series(random, "Agree", 22, 33, 45, 27)),
        }),
    ];

    private static ChartPoint Point(Random random, double height, double weight)
    {
        var h = Normal(random, height, 7);
        return new ChartPoint(Math.Round(h), Math.Round(weight + (h - height) * 0.7 + Normal(random, 0, 5)));
    }

    // Box–Muller.
    private static double Normal(Random random, double mean, double deviation) =>
        mean + deviation * Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());

    private static IEnumerable<GanttTask> Plan(Random random)
    {
        var start = DateTime.Today.AddDays(-12);
        DateTime Day(int offset) => start.AddDays(offset);
        double Done(double value) => Math.Clamp(value + (random.NextDouble() - 0.5) * 0.3, 0, 1);
        return
        [
            new("Specification", Day(0), Day(6), "Design") { Progress = 1 },
            new("Mockups", Day(4), Day(11), "Design") { Progress = Done(0.9), DependsOn = "Specification" },
            new("Charts", Day(9), Day(24), "Build") { Progress = Done(0.45), DependsOn = "Mockups" },
            new("Markdown blocks", Day(14), Day(22), "Build") { Progress = Done(0.3) },
            new("Tests", Day(18), Day(28), "Build") { Progress = Done(0.1), DependsOn = "Markdown blocks" },
            new("Docs", Day(24), Day(31), "Release"),
            new("Release", Day(32), Day(32), "Release") { DependsOn = "Docs" },
        ];
    }
}
