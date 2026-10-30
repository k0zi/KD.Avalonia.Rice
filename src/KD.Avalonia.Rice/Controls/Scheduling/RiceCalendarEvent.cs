using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls.Scheduling;

/// <summary>
/// An event of a <see cref="RiceScheduler"/>. <see cref="End"/> is exclusive: an all day event of one day runs from
/// midnight to the next midnight (an <see cref="End"/> on the start day works too). Changes show up at once; the
/// scheduler changes <see cref="Start"/> and <see cref="End"/> when the event is dragged or resized.
/// Keep your own data in <see cref="Tag"/>.
/// </summary>
public class RiceCalendarEvent : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get => field; set => Set(ref field, value); } = "";

    public string? Description { get => field; set => Set(ref field, value); }

    public string? Location { get => field; set => Set(ref field, value); }

    public DateTime Start { get => field; set => Set(ref field, value); }

    public DateTime End { get => field; set => Set(ref field, value); }

    /// <summary>Shown as a bar over the days instead of a block in the hours.</summary>
    public bool IsAllDay { get => field; set => Set(ref field, value); }

    /// <summary>Color of the event; null: the accent color.</summary>
    public IBrush? Color { get => field; set => Set(ref field, value); }

    /// <summary>A read only event cannot be dragged or resized.</summary>
    public bool IsReadOnly { get => field; set => Set(ref field, value); }

    /// <summary>Your own data (e.g. the record the event was made from).</summary>
    public object? Tag { get => field; set => Set(ref field, value); }

    /// <summary>The first day the event is on.</summary>
    public DateTime FirstDay => Start.Date;

    /// <summary>The last day the event is on (an end at midnight belongs to the day before).</summary>
    public DateTime LastDay => SchedulerLayout.LastDay(Start, End);

    /// <summary>Spans more than one day or is all day: drawn as a bar over the days.</summary>
    public bool IsMultiDay => IsAllDay || LastDay > FirstDay;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public override string ToString() => Title;
}
