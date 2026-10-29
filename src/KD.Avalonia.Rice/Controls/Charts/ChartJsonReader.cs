using System.Globalization;
using System.Text.Json;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls.Charts;

/// <summary>A chart read from JSON, or the problem that stopped it (with its 1-based line when known).</summary>
internal sealed record ChartJsonResult(RiceChartBase? Chart, double? Height, string? Error, int Line = 0);

/// <summary>
/// Reads the JSON of a Markdown <c>chart</c> block into a chart control, e.g.
/// <c>{"type": "stackedColumn", "title": "Sales", "categories": ["Q1", "Q2"], "series": [{"name": "EU", "values": [3, 5]}]}</c>.
/// Property names are case-insensitive; see docs/rice-charts.md for every type.
/// </summary>
internal static class ChartJsonReader
{
    private sealed class ChartJsonException(string message) : Exception(message);

    private static readonly JsonDocumentOptions Options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    public static ChartJsonResult Read(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, Options);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new ChartJsonException("The chart must be a JSON object.");
            var type = String(root, "type") ?? throw new ChartJsonException("Missing \"type\".");
            var chart = Create(Normalize(type), root) ?? throw new ChartJsonException($"Unknown chart type \"{type}\".");
            ApplyCommon(chart, root);
            var height = Number(root, "height");
            return new ChartJsonResult(chart, double.IsFinite(height) && height > 0 ? height : null, null);
        }
        catch (JsonException e)
        {
            return new ChartJsonResult(null, null, StripPosition(e.Message), (int)(e.LineNumber ?? 0) + 1);
        }
        catch (Exception e) when (e is ChartJsonException or FormatException or InvalidOperationException)
        {
            return new ChartJsonResult(null, null, e.Message);
        }
    }

    // "… LineNumber: 3 | BytePositionInLine: 7." – the line is reported separately.
    private static string StripPosition(string message)
    {
        var index = message.IndexOf(" Path:", StringComparison.Ordinal);
        if (index < 0)
            index = message.IndexOf(" LineNumber:", StringComparison.Ordinal);
        return index > 0 ? message[..index] : message;
    }

    private static string Normalize(string type) => new(type.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static RiceChartBase? Create(string type, JsonElement root) => type switch
    {
        "column" => new RiceColumnChart { Data = Categories(root) },
        "clusteredcolumn" or "groupedcolumn" => new RiceClusteredColumnChart { Data = Categories(root) },
        "stackedcolumn" => new RiceStackedColumnChart { Data = Categories(root), IsPercent = Bool(root, "percent") ?? false },
        "stackedcolumn100" or "percentcolumn" => new RiceStackedColumnChart { Data = Categories(root), IsPercent = true },
        "bar" => new RiceBarChart { Data = Categories(root) },
        "clusteredbar" or "groupedbar" => new RiceClusteredBarChart { Data = Categories(root) },
        "stackedbar" => new RiceStackedBarChart { Data = Categories(root), IsPercent = Bool(root, "percent") ?? false },
        "stackedbar100" or "percentbar" => new RiceStackedBarChart { Data = Categories(root), IsPercent = true },
        "divergingbar" or "diverging" => new RiceDivergingBarChart { Data = Categories(root) },
        "line" => new RiceLineChart { Data = Categories(root), IsSmooth = Bool(root, "smooth") ?? false, ShowMarkers = Bool(root, "markers") ?? true },
        "area" => new RiceAreaChart { Data = Categories(root), IsSmooth = Bool(root, "smooth") ?? false, IsStacked = Bool(root, "stacked") ?? false },
        "stackedarea" => new RiceAreaChart { Data = Categories(root), IsSmooth = Bool(root, "smooth") ?? false, IsStacked = true },
        "radar" or "spider" => new RiceRadarChart { Data = Categories(root), IsFilled = Bool(root, "filled") ?? true },
        "pie" => new RicePieChart { Slices = Slices(root) },
        "donut" or "doughnut" => Donut(root),
        "pyramid" => new RicePyramidChart { Slices = Slices(root) },
        "funnel" => new RiceFunnelChart { Slices = Slices(root) },
        "scatter" => new RiceScatterChart { Series = PointSeries(root) },
        "bubble" => new RiceBubbleChart { Series = PointSeries(root) },
        "heatmap" => new RiceHeatmapChart { Data = Heatmap(root) },
        "waterfall" => new RiceWaterfallChart { Steps = Steps(root) },
        "histogram" => Histogram(root),
        "gantt" => new RiceGanttChart { Tasks = Tasks(root), ShowToday = Bool(root, "today") ?? true },
        "bullet" => new RiceBulletChart { Items = Bullets(root) },
        "gauge" => Gauge(root),
        _ => null,
    };

    private static void ApplyCommon(RiceChartBase chart, JsonElement root)
    {
        if (String(root, "title") is { } title)
            chart.Title = title;
        if (String(root, "format") is { } format)
            chart.ValueFormat = format;
        if (Bool(root, "values") is { } values)
            chart.ShowValues = values;
        if (Bool(root, "animated") is { } animated)
            chart.IsAnimated = animated;
        if (Get(root, "legend") is { } legend)
        {
            if (legend.ValueKind is JsonValueKind.True or JsonValueKind.False)
                chart.ShowLegend = legend.GetBoolean();
            else if (legend.ValueKind == JsonValueKind.String)
            {
                var text = legend.GetString() ?? "";
                if (text.Equals("none", StringComparison.OrdinalIgnoreCase) || text.Equals("hidden", StringComparison.OrdinalIgnoreCase))
                    chart.ShowLegend = false;
                else if (Enum.TryParse<ChartLegendPosition>(text, true, out var position))
                    chart.LegendPosition = position;
                else
                    throw new ChartJsonException($"Unknown legend position \"{text}\" (bottom, top, left, right, none).");
            }
        }
    }

    // ---- data ----

    private static ChartCategoryData Categories(JsonElement root)
    {
        var data = new ChartCategoryData();
        data.Categories.Reset(Strings(root, "categories") ?? throw new ChartJsonException("Missing \"categories\"."));
        if (Get(root, "series") is { ValueKind: JsonValueKind.Array } series)
            foreach (var s in series.EnumerateArray())
                data.Series.Add(new ChartSeries(String(s, "name") ?? "", Numbers(s, "values") ?? []) { Color = Color(s) });
        else if (Numbers(root, "values") is { } values)
            data.Series.Add(new ChartSeries(String(root, "name") ?? String(root, "title") ?? "", values) { Color = Color(root) });
        else
            throw new ChartJsonException("Missing \"series\" (or \"values\").");
        return data;
    }

    private static ChartCollection<ChartSlice> Slices(JsonElement root)
    {
        var slices = new ChartCollection<ChartSlice>();
        if (Get(root, "slices") is { ValueKind: JsonValueKind.Array } array)
            foreach (var s in array.EnumerateArray())
                slices.Add(new ChartSlice(String(s, "label") ?? String(s, "name") ?? "", Number(s, "value")) { Color = Color(s) });
        else if (Strings(root, "categories") is { } labels && Numbers(root, "values") is { } values)
            for (var i = 0; i < labels.Count; i++)
                slices.Add(new ChartSlice(labels[i], i < values.Count ? values[i] : 0));
        else if (Get(root, "data") is { ValueKind: JsonValueKind.Object } map)
            foreach (var property in map.EnumerateObject())
                slices.Add(new ChartSlice(property.Name, property.Value.ValueKind == JsonValueKind.Number ? property.Value.GetDouble() : 0));
        else
            throw new ChartJsonException("Missing \"slices\" (or \"categories\" and \"values\").");
        return slices;
    }

    private static RiceDonutChart Donut(JsonElement root)
    {
        var chart = new RiceDonutChart { Slices = Slices(root), CenterText = String(root, "centerText"), CenterLabel = String(root, "centerLabel") };
        if (Number(root, "innerRadius") is var inner && double.IsFinite(inner))
            chart.InnerRadiusRatio = inner;
        return chart;
    }

    private static ChartCollection<ChartPointSeries> PointSeries(JsonElement root)
    {
        var result = new ChartCollection<ChartPointSeries>();
        if (Get(root, "series") is { ValueKind: JsonValueKind.Array } series)
            foreach (var s in series.EnumerateArray())
                result.Add(new ChartPointSeries(String(s, "name") ?? "", Points(s)) { Color = Color(s) });
        else
            result.Add(new ChartPointSeries(String(root, "name") ?? "", Points(root)));
        return result;
    }

    private static List<ChartPoint> Points(JsonElement element)
    {
        if (Get(element, "points") is not { ValueKind: JsonValueKind.Array } points)
            throw new ChartJsonException("Missing \"points\".");
        var result = new List<ChartPoint>();
        foreach (var p in points.EnumerateArray())
        {
            if (p.ValueKind == JsonValueKind.Array)
            {
                var n = p.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.Number ? v.GetDouble() : double.NaN).ToList();
                if (n.Count < 2)
                    throw new ChartJsonException("A point needs [x, y] or [x, y, size].");
                result.Add(new ChartPoint(n[0], n[1], n.Count > 2 ? n[2] : 0));
            }
            else
                result.Add(new ChartPoint(Number(p, "x"), Number(p, "y"), Number(p, "size") is var size && double.IsFinite(size) ? size : 0, String(p, "label")));
        }
        return result;
    }

    private static HeatmapData Heatmap(JsonElement root)
    {
        var data = new HeatmapData();
        data.ColumnLabels.Reset(Strings(root, "columns") ?? Strings(root, "categories") ?? []);
        if (Get(root, "rows") is not { ValueKind: JsonValueKind.Array } rows)
            throw new ChartJsonException("Missing \"rows\".");
        foreach (var r in rows.EnumerateArray())
            data.Rows.Add(new HeatmapRow(String(r, "label") ?? String(r, "name") ?? "", Numbers(r, "values") ?? []));
        return data;
    }

    private static ChartCollection<WaterfallStep> Steps(JsonElement root)
    {
        if (Get(root, "steps") is not { ValueKind: JsonValueKind.Array } steps)
            throw new ChartJsonException("Missing \"steps\".");
        return new ChartCollection<WaterfallStep>(steps.EnumerateArray().Select(s =>
            new WaterfallStep(String(s, "label") ?? "", Number(s, "value") is var v && double.IsFinite(v) ? v : 0, Bool(s, "total") ?? false)));
    }

    private static RiceHistogramChart Histogram(JsonElement root)
    {
        var chart = new RiceHistogramChart { Values = Numbers(root, "values") ?? throw new ChartJsonException("Missing \"values\".") };
        if (Number(root, "bins") is var bins && double.IsFinite(bins))
            chart.BinCount = (int)bins;
        if (Number(root, "binWidth") is var width && double.IsFinite(width))
            chart.BinWidth = width;
        return chart;
    }

    private static ChartCollection<GanttTask> Tasks(JsonElement root)
    {
        if (Get(root, "tasks") is not { ValueKind: JsonValueKind.Array } tasks)
            throw new ChartJsonException("Missing \"tasks\".");
        var result = new ChartCollection<GanttTask>();
        foreach (var t in tasks.EnumerateArray())
        {
            var name = String(t, "name") ?? "";
            var start = Date(t, "start") ?? throw new ChartJsonException($"Task \"{name}\" has no \"start\".");
            var end = Date(t, "end") ?? (Number(t, "days") is var days && double.IsFinite(days) ? start.AddDays(days) : start);
            result.Add(new GanttTask(name, start, end, String(t, "group"))
            {
                Progress = Number(t, "progress") is var progress && double.IsFinite(progress) ? progress : 0,
                DependsOn = String(t, "dependsOn") ?? String(t, "after"),
                Color = Color(t),
            });
        }
        return result;
    }

    private static ChartCollection<BulletItem> Bullets(JsonElement root)
    {
        if (Get(root, "items") is not { ValueKind: JsonValueKind.Array } items)
            throw new ChartJsonException("Missing \"items\".");
        return new ChartCollection<BulletItem>(items.EnumerateArray().Select(b =>
            new BulletItem(String(b, "label") ?? "", Number(b, "value"), Number(b, "target"), Numbers(b, "ranges") ?? [])
            {
                Maximum = Number(b, "max"),
            }));
    }

    private static RiceGaugeChart Gauge(JsonElement root)
    {
        var chart = new RiceGaugeChart { Label = String(root, "label"), ValueText = String(root, "text") };
        if (Number(root, "min") is var min && double.IsFinite(min))
            chart.Minimum = min;
        if (Number(root, "max") is var max && double.IsFinite(max))
            chart.Maximum = max;
        chart.Value = Number(root, "value") is var value && double.IsFinite(value) ? value : throw new ChartJsonException("Missing \"value\".");
        if (Get(root, "bands") is { ValueKind: JsonValueKind.Array } bands)
            chart.Bands = new ChartCollection<GaugeBand>(bands.EnumerateArray().Select(b =>
                new GaugeBand(Number(b, "from"), Number(b, "to"), Color(b), String(b, "label"))));
        return chart;
    }

    // ---- values ----

    private static JsonElement? Get(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;
        foreach (var property in element.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        return null;
    }

    private static string? String(JsonElement element, string name) => Get(element, name) switch
    {
        { ValueKind: JsonValueKind.String } s => s.GetString(),
        { ValueKind: JsonValueKind.Number } n => n.GetRawText(),
        _ => null,
    };

    private static double Number(JsonElement element, string name) => Get(element, name) switch
    {
        { ValueKind: JsonValueKind.Number } n => n.GetDouble(),
        { ValueKind: JsonValueKind.String } s when double.TryParse(s.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) => v,
        _ => double.NaN,
    };

    private static bool? Bool(JsonElement element, string name) => Get(element, name) switch
    {
        { ValueKind: JsonValueKind.True } => true,
        { ValueKind: JsonValueKind.False } => false,
        _ => null,
    };

    private static List<string>? Strings(JsonElement element, string name) =>
        Get(element, name) is { ValueKind: JsonValueKind.Array } array
            ? array.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.GetRawText()).ToList()
            : null;

    private static List<double>? Numbers(JsonElement element, string name) =>
        Get(element, name) is { ValueKind: JsonValueKind.Array } array
            ? array.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.Number ? v.GetDouble() : double.NaN).ToList()
            : null;

    private static Color? Color(JsonElement element) =>
        String(element, "color") is { } text
            ? global::Avalonia.Media.Color.TryParse(text, out var color) ? color : throw new ChartJsonException($"Unknown color \"{text}\".")
            : null;

    private static DateTime? Date(JsonElement element, string name) =>
        String(element, name) is { } text
            ? DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : throw new ChartJsonException($"Unknown date \"{text}\" (use yyyy-MM-dd).")
            : null;
}
