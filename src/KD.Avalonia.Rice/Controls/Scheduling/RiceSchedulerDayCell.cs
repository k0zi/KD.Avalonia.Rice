using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace KD.Avalonia.Rice.Controls.Scheduling;

/// <summary>
/// The background of one day of a <see cref="RiceScheduler"/> row: the day number (month view) and the grid lines.
/// Pseudo classes: <c>:today</c>, <c>:other-month</c>, <c>:weekend</c>, <c>:drop-target</c> (a dragged event would land here).
/// </summary>
public class RiceSchedulerDayCell : TemplatedControl
{
    public static readonly StyledProperty<bool> ShowDayNumberProperty =
        AvaloniaProperty.Register<RiceSchedulerDayCell, bool>(nameof(ShowDayNumber), true);

    internal RiceSchedulerDayCell(DateTime date, bool otherMonth)
    {
        Date = date.Date;
        DayNumber = date.Day.ToString(CultureInfo.CurrentCulture);
        PseudoClasses.Set(":today", Date == DateTime.Today);
        PseudoClasses.Set(":other-month", otherMonth);
        PseudoClasses.Set(":weekend", date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
    }

    public DateTime Date { get; }

    public string DayNumber { get; }

    public bool ShowDayNumber
    {
        get => GetValue(ShowDayNumberProperty);
        set => SetValue(ShowDayNumberProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceSchedulerDayCell);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        e.NameScope.Find<TextBlock>("PART_DayNumber")?.Text = DayNumber;
    }

    internal void SetDropTarget(bool target) => PseudoClasses.Set(":drop-target", target);
}
