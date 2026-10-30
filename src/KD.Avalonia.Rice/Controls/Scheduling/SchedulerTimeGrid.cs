using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace KD.Avalonia.Rice.Controls.Scheduling;

/// <summary>
/// The hours of the week and day views: hour labels in a gutter, hour and half hour lines, shaded hours outside the
/// working day, today's column tinted, a line at the current time, and the timed events as blocks (overlapping events
/// side by side).
/// </summary>
internal sealed class SchedulerTimeGrid : Panel
{
    public const double GutterWidth = 56;

    private readonly RiceScheduler _owner;
    private readonly IReadOnlyList<DateTime> _days;
    private readonly Dictionary<RiceSchedulerEventChip, (int Day, TimedPlacement Place)> _chips = [];
    private (RiceSchedulerEventChip Chip, int Day, double Start, double End)? _preview;
    private readonly GridLines _lines;
    private DispatcherTimer? _clock;

    public SchedulerTimeGrid(RiceScheduler owner, IReadOnlyList<DateTime> days)
    {
        _owner = owner;
        _days = days;
        ClipToBounds = true;
        Background = Brushes.Transparent;
        // Panel.Render is sealed: the lines are drawn by a child under the events.
        _lines = new GridLines(this) { IsHitTestVisible = false };
        Children.Add(_lines);
    }

    public IReadOnlyList<DateTime> Days => _days;

    private double HourHeight => _owner.HourHeight;

    public void Add(RiceSchedulerEventChip chip, int day, TimedPlacement place)
    {
        _chips[chip] = (day, place);
        Children.Add(chip);
    }

    /// <summary>Shows <paramref name="chip"/> at a new time while it is dragged; null ends the preview.</summary>
    public void SetPreview(RiceSchedulerEventChip? chip, int day = 0, double start = 0, double end = 0)
    {
        _preview = chip is null ? null : (chip, day, start, end);
        InvalidateArrange();
    }

    public double ColumnWidth => Math.Max(1, (Bounds.Width - GutterWidth) / Math.Max(1, _days.Count));

    /// <summary>The day column (clamped) and the minute of the day at a point of the grid.</summary>
    public (int Day, double Minute) TimeAt(Point point)
    {
        var day = (int)Math.Floor((point.X - GutterWidth) / ColumnWidth);
        return (Math.Clamp(day, 0, _days.Count - 1), Math.Clamp(point.Y / HourHeight * 60, 0, 24 * 60));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
            child.Measure(new Size(double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width, HourHeight * 24));
        return new Size(0, HourHeight * 24);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columnWidth = Math.Max(1, (finalSize.Width - GutterWidth) / Math.Max(1, _days.Count));
        _lines.Arrange(new Rect(finalSize));
        foreach (var (chip, (day, place)) in _chips)
        {
            if (_preview is { } preview && preview.Chip == chip)
            {
                chip.Arrange(Block(preview.Day, preview.Start, preview.End, 0, 1, columnWidth));
                continue;
            }
            chip.Arrange(Block(day, place.StartMinute, place.EndMinute, place.Slot, place.SlotCount, columnWidth));
        }
        return finalSize;
    }

    private Rect Block(int day, double start, double end, int slot, int slots, double columnWidth)
    {
        // A little room on the right of every day, to click the empty time next to the events.
        var usable = columnWidth - 8;
        var width = usable / slots;
        var top = start / 60 * HourHeight;
        var height = Math.Max(20, (end - start) / 60 * HourHeight);
        return new Rect(GutterWidth + day * columnWidth + slot * width + 1, top + 1, Math.Max(4, width - 2), height - 2);
    }

    private void RenderLines(DrawingContext context)
    {
        var size = Bounds.Size;
        var columnWidth = ColumnWidth;
        var grid = Brush("RiceSchedulerGridBrush");
        var hourPen = new Pen(Brush("RiceBorderBrush"));
        var halfPen = new Pen(grid);
        var offHours = Brush("RiceSchedulerOffHoursBrush");

        for (var d = 0; d < _days.Count; d++)
        {
            var x = GutterWidth + d * columnWidth;
            var day = _days[d];
            if (day == DateTime.Today && Brush("RiceSchedulerTodayBrush") is { } today)
                context.FillRectangle(today, new Rect(x, 0, columnWidth, size.Height));
            if (offHours is not null)
            {
                if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    context.FillRectangle(offHours, new Rect(x, 0, columnWidth, size.Height));
                else
                {
                    context.FillRectangle(offHours, new Rect(x, 0, columnWidth, _owner.WorkdayStartHour * HourHeight));
                    var end = _owner.WorkdayEndHour * HourHeight;
                    context.FillRectangle(offHours, new Rect(x, end, columnWidth, Math.Max(0, size.Height - end)));
                }
            }
            context.DrawLine(hourPen, new Point(Math.Round(x) + 0.5, 0), new Point(Math.Round(x) + 0.5, size.Height));
        }

        var labelBrush = Brush("RiceForegroundMutedBrush");
        var typeface = new Typeface(_owner.FontFamily);
        for (var hour = 0; hour < 24; hour++)
        {
            var y = Math.Round(hour * HourHeight) + 0.5;
            if (hour > 0)
            {
                context.DrawLine(hourPen, new Point(GutterWidth - 6, y), new Point(size.Width, y));
                var label = new FormattedText(HourLabel(hour), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, labelBrush);
                context.DrawText(label, new Point(GutterWidth - 10 - label.Width, y - label.Height / 2));
            }
            var half = Math.Round(hour * HourHeight + HourHeight / 2) + 0.5;
            context.DrawLine(halfPen, new Point(GutterWidth, half), new Point(size.Width, half));
        }

        // The current time: a line over today's column with a dot at its start.
        var todayIndex = _days.ToList().IndexOf(DateTime.Today);
        if (todayIndex >= 0 && Brush("RiceSchedulerNowBrush") is { } now)
        {
            var y = DateTime.Now.TimeOfDay.TotalHours * HourHeight;
            var x = GutterWidth + todayIndex * columnWidth;
            context.DrawLine(new Pen(now, 2), new Point(x, y), new Point(x + columnWidth, y));
            context.DrawEllipse(now, null, new Point(x, y), 5, 5);
        }
    }

    private static string HourLabel(int hour) =>
        DateTime.Today.AddHours(hour).ToString(CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains('t') ? "h tt" : "HH:mm",
            CultureInfo.CurrentCulture);

    private IBrush? Brush(string key) => _owner.Brush(key);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _clock = new DispatcherTimer(TimeSpan.FromMinutes(1), DispatcherPriority.Background, (_, _) => _lines.InvalidateVisual());
        _clock.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _clock?.Stop();
        _clock = null;
    }

    /// <summary>Draws the lines again, e.g. after a theme change.</summary>
    public void Redraw() => _lines.InvalidateVisual();

    private sealed class GridLines(SchedulerTimeGrid grid) : Control
    {
        public override void Render(DrawingContext context) => grid.RenderLines(context);
    }
}
