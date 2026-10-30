using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// One lane (swimlane) of a <see cref="RiceKanbanBoard"/>: a header bar with the card count over a row of
/// <see cref="RiceKanbanColumn"/>s, e.g. one lane per assignee or per epic. The cards are dragged between the columns of
/// every lane. The chevron of the header collapses the lane (<see cref="IsExpanded"/>).
/// Pseudo classes: <c>:collapsed</c>, <c>:empty</c> (no card in any column).
/// </summary>
public class RiceKanbanLane : HeaderedItemsControl
{
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<RiceKanbanLane, bool>(nameof(IsExpanded), true,
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<object?> HeaderActionsProperty =
        AvaloniaProperty.Register<RiceKanbanLane, object?>(nameof(HeaderActions));

    public static readonly DirectProperty<RiceKanbanLane, string> CountTextProperty =
        AvaloniaProperty.RegisterDirect<RiceKanbanLane, string>(nameof(CountText), o => o.CountText);

    private string _countText = "0";
    private Button? _expandButton;
    private IDisposable? _childrenBinding;

    public RiceKanbanLane()
    {
        UpdateCount();
    }

    /// <summary>Shows the columns; false shows only the header. Default true.</summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>Right side of the header, e.g. a menu button.</summary>
    public object? HeaderActions
    {
        get => GetValue(HeaderActionsProperty);
        set => SetValue(HeaderActionsProperty, value);
    }

    /// <summary>The number of cards in all the columns of the lane.</summary>
    public string CountText
    {
        get => _countText;
        private set => SetAndRaise(CountTextProperty, ref _countText, value);
    }

    /// <summary>The board of the lane; null when it is used on its own.</summary>
    public RiceKanbanBoard? Board => ItemsControlFromItemContainer(this) as RiceKanbanBoard;

    /// <summary>The columns of the lane, in order.</summary>
    public IEnumerable<RiceKanbanColumn> Columns =>
        Enumerable.Range(0, ItemCount).Select(ContainerFromIndex).OfType<RiceKanbanColumn>();

    protected override Type StyleKeyOverride => typeof(RiceKanbanLane);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new RiceKanbanColumn();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<RiceKanbanColumn>(item, out recycleKey);

    // The columns get the board's ItemTemplate, like the columns of a board without lanes.
    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        if (container != item && container is RiceKanbanColumn column && Board is { } board)
            board.PrepareColumn(column, item, this);
        else
            base.PrepareContainerForItemOverride(container, item, index);
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        UpdateCount();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is RiceKanbanColumn column)
            column.SetChildrenBinding(null);
        UpdateCount(container);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _expandButton?.Click -= OnExpandButtonClick;
        _expandButton = e.NameScope.Find<Button>("PART_ExpandButton");
        _expandButton?.Click += OnExpandButtonClick;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
            PseudoClasses.Set(":collapsed", !IsExpanded);
    }

    private void OnExpandButtonClick(object? sender, RoutedEventArgs e) => IsExpanded = !IsExpanded;

    // Also called by the columns when their count changes; a column being removed no longer counts.
    internal void UpdateCount(Control? removed = null)
    {
        var count = Columns.Where(c => c != removed).Sum(c => c.ItemCount);
        CountText = count.ToString(CultureInfo.CurrentCulture);
        PseudoClasses.Set(":empty", count == 0);
    }

    // The board binds the columns of a data lane from its LaneTemplate; released with the container.
    internal void SetChildrenBinding(IDisposable? binding)
    {
        _childrenBinding?.Dispose();
        _childrenBinding = binding;
    }
}
