using Avalonia.Controls;
using Avalonia.Interactivity;
using KD.Avalonia.Rice.Controls;
using KD.Avalonia.Rice.Controls.Charts;

namespace KD.Avalonia.Rice.Demo;

public partial class ChartsPage : UserControl
{
    private readonly Random _random = new(7);
    private readonly List<RiceCard> _cards = [];

    public ChartsPage()
    {
        InitializeComponent();
        foreach (var sample in ChartSamples.All)
        {
            var card = new RiceCard { Header = sample.Name, Subtitle = sample.Description, Width = 440, Height = 360, Tag = sample };
            _cards.Add(card);
            Gallery.Children.Add(card);
        }
        Fill();
        AnimateSwitch.IsCheckedChanged += (_, _) =>
        {
            foreach (var card in _cards)
                if (card.Content is RiceChartBase chart)
                    chart.IsAnimated = AnimateSwitch.IsChecked == true;
        };
        StatusText.Text = "Nothing selected";
    }

    private void OnRandomizeClicked(object? sender, RoutedEventArgs e) => Fill();

    // New charts with new data: they play the load animation.
    private void Fill()
    {
        foreach (var card in _cards)
        {
            var sample = (ChartSamples.Sample)card.Tag!;
            var chart = sample.Create(_random);
            chart.Title = null;
            chart.IsAnimated = AnimateSwitch.IsChecked == true;
            // The lines between slices are the color of the card.
            chart.Bind(RiceChartBase.SeparatorBrushProperty, chart.GetResourceObservable("RiceSurfaceBrush"));
            chart.SelectionChanged += (_, args) => StatusText.Text = args.NewItem is null
                ? "Nothing selected"
                : $"{sample.Name}: {Describe(args.NewItem)}";
            card.Content = chart;
        }
    }

    private static string Describe(object item) => item switch
    {
        ChartDataPoint p => $"{p.Series.Name} · {p.Category} = {p.Value}",
        ChartSlice s => $"{s.Label} = {s.Value}",
        ChartPoint p => $"{p.Label ?? "point"} ({p.X}, {p.Y}{(p.Size != 0 ? $", {p.Size}" : "")})",
        HeatmapCell c => $"{c.Row.Label} · {c.ColumnLabel} = {c.Value}",
        WaterfallStep s => $"{s.Label} = {s.Value}",
        HistogramBin b => $"[{b.From} – {b.To}) × {b.Count}",
        GanttTask t => $"{t.Name}: {t.Start:d} – {t.End:d}",
        BulletItem b => $"{b.Label} = {b.Value} (target {b.Target})",
        GaugeBand b => $"{b.Label}: {b.From} – {b.To}",
        _ => item.ToString() ?? "",
    };
}
