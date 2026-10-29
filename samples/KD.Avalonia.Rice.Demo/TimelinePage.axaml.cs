using System.Collections.ObjectModel;
using Avalonia.Controls;
using KD.Avalonia.Rice.Controls;
using Material.Icons;

namespace KD.Avalonia.Rice.Demo;

public sealed record TimelineEvent(string Title, string? Details, DateTime When, TimelineItemKind Kind = TimelineItemKind.Default,
    MaterialIconKind? Icon = null);

public partial class TimelinePage : UserControl
{
    private static readonly (string Title, TimelineItemKind Kind, MaterialIconKind? Icon)[] NewEvents =
    [
        ("Build passed", TimelineItemKind.Success, MaterialIconKind.Check),
        ("Comment added", TimelineItemKind.Default, null),
        ("Disk almost full", TimelineItemKind.Warning, MaterialIconKind.AlertOutline),
        ("Deploy failed", TimelineItemKind.Error, MaterialIconKind.Close),
    ];

    // Newest first: the timeline shows the items in this order.
    private readonly ObservableCollection<TimelineEvent> _events;
    private int _added;

    public TimelinePage()
    {
        InitializeComponent();
        var now = DateTime.Now;
        _events =
        [
            new("Pull request merged", "#128 Kanban board: drag and drop between columns", now.AddMinutes(-25), TimelineItemKind.Success, MaterialIconKind.SourceMerge),
            new("Review requested", "Anna asked for a review of the code editor.", now.AddHours(-2), TimelineItemKind.Info),
            new("Build failed", "Tests: 2 failed, 341 passed.", now.AddHours(-3), TimelineItemKind.Error, MaterialIconKind.Close),
            new("Issue opened", "#131 Dock layout: remember the sizes", now.AddDays(-1).AddHours(-1)),
            new("Release 26.10.4", "Toast notifications and the ribbon.", now.AddDays(-1).AddHours(-5), TimelineItemKind.Success, MaterialIconKind.TagOutline),
            new("Low disk space", "The build agent has 3 GB left.", now.AddDays(-4), TimelineItemKind.Warning, MaterialIconKind.AlertOutline),
            new("Project created", null, now.AddDays(-4).AddHours(-3)),
        ];
        Activity.ItemsSource = _events;
        AddEventButton.Click += (_, _) =>
        {
            var (title, kind, icon) = NewEvents[_added++ % NewEvents.Length];
            _events.Insert(0, new TimelineEvent(title, $"Added from the demo ({_added}).", DateTime.Now, kind, icon));
        };
    }
}
