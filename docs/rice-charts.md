# Charts

22 chart controls, drawn from a typed data model in the colors of the current Rice theme: column, clustered column,
stacked column, line, pie, donut, area, bar, clustered bar, stacked bar, bubble, radar, scatter, heatmap, pyramid,
funnel, waterfall, gantt, histogram, bullet, gauge and diverging bar.

Every chart has:
- **Tooltips:** hover a bar, slice or point to see its value.
- **Legend toggle:** click a legend entry to hide or show that series or slice.
- **Selection:** click to select, through the two-way `SelectedItem`.
- **Load animation:** bars grow, slices open, lines rise and the gauge needle swings.

The charts fill their bounds. When a direction is unbounded (e.g. inside a vertical `StackPanel`), they take
480 × 300.

```xml
xmlns:charts="clr-namespace:KD.Avalonia.Rice.Controls.Charts;assembly=KD.Avalonia.Rice"
```

```xml
<charts:RiceClusteredColumnChart Title="Sales by region" Data="{Binding Sales}" Height="320"
                                 SelectedItem="{Binding SelectedPoint}" />
<charts:RiceDonutChart Slices="{Binding DiskUsage}" ValueFormat="0 'GB'" CenterLabel="used" />
<charts:RiceGaugeChart Value="{Binding Temperature}" Minimum="20" Maximum="100" ValueFormat="0 '°C'" />
```

```csharp
SalesChart.Data = new ChartCategoryData(["Q1", "Q2", "Q3", "Q4"],
    new ChartSeries("Europe", 42, 51, 48, 63),
    new ChartSeries("Americas", 35, 39, 46, 52) { Color = Color.Parse("#8B5CF6") });

// The model is observable: edits redraw the chart (without replaying the animation).
SalesChart.Data.Series[0].Values[3] = 70;
SalesChart.Data.Categories.Add("Q5");
```

The data classes raise `PropertyChanged`. Their collections are `ChartCollection<T>`, an `ObservableCollection<T>`
that also reports changes of the items in it. Properties typed `IList<T>` (`Slices`, `Steps`, `Tasks`, …) take
any list. A `ChartCollection<T>` is followed when it changes; after editing any other list, call `Refresh()`.
Assigning a new data object replays the animation, and so does `Replay()`.

## The controls

| Control | Data | Own properties |
|---------|------|----------------|
| `RiceColumnChart` | `Data` (`ChartCategoryData`), first series | |
| `RiceClusteredColumnChart` | `Data`, series side by side | |
| `RiceStackedColumnChart` | `Data`, series stacked; negative values below zero | `IsPercent` (100 %) |
| `RiceBarChart` | `Data`, first series, horizontal | |
| `RiceClusteredBarChart` | `Data`, side by side, horizontal | |
| `RiceStackedBarChart` | `Data`, stacked, horizontal | `IsPercent` |
| `RiceDivergingBarChart` | `Data`, signed values stacked out from a central zero line, the first negative series outermost (Likert scales) | |
| `RiceLineChart` | `Data`, a line per series | `IsSmooth`, `ShowMarkers` (`true`) |
| `RiceAreaChart` | `Data`, filled lines | `IsSmooth`, `IsStacked` |
| `RiceRadarChart` | `Data`, a polygon per series, a spoke per category (≥ 3) | `IsFilled` (`true`) |
| `RicePieChart` | `Slices` (`IList<ChartSlice>`) | legend on the right by default |
| `RiceDonutChart` | `Slices` | `InnerRadiusRatio` (`0.62`), `CenterText` (default: the total), `CenterLabel` |
| `RicePyramidChart` | `Slices`, layer heights follow the values, first at the top | |
| `RiceFunnelChart` | `Slices`, stage widths follow the values | |
| `RiceScatterChart` | `Series` (`IList<ChartPointSeries>`) | |
| `RiceBubbleChart` | `Series`; the bubble area follows `ChartPoint.Size` | |
| `RiceHeatmapChart` | `Data` (`HeatmapData`: column labels, rows of values; `NaN` = empty) | |
| `RiceWaterfallChart` | `Steps` (`IList<WaterfallStep>`): changes from the running total; `IsTotal` bars from zero | |
| `RiceHistogramChart` | `Values` (`IList<double>`, raw values) | `BinCount` (0 = auto), `BinWidth` (NaN = auto), `Bins` |
| `RiceGanttChart` | `Tasks` (`IList<GanttTask>`): group, start, end, progress, `DependsOn`; `End == Start` is a milestone | `ShowToday` (`true`) |
| `RiceBulletChart` | `Items` (`IList<BulletItem>`): value, target, qualitative `Ranges`, `Maximum` | |
| `RiceGaugeChart` | `Value`, `Minimum` (0), `Maximum` (100), `Bands` (`IList<GaugeBand>`) | `Label`, `ValueText`; without bands an arc fills up to the value |

## Common properties (`RiceChartBase`)

| Property | Default | Description |
|----------|---------|-------------|
| `Title` | `null` | Centered above the chart. |
| `ShowLegend` | `true` | The legend; with a single series it is left out. |
| `LegendPosition` | `Bottom` | `Bottom`, `Top`, `Right`, `Left`. |
| `ShowTooltips` | `true` | The value box under the pointer. |
| `ShowValues` | `false` | Value labels on bars and cells (column, bar, waterfall, histogram, heatmap). |
| `IsSelectionEnabled` | `true` | Click selection. |
| `SelectedItem` | `null` | Two-way. The clicked item; its type depends on the chart, see below. |
| `IsAnimated` | `true` | Load animation. |
| `AnimationDuration` | `0.7 s` | |
| `ValueFormat` | `null` | .NET number format (`N1`, `C0`, `0.0 'kg'`, …); by default large numbers are shortened to k / M / G. |
| `FontSize` | `13` | Base font size. |
| `Foreground`, `MutedForeground`, `AxisBrush`, `GridBrush`, `SeparatorBrush`, `TrackBrush`, `PositiveBrush`, `NegativeBrush`, `HeatLowBrush`, `HeatHighBrush`, `SelectionBrush`, `TooltipBackground`, `TooltipBorderBrush`, `TooltipShadow` | theme | The theme sets them all. `SeparatorBrush` draws the lines between slices; on a card, set it to the card color (`RiceSurfaceBrush`). |

| Member | Description |
|--------|-------------|
| `SelectionChanged` | Routed event with `OldItem` and `NewItem`. |
| `Refresh()` | Lays the chart out again, e.g. after editing a plain list. |
| `Replay()` | Plays the load animation again. |
| `IsSeriesHidden(i)`, `SetSeriesHidden(i, hidden)` | The legend toggles. They are cleared when the data is replaced. |

`SelectedItem` is a model object:

| Charts | `SelectedItem` |
|--------|----------------|
| column, bar, line, area, radar, diverging | `ChartDataPoint` (`Series`, `Index`, `Category`, `Value`). Two points are equal when they have the same series and index. |
| pie, donut, pyramid, funnel | `ChartSlice` |
| scatter, bubble | `ChartPoint` |
| heatmap | `HeatmapCell` (`Row`, `Column`, `ColumnLabel`, `Value`) |
| waterfall | `WaterfallStep` |
| histogram | `HistogramBin` (`From`, `To`, `Count`) |
| gantt | `GanttTask` |
| bullet | `BulletItem` |
| gauge | `GaugeBand`, or the value (`double`) without bands |

## Colors

The series take the theme's chart colors, `RiceChartSeries0Brush` … `RiceChartSeries7Brush`. The first one is the
accent. Use `Color` on a `ChartSeries`, `ChartSlice`, `ChartPointSeries`, `GanttTask` or `GaugeBand` to give it its
own color. The other brushes are:

| Brush | Used for |
|-------|----------|
| `RiceChartGridBrush` | Grid lines |
| `RiceChartAxisBrush` | Zero lines and axes |
| `RiceChartTrackBrush` | Empty part of gauges and bullets |
| `RiceChartPositiveBrush`, `RiceChartNegativeBrush` | Waterfall up and down |
| `RiceChartHeatLowBrush`, `RiceChartHeatHighBrush` | Heatmap scale |

Text on a colored bar or slice is dark or white, whichever reads better on that color.

## Charts in Markdown

`MarkdownView` draws `chart` code blocks as charts. The block is a JSON object with the chart `type` and its data.
Property names are case-insensitive, and comments and trailing commas are allowed.

````markdown
```chart
{
  "type": "stackedColumn",
  "title": "Sales by region",
  "categories": ["Q1", "Q2", "Q3", "Q4"],
  "series": [
    { "name": "Europe", "values": [42, 51, 48, 63] },
    { "name": "Asia", "values": [22, 30, 37, 41], "color": "#8B5CF6" }
  ]
}
```
````

These options work for every type:

| Option | Description |
|--------|-------------|
| `title` | The chart title. |
| `format` | Number format, like `ValueFormat`. |
| `legend` | `true` / `false`, or a position: `bottom`, `top`, `left`, `right`, `none`. |
| `values` | `true` shows value labels. |
| `animated` | `false` turns the load animation off. |
| `height` | Height in the text, 320 by default. |

In type names, case, spaces, dashes and underscores do not matter, so `stackedColumn`, `stacked-column` and
`Stacked Column` are the same type.

| `type` | Data |
|--------|------|
| `column`, `clusteredColumn`, `stackedColumn` (`percent`), `stackedColumn100`, `bar`, `clusteredBar`, `stackedBar` (`percent`), `stackedBar100`, `divergingBar`, `line` (`smooth`, `markers`), `area` (`smooth`, `stacked`), `stackedArea`, `radar` (`filled`) | `categories` and `series: [{name, values, color}]`, or `values` for a single series |
| `pie`, `donut` (`innerRadius`, `centerText`, `centerLabel`), `pyramid`, `funnel` | `slices: [{label, value, color}]`, or `categories` + `values`, or `data: {"label": value}` |
| `scatter`, `bubble` | `series: [{name, points}]` or `points`. A point is `[x, y]`, `[x, y, size]` or `{x, y, size, label}`. |
| `heatmap` | `columns` and `rows: [{label, values}]` (`null` = empty cell) |
| `waterfall` | `steps: [{label, value, total}]` |
| `histogram` | `values`, `bins`, `binWidth` |
| `gantt` (`today`) | `tasks: [{name, group, start, end or days, progress, dependsOn, color}]`, dates as `yyyy-MM-dd` |
| `bullet` | `items: [{label, value, target, ranges, max}]` |
| `gauge` | `value`, `min`, `max`, `label`, `text`, `bands: [{from, to, color, label}]` |

A block that cannot be read is shown as code with the problem above it. The JSON is only a way to put charts into
Markdown: in code, set the model.
