using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using KD.Avalonia.Rice.Controls.Scheduling;
using KD.Avalonia.Rice.Notifications;

namespace KD.Avalonia.Rice.Demo;

public partial class SchedulerPage : UserControl
{
    private static readonly IBrush Green = new SolidColorBrush(Color.Parse("#30A46C"));
    private static readonly IBrush Purple = new SolidColorBrush(Color.Parse("#8E4EC6"));
    private static readonly IBrush Orange = new SolidColorBrush(Color.Parse("#F76B15"));
    private static readonly IBrush Blue = new SolidColorBrush(Color.Parse("#0090FF"));

    private readonly ObservableCollection<RiceCalendarEvent> _events = [];
    private int _created;

    public SchedulerPage()
    {
        InitializeComponent();
        var monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        Add("Stand-up", monday.AddHours(9.5), 0.25, Blue);
        Add("Stand-up", monday.AddDays(1).AddHours(9.5), 0.25, Blue);
        Add("Stand-up", monday.AddDays(2).AddHours(9.5), 0.25, Blue);
        Add("Stand-up", monday.AddDays(3).AddHours(9.5), 0.25, Blue);
        Add("Design review", monday.AddDays(1).AddHours(10), 1.5, Purple, "Kanban board and timeline");
        Add("Pairing: code editor", monday.AddDays(1).AddHours(11), 1, null);
        Add("Lunch with Anna", monday.AddDays(2).AddHours(12), 1, Green);
        Add("Release planning", monday.AddDays(2).AddHours(14), 2, Orange, "Version 26.11");
        Add("1:1", monday.AddDays(2).AddHours(15), 0.5, null);
        Add("Dentist", monday.AddDays(3).AddHours(16.5), 1, Green);
        Add("Demo day", monday.AddDays(4).AddHours(13), 3, Purple);
        Add("Retro", monday.AddDays(4).AddHours(10), 1, null);
        _events.Add(new RiceCalendarEvent { Title = "Linux conference", Start = monday.AddDays(3), End = monday.AddDays(6), IsAllDay = true, Color = Orange });
        _events.Add(new RiceCalendarEvent { Title = "Sprint 42", Start = monday, End = monday.AddDays(5), IsAllDay = true, Color = Blue });
        _events.Add(new RiceCalendarEvent { Title = "Holiday", Start = monday.AddDays(9), End = monday.AddDays(12), IsAllDay = true, Color = Green });
        _events.Add(new RiceCalendarEvent { Title = "Birthday 🎂", Start = monday.AddDays(-3), End = monday.AddDays(-2), IsAllDay = true, Color = Purple });
        for (var i = 0; i < 5; i++)
            Add($"Interview {i + 1}", monday.AddDays(8).AddHours(9 + i * 1.5), 1, i % 2 == 0 ? Blue : null);
        Add("Budget meeting", monday.AddDays(-5).AddHours(14), 1, Orange);
        Add("Read-only: company all-hands", monday.AddDays(15).AddHours(16), 1, null);
        _events[^1].IsReadOnly = true;

        Scheduler.ItemsSource = _events;
        Scheduler.EventActivated += (_, e) =>
            RiceToast.ShowInfo($"{e.Event.Start:g} – {e.Event.End:t}", e.Event.Title);
        Scheduler.EventChanged += (_, e) =>
            ChangeInfo.Text = $"“{e.Event.Title}” moved to {e.Event.Start.ToString("g", CultureInfo.CurrentCulture)} – {e.Event.End.ToString("t", CultureInfo.CurrentCulture)}";
        Scheduler.CreateEventRequested += (_, e) => _events.Add(new RiceCalendarEvent
        {
            Title = $"New event {++_created}",
            Start = e.Start,
            End = e.End,
            IsAllDay = e.IsAllDay,
        });
    }

    private void Add(string title, DateTime start, double hours, IBrush? color, string? description = null) =>
        _events.Add(new RiceCalendarEvent
        {
            Title = title,
            Start = start,
            End = start.AddHours(hours),
            Color = color,
            Description = description,
        });
}
