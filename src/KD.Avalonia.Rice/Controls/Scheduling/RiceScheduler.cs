using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace KD.Avalonia.Rice.Controls.Scheduling;

/// <summary>
/// Calendar of events with a month, a week and a day view, like the calendar of a mail client. The events
/// (<see cref="RiceCalendarEvent"/>s in <see cref="ItemsSource"/>) are bars over the days in the month view and in the
/// all day strip, and blocks in the hours of the week and day views, where overlapping events stand side by side.
/// Events are moved by dragging (to another day, or another time in steps of <see cref="SnapMinutes"/>) and resized at
/// their bottom edge; a double click on an empty slot asks for a new event (<see cref="CreateEventRequested"/>).
/// </summary>
public class RiceScheduler : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<RiceScheduler, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<DateTime> DisplayDateProperty =
        AvaloniaProperty.Register<RiceScheduler, DateTime>(nameof(DisplayDate), DateTime.Today,
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<SchedulerViewMode> ViewModeProperty =
        AvaloniaProperty.Register<RiceScheduler, SchedulerViewMode>(nameof(ViewMode),
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<DayOfWeek> FirstDayOfWeekProperty =
        AvaloniaProperty.Register<RiceScheduler, DayOfWeek>(nameof(FirstDayOfWeek), CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);

    public static readonly StyledProperty<RiceCalendarEvent?> SelectedEventProperty =
        AvaloniaProperty.Register<RiceScheduler, RiceCalendarEvent?>(nameof(SelectedEvent),
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> HourHeightProperty =
        AvaloniaProperty.Register<RiceScheduler, double>(nameof(HourHeight), 48, coerce: (_, value) => Math.Clamp(value, 20, 200));

    public static readonly StyledProperty<int> SnapMinutesProperty =
        AvaloniaProperty.Register<RiceScheduler, int>(nameof(SnapMinutes), 15, coerce: (_, value) => Math.Clamp(value, 1, 60));

    public static readonly StyledProperty<int> DayStartHourProperty =
        AvaloniaProperty.Register<RiceScheduler, int>(nameof(DayStartHour), 7, coerce: (_, value) => Math.Clamp(value, 0, 23));

    public static readonly StyledProperty<int> WorkdayStartHourProperty =
        AvaloniaProperty.Register<RiceScheduler, int>(nameof(WorkdayStartHour), 8, coerce: (_, value) => Math.Clamp(value, 0, 24));

    public static readonly StyledProperty<int> WorkdayEndHourProperty =
        AvaloniaProperty.Register<RiceScheduler, int>(nameof(WorkdayEndHour), 18, coerce: (_, value) => Math.Clamp(value, 0, 24));

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<RiceScheduler, bool>(nameof(IsReadOnly));

    public static readonly DirectProperty<RiceScheduler, string> TitleProperty =
        AvaloniaProperty.RegisterDirect<RiceScheduler, string>(nameof(Title), o => o.Title);

    public static readonly RoutedEvent<RiceCalendarEventEventArgs> EventActivatedEvent =
        RoutedEvent.Register<RiceScheduler, RiceCalendarEventEventArgs>(nameof(EventActivated), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RiceCalendarEventChangingEventArgs> EventChangingEvent =
        RoutedEvent.Register<RiceScheduler, RiceCalendarEventChangingEventArgs>(nameof(EventChanging), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RiceCalendarEventEventArgs> EventChangedEvent =
        RoutedEvent.Register<RiceScheduler, RiceCalendarEventEventArgs>(nameof(EventChanged), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RiceCalendarCreateEventArgs> CreateEventRequestedEvent =
        RoutedEvent.Register<RiceScheduler, RiceCalendarCreateEventArgs>(nameof(CreateEventRequested), RoutingStrategies.Bubble);

    private const double DragThreshold = 4;
    private const double ResizeGrip = 6;
    private const double MonthHeaderHeight = 28;

    private readonly Dictionary<string, IBrush?> _brushes = [];
    private readonly List<RiceSchedulerEventChip> _chips = [];
    private readonly List<RiceSchedulerDayCell> _cells = [];
    private readonly HashSet<RiceCalendarEvent> _watched = [];
    private INotifyCollectionChanged? _watchedCollection;
    private Decorator? _host;
    private RiceSegmentedControl? _viewSwitch;
    private SchedulerTimeGrid? _timeGrid;
    private ScrollViewer? _timeScroller;
    private double? _timeScrollOffset;
    private string _title = "";
    private bool _rebuildPosted;
    private DragState? _drag;

    private enum DragMode
    {
        Days,
        Time,
        Resize,
    }

    private sealed class DragState(RiceSchedulerEventChip chip, DragMode mode, Point start)
    {
        public RiceSchedulerEventChip Chip { get; } = chip;
        public DragMode Mode { get; } = mode;
        public Point Start { get; } = start;
        public bool Moved { get; set; }
        public int OriginDay { get; init; }
        public double OriginMinute { get; init; }
        public DateTime? OriginDate { get; init; }
        public RiceSchedulerDayCell? Target { get; set; }
        public DateTime NewStart { get; set; }
        public DateTime NewEnd { get; set; }
    }

    static RiceScheduler()
    {
        FocusableProperty.OverrideDefaultValue<RiceScheduler>(true);
    }

    public RiceScheduler()
    {
        AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Bubble);
        AddHandler(DoubleTappedEvent, OnDoubleTapped, RoutingStrategies.Bubble);
        ActualThemeVariantChanged += (_, _) => ResetBrushes();
        ResourcesChanged += (_, _) => ResetBrushes();
    }

    /// <summary>The events: <see cref="RiceCalendarEvent"/>s (other items are skipped); an observable collection is followed.</summary>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>A day of the shown month / week, or the shown day.</summary>
    public DateTime DisplayDate
    {
        get => GetValue(DisplayDateProperty);
        set => SetValue(DisplayDateProperty, value);
    }

    public SchedulerViewMode ViewMode
    {
        get => GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    /// <summary>The first column of the weeks; defaults to the current culture's.</summary>
    public DayOfWeek FirstDayOfWeek
    {
        get => GetValue(FirstDayOfWeekProperty);
        set => SetValue(FirstDayOfWeekProperty, value);
    }

    public RiceCalendarEvent? SelectedEvent
    {
        get => GetValue(SelectedEventProperty);
        set => SetValue(SelectedEventProperty, value);
    }

    /// <summary>Height of an hour in the week and day views (20–200). Default 48.</summary>
    public double HourHeight
    {
        get => GetValue(HourHeightProperty);
        set => SetValue(HourHeightProperty, value);
    }

    /// <summary>Dragged and resized events move in steps of this many minutes. Default 15.</summary>
    public int SnapMinutes
    {
        get => GetValue(SnapMinutesProperty);
        set => SetValue(SnapMinutesProperty, value);
    }

    /// <summary>The hour the week and day views open scrolled to. Default 7.</summary>
    public int DayStartHour
    {
        get => GetValue(DayStartHourProperty);
        set => SetValue(DayStartHourProperty, value);
    }

    /// <summary>The working day; the hours outside it are shaded. Default 8.</summary>
    public int WorkdayStartHour
    {
        get => GetValue(WorkdayStartHourProperty);
        set => SetValue(WorkdayStartHourProperty, value);
    }

    /// <summary>Default 18.</summary>
    public int WorkdayEndHour
    {
        get => GetValue(WorkdayEndHourProperty);
        set => SetValue(WorkdayEndHourProperty, value);
    }

    /// <summary>No dragging or resizing (double clicks are still raised).</summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>"October 2026", "Oct 26 – Nov 1, 2026" or the long date of the day view.</summary>
    public string Title
    {
        get => _title;
        private set => SetAndRaise(TitleProperty, ref _title, value);
    }

    /// <summary>Double click or Enter on an event, e.g. to open it.</summary>
    public event EventHandler<RiceCalendarEventEventArgs>? EventActivated
    {
        add => AddHandler(EventActivatedEvent, value);
        remove => RemoveHandler(EventActivatedEvent, value);
    }

    /// <summary>Raised before a dragged or resized event changes; set <c>Cancel</c> to keep it, or <c>Handled</c> to change it yourself.</summary>
    public event EventHandler<RiceCalendarEventChangingEventArgs>? EventChanging
    {
        add => AddHandler(EventChangingEvent, value);
        remove => RemoveHandler(EventChangingEvent, value);
    }

    public event EventHandler<RiceCalendarEventEventArgs>? EventChanged
    {
        add => AddHandler(EventChangedEvent, value);
        remove => RemoveHandler(EventChangedEvent, value);
    }

    /// <summary>A double click on an empty slot: an hour in the week and day views, a whole day in the month view and the all day strip.</summary>
    public event EventHandler<RiceCalendarCreateEventArgs>? CreateEventRequested
    {
        add => AddHandler(CreateEventRequestedEvent, value);
        remove => RemoveHandler(CreateEventRequestedEvent, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceScheduler);

    public void GoToToday() => DisplayDate = DateTime.Today;

    public void Next() => DisplayDate = SchedulerLayout.Step(ViewMode, DisplayDate, 1);

    public void Previous() => DisplayDate = SchedulerLayout.Step(ViewMode, DisplayDate, -1);

    /// <summary>The first day shown and the number of days.</summary>
    public (DateTime Start, int Days) VisibleRange => SchedulerLayout.VisibleRange(ViewMode, DisplayDate, FirstDayOfWeek);

    private IEnumerable<RiceCalendarEvent> Events => ItemsSource?.OfType<RiceCalendarEvent>() ?? [];

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Decorator>("PART_ViewHost");
        Wire(e, "PART_PreviousButton", Previous, RiceStrings.SchedulerPrevious);
        Wire(e, "PART_NextButton", Next, RiceStrings.SchedulerNext);
        Wire(e, "PART_TodayButton", GoToToday, null, RiceStrings.SchedulerToday);
        _viewSwitch?.SelectionChanged -= OnViewSwitchChanged;
        _viewSwitch = e.NameScope.Find<RiceSegmentedControl>("PART_ViewSwitch");
        if (_viewSwitch is not null)
        {
            _viewSwitch.ItemsSource = new[] { RiceStrings.SchedulerMonth, RiceStrings.SchedulerWeek, RiceStrings.SchedulerDay };
            _viewSwitch.SelectedIndex = (int)ViewMode;
            _viewSwitch.SelectionChanged += OnViewSwitchChanged;
        }
        PostRebuild();
    }

    private static void Wire(TemplateAppliedEventArgs e, string name, Action action, string? tip, string? content = null)
    {
        if (e.NameScope.Find<Button>(name) is not { } button)
            return;
        button.Click += (_, _) => action();
        if (tip is not null)
            ToolTip.SetTip(button, tip);
        if (content is not null)
            button.Content = content;
    }

    private void OnViewSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewSwitch is { SelectedIndex: >= 0 and <= 2 } viewSwitch)
            ViewMode = (SchedulerViewMode)viewSwitch.SelectedIndex;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ItemsSourceProperty)
        {
            WatchCollection();
            PostRebuild();
        }
        else if (property == ViewModeProperty)
        {
            _viewSwitch?.SelectedIndex = (int)ViewMode;
            _timeScrollOffset = null;
            PostRebuild();
        }
        else if (property == DisplayDateProperty || property == FirstDayOfWeekProperty || property == HourHeightProperty
                 || property == WorkdayStartHourProperty || property == WorkdayEndHourProperty)
            PostRebuild();
        else if (property == SelectedEventProperty)
        {
            foreach (var chip in _chips)
                chip.IsSelected = chip.Event == SelectedEvent;
        }
    }

    private void WatchCollection()
    {
        _watchedCollection?.CollectionChanged -= OnCollectionChanged;
        _watchedCollection = ItemsSource as INotifyCollectionChanged;
        _watchedCollection?.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => PostRebuild();

    private void OnEventPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // During a drag the event keeps its times until the drop; other changes redraw the view.
        if (_drag is null)
            PostRebuild();
    }

    private void PostRebuild()
    {
        if (_rebuildPosted)
            return;
        _rebuildPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _rebuildPosted = false;
            Rebuild();
        }, DispatcherPriority.Loaded);
    }

    private void Rebuild()
    {
        if (_host is null)
            return;
        var events = Events.ToList();
        foreach (var e in _watched.Except(events).ToList())
        {
            e.PropertyChanged -= OnEventPropertyChanged;
            _watched.Remove(e);
        }
        foreach (var e in events.Where(_watched.Add))
            e.PropertyChanged += OnEventPropertyChanged;

        if (_timeScroller is not null)
            _timeScrollOffset = _timeScroller.Offset.Y;
        _chips.Clear();
        _cells.Clear();
        _timeGrid = null;
        _timeScroller = null;
        var (start, days) = VisibleRange;
        Title = TitleFor(ViewMode, DisplayDate, start, days);
        _host.Child = ViewMode == SchedulerViewMode.Month ? BuildMonth(events, start) : BuildDays(events, start, days);
    }

    internal static string TitleFor(SchedulerViewMode mode, DateTime date, DateTime start, int days)
    {
        var culture = CultureInfo.CurrentCulture;
        switch (mode)
        {
            case SchedulerViewMode.Month:
                return date.ToString("Y", culture);
            case SchedulerViewMode.Week:
                var end = start.AddDays(days - 1);
                var monthDay = culture.DateTimeFormat.MonthDayPattern;
                return start.Year == end.Year
                    ? $"{start.ToString(monthDay, culture)} – {end.ToString(monthDay, culture)}, {end.Year}"
                    : $"{start.ToString("d", culture)} – {end.ToString("d", culture)}";
            default:
                return date.ToString("D", culture);
        }
    }

    private Control BuildMonth(List<RiceCalendarEvent> events, DateTime start)
    {
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto," + string.Join(",", Enumerable.Repeat("*", SchedulerLayout.MonthWeeks))) };
        grid.Children.Add(WeekdayHeader(start, 7, gutter: false));
        for (var week = 0; week < SchedulerLayout.MonthWeeks; week++)
        {
            var rowStart = start.AddDays(week * 7);
            var cells = Enumerable.Range(0, 7)
                .Select(d => new RiceSchedulerDayCell(rowStart.AddDays(d), rowStart.AddDays(d).Month != DisplayDate.Month))
                .ToList();
            _cells.AddRange(cells);
            var chips = SchedulerLayout.PlaceSpans(events, rowStart, 7)
                .Select(p => (Chip(p.Event, p.Event.IsMultiDay ? SchedulerChipKind.Bar : SchedulerChipKind.Dot, p), p))
                .ToList();
            var row = new SchedulerWeekRow(cells, chips, MoreButton, fitToHeight: true, MonthHeaderHeight);
            Grid.SetRow(row, week + 1);
            grid.Children.Add(row);
        }
        return grid;
    }

    private Control BuildDays(List<RiceCalendarEvent> events, DateTime start, int days)
    {
        var dates = Enumerable.Range(0, days).Select(d => start.AddDays(d)).ToList();
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        grid.Children.Add(WeekdayHeader(start, days, gutter: true));

        // All day and multi day events in a strip above the hours.
        var cells = dates.Select(d => new RiceSchedulerDayCell(d, false) { ShowDayNumber = false }).ToList();
        _cells.AddRange(cells);
        var bars = SchedulerLayout.PlaceSpans(events.Where(e => e.IsMultiDay), start, days)
            .Select(p => (Chip(p.Event, SchedulerChipKind.Bar, p), p))
            .ToList();
        var allDay = new DockPanel();
        var label = new TextBlock { Text = RiceStrings.SchedulerAllDay, Width = SchedulerTimeGrid.GutterWidth };
        label.Classes.Add("gutter");
        DockPanel.SetDock(label, Dock.Left);
        allDay.Children.Add(label);
        allDay.Children.Add(new SchedulerWeekRow(cells, bars, MoreButton, fitToHeight: false, headerHeight: 2));
        var allDayStrip = new Border { Child = allDay };
        allDayStrip.Classes.Add("all-day");
        Grid.SetRow(allDayStrip, 1);
        grid.Children.Add(allDayStrip);

        _timeGrid = new SchedulerTimeGrid(this, dates);
        for (var d = 0; d < days; d++)
        {
            var day = dates[d];
            var timed = events.Where(e => !e.IsMultiDay && e.FirstDay == day);
            foreach (var place in SchedulerLayout.PlaceTimed(timed, day))
                _timeGrid.Add(Chip(place.Event, SchedulerChipKind.Block, null), d, place);
        }
        _timeScroller = new ScrollViewer
        {
            Content = _timeGrid,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        var scroller = _timeScroller;
        // A little above the first hour, so its label is not cut off.
        var offset = _timeScrollOffset ?? Math.Max(0, DayStartHour * HourHeight - 12);
        Dispatcher.UIThread.Post(() => scroller.Offset = new Vector(0, offset), DispatcherPriority.Loaded);
        Grid.SetRow(_timeScroller, 2);
        grid.Children.Add(_timeScroller);
        return grid;
    }

    // The names of the days above the columns; in the week and day views with the date, today's in an accent circle.
    private Control WeekdayHeader(DateTime start, int days, bool gutter)
    {
        var header = new Grid();
        header.Classes.Add("weekdays");
        if (gutter)
            header.ColumnDefinitions.Add(new ColumnDefinition(SchedulerTimeGrid.GutterWidth, GridUnitType.Pixel));
        var names = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;
        for (var d = 0; d < days; d++)
        {
            header.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            var date = start.AddDays(d);
            var name = new TextBlock { Text = names[(int)date.DayOfWeek] };
            name.Classes.Add("weekday");
            Control cell = name;
            if (gutter)
            {
                var number = new TextBlock { Text = date.Day.ToString(CultureInfo.CurrentCulture) };
                number.Classes.Add("date");
                var circle = new Border { Child = number };
                circle.Classes.Add("date");
                if (date == DateTime.Today)
                    circle.Classes.Add("today");
                var stack = new StackPanel { Children = { name, circle } };
                var button = new Button { Content = stack };
                button.Classes.Add("day-header");
                button.Click += (_, _) =>
                {
                    DisplayDate = date;
                    ViewMode = SchedulerViewMode.Day;
                };
                cell = button;
            }
            Grid.SetColumn(cell, d + (gutter ? 1 : 0));
            header.Children.Add(cell);
        }
        return header;
    }

    private Button MoreButton(DateTime date)
    {
        var button = new Button();
        button.Classes.Add("more");
        button.Click += (_, _) =>
        {
            DisplayDate = date;
            ViewMode = SchedulerViewMode.Day;
        };
        return button;
    }

    private RiceSchedulerEventChip Chip(RiceCalendarEvent e, SchedulerChipKind kind, SpanPlacement? place)
    {
        var chip = new RiceSchedulerEventChip(e, kind)
        {
            IsSelected = e == SelectedEvent,
            TimeText = kind switch
            {
                SchedulerChipKind.Dot => e.Start.ToString("t", CultureInfo.CurrentCulture),
                SchedulerChipKind.Block => $"{e.Start.ToString("t", CultureInfo.CurrentCulture)} – {e.End.ToString("t", CultureInfo.CurrentCulture)}",
                _ => null,
            },
        };
        if (place is { } p)
            chip.SetContinues(p.ContinuesBefore, p.ContinuesAfter);
        _chips.Add(chip);
        return chip;
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed || e.ClickCount > 1)
            return;
        Focus(NavigationMethod.Pointer);
        if (e.Source is not Visual source || source.FindAncestorOfType<RiceSchedulerEventChip>(includeSelf: true) is not { } chip)
            return;
        SelectedEvent = chip.Event;
        e.Handled = true;
        if (IsReadOnly || chip.Event.IsReadOnly)
            return;

        if (chip.Kind == SchedulerChipKind.Block && _timeGrid is { } timeGrid)
        {
            var onChip = e.GetPosition(chip);
            var (day, minute) = timeGrid.TimeAt(e.GetPosition(timeGrid));
            _drag = new DragState(chip, onChip.Y >= chip.Bounds.Height - ResizeGrip ? DragMode.Resize : DragMode.Time, point.Position)
            {
                OriginDay = day,
                OriginMinute = minute,
            };
        }
        else
            _drag = new DragState(chip, DragMode.Days, point.Position) { OriginDate = CellAt(e)?.Date };
        _drag.NewStart = chip.Event.Start;
        _drag.NewEnd = chip.Event.End;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag is null)
        {
            // The resize cursor over the bottom edge of a block.
            if (e.Source is Visual source && source.FindAncestorOfType<RiceSchedulerEventChip>(includeSelf: true) is { Kind: SchedulerChipKind.Block } chip
                && !IsReadOnly && !chip.Event.IsReadOnly)
                chip.Cursor = e.GetPosition(chip).Y >= chip.Bounds.Height - ResizeGrip ? new Cursor(StandardCursorType.SizeNorthSouth) : null;
            return;
        }
        var drag = _drag;
        var position = e.GetPosition(this);
        if (!drag.Moved)
        {
            var moved = position - drag.Start;
            if (Math.Abs(moved.X) < DragThreshold && Math.Abs(moved.Y) < DragThreshold)
                return;
            drag.Moved = true;
            drag.Chip.SetDragging(true);
        }

        var ev = drag.Chip.Event;
        if (drag.Mode == DragMode.Days)
        {
            var cell = CellAt(e);
            if (cell != drag.Target)
            {
                drag.Target?.SetDropTarget(false);
                drag.Target = cell;
                cell?.SetDropTarget(true);
            }
            if (cell is not null && drag.OriginDate is { } origin)
            {
                var days = (cell.Date - origin).Days;
                drag.NewStart = ev.Start.AddDays(days);
                drag.NewEnd = ev.End.AddDays(days);
            }
        }
        else if (_timeGrid is { } timeGrid)
        {
            var (day, minute) = timeGrid.TimeAt(e.GetPosition(timeGrid));
            var dayStart = timeGrid.Days[drag.Mode == DragMode.Time ? day : drag.OriginDay];
            if (drag.Mode == DragMode.Time)
            {
                var duration = ev.End - ev.Start;
                var startMinute = SchedulerLayout.Snap((ev.Start - ev.Start.Date).TotalMinutes + minute - drag.OriginMinute, SnapMinutes);
                startMinute = Math.Clamp(startMinute, 0, Math.Max(0, 24 * 60 - duration.TotalMinutes));
                drag.NewStart = dayStart.AddMinutes(startMinute);
                drag.NewEnd = drag.NewStart + duration;
            }
            else
            {
                var endMinute = Math.Clamp(SchedulerLayout.Snap(minute, SnapMinutes), (ev.Start - dayStart).TotalMinutes + SnapMinutes, 24 * 60);
                drag.NewStart = ev.Start;
                drag.NewEnd = dayStart.AddMinutes(endMinute);
            }
            var previewDay = timeGrid.Days.ToList().IndexOf(drag.NewStart.Date);
            timeGrid.SetPreview(drag.Chip, Math.Max(0, previewDay), (drag.NewStart - drag.NewStart.Date).TotalMinutes,
                (drag.NewEnd - drag.NewStart.Date).TotalMinutes);
            drag.Chip.TimeText = $"{drag.NewStart.ToString("t", CultureInfo.CurrentCulture)} – {drag.NewEnd.ToString("t", CultureInfo.CurrentCulture)}";
            AutoScroll(e);
        }
        e.Handled = true;
    }

    private void AutoScroll(PointerEventArgs e)
    {
        if (_timeScroller is not { } scroller)
            return;
        var y = e.GetPosition(scroller).Y;
        if (y < 24)
            scroller.Offset -= new Vector(0, 12);
        else if (y > scroller.Bounds.Height - 24)
            scroller.Offset += new Vector(0, 12);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag is not { } drag)
            return;
        EndDrag();
        e.Pointer.Capture(null);
        var ev = drag.Chip.Event;
        // A plain click changes nothing (rebuilding would replace the chip before the second click of a double click).
        if (drag.Moved && (drag.NewStart != ev.Start || drag.NewEnd != ev.End))
            ChangeEvent(ev, drag.NewStart, drag.NewEnd);
        else if (drag.Moved)
            PostRebuild();
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_drag is not null)
        {
            EndDrag();
            PostRebuild();
        }
    }

    private void EndDrag()
    {
        if (_drag is not { } drag)
            return;
        _drag = null;
        drag.Chip.SetDragging(false);
        drag.Target?.SetDropTarget(false);
        _timeGrid?.SetPreview(null);
    }

    /// <summary>Moves or resizes an event as a drag would: raises <see cref="EventChanging"/> and <see cref="EventChanged"/>.</summary>
    public bool ChangeEvent(RiceCalendarEvent calendarEvent, DateTime newStart, DateTime newEnd)
    {
        var args = new RiceCalendarEventChangingEventArgs(EventChangingEvent, calendarEvent, newStart, newEnd);
        RaiseEvent(args);
        if (args.Cancel)
        {
            PostRebuild();
            return false;
        }
        if (!args.Handled)
        {
            calendarEvent.Start = newStart;
            calendarEvent.End = newEnd;
        }
        RaiseEvent(new RiceCalendarEventEventArgs(EventChangedEvent, calendarEvent));
        PostRebuild();
        return true;
    }

    private RiceSchedulerDayCell? CellAt(PointerEventArgs e) =>
        _cells.FirstOrDefault(c => c.IsEffectivelyVisible && new Rect(c.Bounds.Size).Contains(e.GetPosition(c)));

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Visual source)
            return;
        if (source.FindAncestorOfType<RiceSchedulerEventChip>(includeSelf: true) is { } chip)
        {
            RaiseEvent(new RiceCalendarEventEventArgs(EventActivatedEvent, chip.Event));
            e.Handled = true;
            return;
        }
        if (source.FindAncestorOfType<Button>(includeSelf: true) is not null)
            return;
        if (_timeGrid is { } timeGrid && (source == timeGrid || source.FindAncestorOfType<SchedulerTimeGrid>() == timeGrid))
        {
            var point = e.GetPosition(timeGrid);
            if (point.X < SchedulerTimeGrid.GutterWidth)
                return;
            var (day, minute) = timeGrid.TimeAt(point);
            var start = timeGrid.Days[day].AddMinutes(Math.Floor(minute / 30) * 30);
            RaiseEvent(new RiceCalendarCreateEventArgs(CreateEventRequestedEvent, start, start.AddHours(1), false));
            e.Handled = true;
        }
        else if (source.FindAncestorOfType<RiceSchedulerDayCell>(includeSelf: true) is { } cell)
        {
            RaiseEvent(new RiceCalendarCreateEventArgs(CreateEventRequestedEvent, cell.Date, cell.Date.AddDays(1), true));
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;
        switch (e.Key)
        {
            case Key.Escape when _drag is not null:
                EndDrag();
                PostRebuild();
                break;
            case Key.PageDown:
                Next();
                break;
            case Key.PageUp:
                Previous();
                break;
            case Key.Home:
                GoToToday();
                break;
            case Key.Enter when SelectedEvent is { } selected:
                RaiseEvent(new RiceCalendarEventEventArgs(EventActivatedEvent, selected));
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void ResetBrushes()
    {
        _brushes.Clear();
        _timeGrid?.Redraw();
    }

    internal IBrush? Brush(string key)
    {
        if (_brushes.TryGetValue(key, out var brush))
            return brush;
        brush = this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;
        _brushes[key] = brush;
        return brush;
    }
}

public class RiceCalendarEventEventArgs(RoutedEvent routedEvent, RiceCalendarEvent calendarEvent) : RoutedEventArgs(routedEvent)
{
    public RiceCalendarEvent Event { get; } = calendarEvent;
}

public class RiceCalendarEventChangingEventArgs(RoutedEvent routedEvent, RiceCalendarEvent calendarEvent, DateTime newStart, DateTime newEnd)
    : RiceCalendarEventEventArgs(routedEvent, calendarEvent)
{
    public DateTime NewStart { get; } = newStart;

    public DateTime NewEnd { get; } = newEnd;

    /// <summary>Keeps the event where it was.</summary>
    public bool Cancel { get; set; }
}

public class RiceCalendarCreateEventArgs(RoutedEvent routedEvent, DateTime start, DateTime end, bool isAllDay) : RoutedEventArgs(routedEvent)
{
    public DateTime Start { get; } = start;

    /// <summary>Exclusive: an hour after <see cref="Start"/>, or the next midnight for a day.</summary>
    public DateTime End { get; } = end;

    public bool IsAllDay { get; } = isAllDay;
}
