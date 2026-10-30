using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Media;
using KD.Avalonia.Rice.Controls;
using KD.Avalonia.Rice.Notifications;

namespace KD.Avalonia.Rice.Demo;

public sealed record KanbanTask(string Title, string Tag, string Initials, IBrush? PriorityBrush = null);

public sealed class KanbanColumnModel(string title, IBrush? accent = null, int wipLimit = 0)
{
    public string Title { get; } = title;
    public IBrush? Accent { get; } = accent;
    public int WipLimit { get; } = wipLimit;
    public ObservableCollection<KanbanTask> Cards { get; } = [];
}

public sealed class KanbanLaneModel(string name, string initials)
{
    public string Name { get; } = name;
    public string Initials { get; } = initials;
    public ObservableCollection<KanbanColumnModel> Columns { get; } = [];
}

public partial class KanbanPage : UserControl
{
    private static readonly IBrush High = new SolidColorBrush(Color.Parse("#E5484D"));
    private static readonly IBrush Medium = new SolidColorBrush(Color.Parse("#F5A524"));

    private int _added;

    public KanbanPage()
    {
        InitializeComponent();

        var todo = new KanbanColumnModel("To do", new SolidColorBrush(Color.Parse("#8B8D98")));
        var doing = new KanbanColumnModel("In progress", new SolidColorBrush(Color.Parse("#3E63DD")), wipLimit: 3);
        var review = new KanbanColumnModel("Review", new SolidColorBrush(Color.Parse("#AB4ABA")));
        var done = new KanbanColumnModel("Done", new SolidColorBrush(Color.Parse("#30A46C")));
        todo.Cards.Add(new KanbanTask("Timeline: day headers", "timeline", "AK", Medium));
        todo.Cards.Add(new KanbanTask("Code editor: Ctrl+/ comments", "editor", "DK"));
        todo.Cards.Add(new KanbanTask("Write the dock layout docs", "docs", "BN"));
        doing.Cards.Add(new KanbanTask("Kanban drag and drop", "kanban", "DK", High));
        doing.Cards.Add(new KanbanTask("Save the dock layout", "dock", "AK"));
        review.Cards.Add(new KanbanTask("Ribbon key tips", "ribbon", "BN", Medium));
        done.Cards.Add(new KanbanTask("Toast notifications", "toast", "DK"));
        done.Cards.Add(new KanbanTask("TreeMap chart", "charts", "AK"));
        Board.ItemsSource = new[] { todo, doing, review, done };

        LaneBoard.ItemsSource = CreateLanes(out var headers);
        LaneBoard.ColumnHeaders = headers;
        LayoutSwitch.SelectionChanged += (_, _) => LaneBoard.IsVisible = LayoutSwitch.SelectedIndex == 1;
        LaneBoard.CardMoved += (_, e) =>
            MoveResult.Text = $"“{(e.Item as KanbanTask)?.Title}” → {(e.TargetLane?.DataContext as KanbanLaneModel)?.Name}, "
                              + $"{(e.TargetColumn.DataContext as KanbanColumnModel)?.Title}, position {e.TargetIndex + 1}";

        Board.CardMoved += (_, e) =>
            MoveResult.Text = $"“{(e.Item as KanbanTask)?.Title}” → {(e.TargetColumn.DataContext as KanbanColumnModel)?.Title}, position {e.TargetIndex + 1}";
        Board.CardActivated += (_, e) => RiceToast.ShowInfo((e.Item as KanbanTask)?.Title ?? "", "Opened");
        Board.AddCardRequested += (_, e) =>
        {
            if (e.Column.DataContext is KanbanColumnModel column)
                column.Cards.Add(new KanbanTask($"New task {++_added}", "new", "ME"));
        };
    }

    // One lane per person, each with its own To do / In progress / Done columns; the header row names them once.
    private static KanbanLaneModel[] CreateLanes(out KanbanColumnModel[] headers)
    {
        IBrush Accent(string color) => new SolidColorBrush(Color.Parse(color));
        headers =
        [
            new KanbanColumnModel("To do", Accent("#8B8D98")),
            new KanbanColumnModel("In progress", Accent("#3E63DD"), wipLimit: 2),
            new KanbanColumnModel("Done", Accent("#30A46C")),
        ];
        var lanes = new[] { new KanbanLaneModel("Dávid Kovács", "DK"), new KanbanLaneModel("Anna Kiss", "AK"), new KanbanLaneModel("Bence Nagy", "BN") };
        foreach (var lane in lanes)
        foreach (var header in headers)
            lane.Columns.Add(new KanbanColumnModel(header.Title, header.Accent, header.WipLimit));

        lanes[0].Columns[0].Cards.Add(new KanbanTask("Code editor: Ctrl+/ comments", "editor", "DK"));
        lanes[0].Columns[1].Cards.Add(new KanbanTask("Kanban swimlanes", "kanban", "DK", High));
        lanes[0].Columns[2].Cards.Add(new KanbanTask("Toast notifications", "toast", "DK"));
        lanes[1].Columns[0].Cards.Add(new KanbanTask("Timeline: day headers", "timeline", "AK", Medium));
        lanes[1].Columns[1].Cards.Add(new KanbanTask("Save the dock layout", "dock", "AK"));
        lanes[1].Columns[2].Cards.Add(new KanbanTask("TreeMap chart", "charts", "AK"));
        lanes[2].Columns[0].Cards.Add(new KanbanTask("Write the dock layout docs", "docs", "BN"));
        lanes[2].Columns[1].Cards.Add(new KanbanTask("Ribbon key tips", "ribbon", "BN", Medium));
        return lanes;
    }
}
