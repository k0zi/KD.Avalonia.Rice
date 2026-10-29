using KD.Avalonia.Rice.Controls.Charts;

namespace KD.Avalonia.Rice.Tests;

public class ChartJsonReaderTests
{
    public static TheoryData<string, Type> ValidCharts => new()
    {
        { """{"type": "column", "categories": ["A", "B"], "values": [1, 2]}""", typeof(RiceColumnChart) },
        { """{"type": "Clustered Column", "categories": ["A"], "series": [{"name": "X", "values": [1]}, {"name": "Y", "values": [2]}]}""", typeof(RiceClusteredColumnChart) },
        { """{"type": "stackedColumn", "percent": true, "categories": ["A"], "series": [{"name": "X", "values": [1]}]}""", typeof(RiceStackedColumnChart) },
        { """{"type": "line", "smooth": true, "categories": ["A", "B"], "values": [1, 2]}""", typeof(RiceLineChart) },
        { """{"type": "pie", "slices": [{"label": "A", "value": 1, "color": "#ff0000"}]}""", typeof(RicePieChart) },
        { """{"type": "donut", "data": {"A": 1, "B": 2}, "centerText": "3"}""", typeof(RiceDonutChart) },
        { """{"type": "area", "stacked": true, "categories": ["A"], "values": [1]}""", typeof(RiceAreaChart) },
        { """{"type": "bar", "categories": ["A"], "values": [1]}""", typeof(RiceBarChart) },
        { """{"type": "clustered-bar", "categories": ["A"], "values": [1]}""", typeof(RiceClusteredBarChart) },
        { """{"type": "stacked_bar", "categories": ["A"], "values": [1]}""", typeof(RiceStackedBarChart) },
        { """{"type": "bubble", "series": [{"name": "S", "points": [[1, 2, 3], {"x": 2, "y": 3, "size": 4, "label": "P"}]}]}""", typeof(RiceBubbleChart) },
        { """{"type": "radar", "categories": ["A", "B", "C"], "values": [1, 2, 3]}""", typeof(RiceRadarChart) },
        { """{"type": "scatter", "points": [[1, 2], [3, 4]]}""", typeof(RiceScatterChart) },
        { """{"type": "heatmap", "columns": ["x", "y"], "rows": [{"label": "r", "values": [1, null]}]}""", typeof(RiceHeatmapChart) },
        { """{"type": "pyramid", "categories": ["A", "B"], "values": [1, 2]}""", typeof(RicePyramidChart) },
        { """{"type": "funnel", "slices": [{"label": "A", "value": 10}, {"label": "B", "value": 5}]}""", typeof(RiceFunnelChart) },
        { """{"type": "waterfall", "steps": [{"label": "A", "value": 5}, {"label": "T", "total": true}]}""", typeof(RiceWaterfallChart) },
        { """{"type": "gantt", "tasks": [{"name": "A", "start": "2026-01-05", "days": 3}, {"name": "B", "start": "2026-01-08", "end": "2026-01-10", "after": "A"}]}""", typeof(RiceGanttChart) },
        { """{"type": "histogram", "values": [1, 2, 2, 3], "bins": 3}""", typeof(RiceHistogramChart) },
        { """{"type": "treemap", "nodes": [{"label": "A", "children": [{"label": "A1", "value": 3}, {"label": "A2", "value": 1}]}, {"label": "B", "value": 2, "color": "#00ff00"}]}""", typeof(RiceTreeMapChart) },
        { """{"type": "Tree Map", "data": {"A": {"A1": 3, "A2": 1}, "B": 2}}""", typeof(RiceTreeMapChart) },
        { """{"type": "bullet", "items": [{"label": "A", "value": 5, "target": 7, "ranges": [3, 6, 9]}]}""", typeof(RiceBulletChart) },
        { """{"type": "gauge", "value": 42, "max": 50, "bands": [{"from": 0, "to": 40}, {"from": 40, "to": 50, "color": "red"}]}""", typeof(RiceGaugeChart) },
        { """{"type": "divergingBar", "categories": ["A"], "series": [{"name": "No", "values": [-1]}, {"name": "Yes", "values": [2]}]}""", typeof(RiceDivergingBarChart) },
    };

    [Theory]
    [MemberData(nameof(ValidCharts))]
    public void Reads_every_chart_type(string json, Type type)
    {
        var result = ChartJsonReader.Read(json);

        Assert.Null(result.Error);
        Assert.IsType(type, result.Chart);
    }

    [Fact]
    public void Reads_the_common_options()
    {
        var result = ChartJsonReader.Read("""
            {
              // comments and trailing commas are fine
              "type": "column", "title": "Sales", "format": "N1", "legend": "right", "height": 240, "values": true,
              "categories": ["A"], "series": [{"name": "X", "values": [1], "color": "#336699"}],
            }
            """);

        var chart = Assert.IsType<RiceColumnChart>(result.Chart);
        Assert.Equal("Sales", chart.Title);
        Assert.Equal("N1", chart.ValueFormat);
        Assert.Equal(ChartLegendPosition.Right, chart.LegendPosition);
        Assert.True(chart.ShowValues);
        Assert.Equal(240, result.Height);
        Assert.Equal(global::Avalonia.Media.Color.Parse("#336699"), chart.Data!.Series[0].Color);
    }

    [Fact]
    public void Bad_json_reports_its_line()
    {
        var result = ChartJsonReader.Read("{\n  \"type\": \"pie\",\n  \"slices\": [ oops ]\n}");

        Assert.Null(result.Chart);
        Assert.NotNull(result.Error);
        Assert.Equal(3, result.Line);
    }

    [Theory]
    [InlineData("""{"categories": ["A"]}""", "type")]
    [InlineData("""{"type": "sunburst"}""", "sunburst")]
    [InlineData("""{"type": "column", "values": [1]}""", "categories")]
    [InlineData("""{"type": "gantt", "tasks": [{"name": "A"}]}""", "start")]
    [InlineData("""{"type": "pie", "slices": [{"label": "A", "value": 1, "color": "nocolor"}]}""", "nocolor")]
    [InlineData("""[1, 2]""", "object")]
    public void Missing_or_wrong_content_is_a_message_not_an_exception(string json, string mentioned)
    {
        var result = ChartJsonReader.Read(json);

        Assert.Null(result.Chart);
        Assert.Contains(mentioned, result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
