using Avalonia.Controls;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// One header of the row above the lanes of a <see cref="RiceKanbanBoard"/> (<see cref="RiceKanbanBoard.ColumnHeaders"/>),
/// as wide as a <see cref="RiceKanbanColumn"/>: give both the same <c>Width</c> in a style when the columns change it.
/// </summary>
public class RiceKanbanColumnHeader : ContentControl
{
    protected override Type StyleKeyOverride => typeof(RiceKanbanColumnHeader);
}
