using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Kanban board: <see cref="RiceKanbanColumn"/>s side by side, holding <see cref="RiceKanbanCard"/>s that are dragged
/// within and between the columns (a picture of the card follows the pointer, a line shows where it lands), or moved
/// with Ctrl+arrow keys. Moving removes the item from the source column's <c>Items</c> / modifiable <c>ItemsSource</c>
/// and inserts it into the target's, unless a <see cref="CardMoving"/> handler cancels it.
/// From data: <c>ItemsSource</c> lists the columns, a <see cref="TreeDataTemplate"/> as <c>ItemTemplate</c> gives the
/// header and the cards of a column (its <c>ItemsSource</c>), and <see cref="CardTemplate"/> shows a card.
/// </summary>
public class RiceKanbanBoard : ItemsControl
{
    /// <summary>Template of the data cards of every column (inherited); a column's own <c>ItemTemplate</c> wins.</summary>
    public static readonly StyledProperty<IDataTemplate?> CardTemplateProperty =
        AvaloniaProperty.Register<RiceKanbanBoard, IDataTemplate?>(nameof(CardTemplate), inherits: true);

    public static readonly StyledProperty<bool> CanDragCardsProperty =
        AvaloniaProperty.Register<RiceKanbanBoard, bool>(nameof(CanDragCards), true);

    public static readonly StyledProperty<bool> EnforceWipLimitsProperty =
        AvaloniaProperty.Register<RiceKanbanBoard, bool>(nameof(EnforceWipLimits));

    /// <summary>Shows an "Add card" button at the bottom of every column (inherited).</summary>
    public static readonly StyledProperty<bool> ShowAddCardButtonProperty =
        AvaloniaProperty.Register<RiceKanbanBoard, bool>(nameof(ShowAddCardButton), inherits: true);

    public static readonly StyledProperty<object?> SelectedCardProperty =
        AvaloniaProperty.Register<RiceKanbanBoard, object?>(nameof(SelectedCard),
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public static readonly RoutedEvent<RiceKanbanCardMovingEventArgs> CardMovingEvent =
        RoutedEvent.Register<RiceKanbanBoard, RiceKanbanCardMovingEventArgs>(nameof(CardMoving), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RiceKanbanCardMovedEventArgs> CardMovedEvent =
        RoutedEvent.Register<RiceKanbanBoard, RiceKanbanCardMovedEventArgs>(nameof(CardMoved), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RiceKanbanColumnEventArgs> AddCardRequestedEvent =
        RoutedEvent.Register<RiceKanbanBoard, RiceKanbanColumnEventArgs>(nameof(AddCardRequested), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RiceKanbanCardEventArgs> CardActivatedEvent =
        RoutedEvent.Register<RiceKanbanBoard, RiceKanbanCardEventArgs>(nameof(CardActivated), RoutingStrategies.Bubble);

    // The pointer must move this far before a press on a card becomes a drag; near the edges the views scroll.
    private const double DragThreshold = 6;
    private const double AutoScrollEdge = 36;
    private const double AutoScrollStep = 14;

    private ScrollViewer? _scrollViewer;

    private RiceKanbanCard? _pressedCard;
    private Point _pressPoint;
    private DragState? _drag;

    private sealed class DragState(object? item, RiceKanbanColumn source, Point grabOffset)
    {
        public object? Item { get; } = item;
        public RiceKanbanColumn Source { get; } = source;
        public Point GrabOffset { get; } = grabOffset;
        public Canvas? GhostHost { get; set; }
        public Control? Ghost { get; set; }
        public RiceKanbanColumn? Target { get; set; }
        public int TargetIndex { get; set; } = -1;
    }

    public RiceKanbanBoard()
    {
        AddHandler(PointerPressedEvent, OnCardPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(DoubleTappedEvent, OnCardDoubleTapped, RoutingStrategies.Bubble);
    }

    public IDataTemplate? CardTemplate
    {
        get => GetValue(CardTemplateProperty);
        set => SetValue(CardTemplateProperty, value);
    }

    /// <summary>Lets the cards be dragged and moved with Ctrl+arrow keys. Default true.</summary>
    public bool CanDragCards
    {
        get => GetValue(CanDragCardsProperty);
        set => SetValue(CanDragCardsProperty, value);
    }

    /// <summary>Refuses to move a card into a column that has as many cards as its <see cref="RiceKanbanColumn.WipLimit"/>.</summary>
    public bool EnforceWipLimits
    {
        get => GetValue(EnforceWipLimitsProperty);
        set => SetValue(EnforceWipLimitsProperty, value);
    }

    public bool ShowAddCardButton
    {
        get => GetValue(ShowAddCardButtonProperty);
        set => SetValue(ShowAddCardButtonProperty, value);
    }

    /// <summary>The item of the selected card (the data item, or the <see cref="RiceKanbanCard"/> declared in XAML).</summary>
    public object? SelectedCard
    {
        get => GetValue(SelectedCardProperty);
        set => SetValue(SelectedCardProperty, value);
    }

    /// <summary>Raised before a card moves; set <c>Cancel</c> to keep it, or <c>Handled</c> to move it yourself.</summary>
    public event EventHandler<RiceKanbanCardMovingEventArgs>? CardMoving
    {
        add => AddHandler(CardMovingEvent, value);
        remove => RemoveHandler(CardMovingEvent, value);
    }

    /// <summary>Raised after a card moved (also within its column).</summary>
    public event EventHandler<RiceKanbanCardMovedEventArgs>? CardMoved
    {
        add => AddHandler(CardMovedEvent, value);
        remove => RemoveHandler(CardMovedEvent, value);
    }

    /// <summary>Raised by the "Add card" button of a column (<c>e.Column</c>).</summary>
    public event EventHandler<RiceKanbanColumnEventArgs>? AddCardRequested
    {
        add => AddHandler(AddCardRequestedEvent, value);
        remove => RemoveHandler(AddCardRequestedEvent, value);
    }

    /// <summary>Raised when a card is double clicked or Enter is pressed on it, e.g. to open it.</summary>
    public event EventHandler<RiceKanbanCardEventArgs>? CardActivated
    {
        add => AddHandler(CardActivatedEvent, value);
        remove => RemoveHandler(CardActivatedEvent, value);
    }

    /// <summary>The columns on the board, in order.</summary>
    public IEnumerable<RiceKanbanColumn> Columns =>
        Enumerable.Range(0, ItemCount).Select(ContainerFromIndex).OfType<RiceKanbanColumn>();

    protected override Type StyleKeyOverride => typeof(RiceKanbanBoard);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new RiceKanbanColumn();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<RiceKanbanColumn>(item, out recycleKey);

    // Not the base: it would hand the board's ItemTemplate (the column header) down to the cards as well.
    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        if (container == item || container is not RiceKanbanColumn column)
        {
            base.PrepareContainerForItemOverride(container, item, index);
            return;
        }
        var template = ItemTemplate ?? this.FindDataTemplate(item);
        if (!column.IsSet(HeaderedItemsControl.HeaderProperty))
            column.Header = item;
        if (!column.IsSet(HeaderedItemsControl.HeaderTemplateProperty) && template is not null)
            column.HeaderTemplate = template;
        if (template is ITreeDataTemplate tree && item is not null && !column.IsSet(ItemsSourceProperty))
            column.SetChildrenBinding(tree.BindChildren(column, ItemsSourceProperty, item));
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is RiceKanbanColumn column)
            column.SetChildrenBinding(null);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedCardProperty)
        {
            foreach (var card in AllCards())
                card.IsSelected = SelectedCard is { } selected && Equals(ItemOf(card), selected);
        }
    }

    /// <summary>
    /// Moves the card at <paramref name="index"/> of <paramref name="source"/> to <paramref name="targetIndex"/> of
    /// <paramref name="target"/> (an index before the move; past the end: last), raising <see cref="CardMoving"/> and
    /// <see cref="CardMoved"/>. False when it was cancelled or cannot be moved.
    /// </summary>
    public bool MoveCard(RiceKanbanColumn source, int index, RiceKanbanColumn target, int targetIndex)
    {
        if (index < 0 || index >= source.ItemCount || !source.CanChangeItems || !target.CanChangeItems)
            return false;
        var same = source == target;
        var finalIndex = FinalIndex(same, index, Math.Clamp(targetIndex, 0, target.ItemCount));
        if (finalIndex < 0)
            return false;
        if (!same && !CanDropInto(target))
            return false;

        var item = source.ItemAt(index);
        var args = new RiceKanbanCardMovingEventArgs(CardMovingEvent, item, source, index, target, finalIndex);
        RaiseEvent(args);
        if (args.Cancel)
            return false;
        if (!args.Handled)
        {
            source.RemoveAt(index);
            if (!target.Insert(finalIndex, item))
            {
                source.Insert(index, item);
                return false;
            }
        }
        SelectedCard = item;
        RaiseEvent(new RiceKanbanCardMovedEventArgs(CardMovedEvent, item, source, index, target, finalIndex));
        Dispatcher.UIThread.Post(() => (target.ContainerFromItem(item!) as RiceKanbanCard)?.Focus(NavigationMethod.Directional),
            DispatcherPriority.Loaded);
        return true;
    }

    /// <summary>
    /// The index a card takes in the target when dropped before <paramref name="targetIndex"/> (an index before the move);
    /// -1 when it stays where it was.
    /// </summary>
    internal static int FinalIndex(bool sameColumn, int sourceIndex, int targetIndex)
    {
        if (!sameColumn)
            return targetIndex;
        // Dropping right before or after itself changes nothing; below it, the removal shifts the place up by one.
        if (targetIndex == sourceIndex || targetIndex == sourceIndex + 1)
            return -1;
        return targetIndex > sourceIndex ? targetIndex - 1 : targetIndex;
    }

    /// <summary>Whether a card from another column may be dropped into <paramref name="column"/>.</summary>
    public bool CanDropInto(RiceKanbanColumn column) =>
        column.AllowDrop && column.CanChangeItems && !IsFull(column.ItemCount, column.WipLimit, EnforceWipLimits);

    internal static bool IsFull(int count, int wipLimit, bool enforce) => enforce && wipLimit > 0 && count >= wipLimit;

    internal bool IsDraggingItem(object? item) => _drag is { } drag && Equals(drag.Item, item);

    private IEnumerable<RiceKanbanCard> AllCards() =>
        Columns.SelectMany(c => Enumerable.Range(0, c.ItemCount).Select(c.ContainerFromIndex)).OfType<RiceKanbanCard>();

    private static object? ItemOf(RiceKanbanCard card) =>
        ItemsControlFromItemContainer(card) is { } column ? column.ItemFromContainer(card) : card;

    private static RiceKanbanColumn? ColumnOf(RiceKanbanCard card) => ItemsControlFromItemContainer(card) as RiceKanbanColumn;

    private void OnCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Visual source || source.FindAncestorOfType<RiceKanbanCard>(includeSelf: true) is not { } card
            || ColumnOf(card) is not { } column || column.Board != this)
            return;
        // Buttons and text boxes on a card keep working.
        if (source.FindAncestorOfType<Button>(includeSelf: true) is { } button && card.IsVisualAncestorOf(button)
            || source.FindAncestorOfType<TextBox>(includeSelf: true) is not null)
            return;
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
            return;
        SelectedCard = column.ItemFromContainer(card);
        card.Focus(NavigationMethod.Pointer);
        if (CanDragCards)
        {
            _pressedCard = card;
            _pressPoint = point.Position;
        }
    }

    private void OnCardDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source && source.FindAncestorOfType<RiceKanbanCard>(includeSelf: true) is { } card
            && ColumnOf(card) is { } column && column.Board == this)
        {
            RaiseEvent(new RiceKanbanCardEventArgs(CardActivatedEvent, column.ItemFromContainer(card), column));
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        if (_drag is null)
        {
            if (_pressedCard is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;
            var moved = position - _pressPoint;
            if (Math.Abs(moved.X) < DragThreshold && Math.Abs(moved.Y) < DragThreshold)
                return;
            StartDrag(_pressedCard, e);
            if (_drag is null)
                return;
        }

        MoveGhost(position);
        AutoScroll(position);
        UpdateDropTarget(position);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _pressedCard = null;
        if (_drag is not { } drag)
            return;
        var target = drag.Target;
        var targetIndex = drag.TargetIndex;
        EndDrag(e.Pointer);
        var index = drag.Source.Items.IndexOf(drag.Item);
        if (target is not null && targetIndex >= 0 && index >= 0)
            MoveCard(drag.Source, index, target, targetIndex);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressedCard = null;
        EndDrag(null);
    }

    private void StartDrag(RiceKanbanCard card, PointerEventArgs e)
    {
        if (ColumnOf(card) is not { } column || !column.CanChangeItems)
        {
            _pressedCard = null;
            return;
        }
        var grab = e.GetPosition(card);
        _drag = new DragState(column.ItemFromContainer(card), column, grab);
        CreateGhost(card, _drag);
        card.SetDragging(true);
        // Captured by the board, not the card: moving the item detaches its container, which would lose the capture.
        e.Pointer.Capture(this);
        Cursor = new Cursor(StandardCursorType.DragMove);
    }

    // A picture of the card in the adorner layer follows the pointer; the card itself stays as a faded place holder.
    private void CreateGhost(RiceKanbanCard card, DragState drag)
    {
        if (AdornerLayer.GetAdornerLayer(this) is not { } layer || card.Bounds.Width <= 0 || card.Bounds.Height <= 0)
            return;
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var size = card.Bounds.Size;
        var bitmap = new RenderTargetBitmap(
            new PixelSize((int)Math.Ceiling(size.Width * scale), (int)Math.Ceiling(size.Height * scale)),
            new Vector(96 * scale, 96 * scale));
        bitmap.Render(card);
        var ghost = new Border
        {
            Width = size.Width,
            Height = size.Height,
            CornerRadius = card.CornerRadius,
            Child = new Image { Source = bitmap, Width = size.Width, Height = size.Height },
            Opacity = 0.92,
            RenderTransform = new RotateTransform(2),
        };
        if (this.TryFindResource("RicePopupShadow", ActualThemeVariant, out var shadow) && shadow is BoxShadows shadows)
            ghost.BoxShadow = shadows;
        var host = new Canvas { IsHitTestVisible = false, Children = { ghost } };
        AdornerLayer.SetAdornedElement(host, this);
        layer.Children.Add(host);
        drag.GhostHost = host;
        drag.Ghost = ghost;
    }

    private void MoveGhost(Point position)
    {
        if (_drag?.Ghost is not { } ghost)
            return;
        Canvas.SetLeft(ghost, position.X - _drag.GrabOffset.X);
        Canvas.SetTop(ghost, position.Y - _drag.GrabOffset.Y);
    }

    private void UpdateDropTarget(Point position)
    {
        if (_drag is not { } drag)
            return;
        var column = Columns.FirstOrDefault(c => c.IsEffectivelyVisible
                                                 && new Rect(c.Bounds.Size).Contains(this.TranslatePoint(position, c) ?? default));
        if (column != drag.Target)
            drag.Target?.HideDropIndicator();
        drag.Target = column;
        drag.TargetIndex = -1;
        if (column is null)
            return;
        if (column != drag.Source && !CanDropInto(column))
        {
            column.ShowDropRefused();
            return;
        }
        drag.TargetIndex = column.InsertIndexAt(this.TranslatePoint(position, column) ?? default);
        column.ShowDropIndicator(drag.TargetIndex);
    }

    private void AutoScroll(Point position)
    {
        if (_scrollViewer is not null)
        {
            var x = this.TranslatePoint(position, _scrollViewer)?.X ?? 0;
            if (x < AutoScrollEdge)
                _scrollViewer.Offset -= new Vector(AutoScrollStep, 0);
            else if (x > _scrollViewer.Bounds.Width - AutoScrollEdge)
                _scrollViewer.Offset += new Vector(AutoScrollStep, 0);
        }
        if (_drag?.Target?.CardsScrollViewer is { } cards)
        {
            var y = this.TranslatePoint(position, cards)?.Y ?? 0;
            if (y < AutoScrollEdge && y > -AutoScrollEdge)
                cards.Offset -= new Vector(0, AutoScrollStep);
            else if (y > cards.Bounds.Height - AutoScrollEdge && y < cards.Bounds.Height + AutoScrollEdge)
                cards.Offset += new Vector(0, AutoScrollStep);
        }
    }

    private void EndDrag(IPointer? pointer)
    {
        if (_drag is not { } drag)
            return;
        _drag = null;
        drag.Target?.HideDropIndicator();
        if (drag.GhostHost is { } host)
            (host.Parent as Panel)?.Children.Remove(host);
        foreach (var card in AllCards())
            card.SetDragging(false);
        Cursor = null;
        pointer?.Capture(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;
        if (e.Key == Key.Escape && _drag is not null)
        {
            // Cancels the drag: the card stays where it was.
            EndDrag(null);
            e.Handled = true;
            return;
        }
        if (e.Source is not Visual source || source.FindAncestorOfType<RiceKanbanCard>(includeSelf: true) is not { } card
            || ColumnOf(card) is not { } column || column.Board != this)
            return;
        var index = column.IndexFromContainer(card);
        var columns = Columns.ToList();
        var columnIndex = columns.IndexOf(column);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        e.Handled = (e.Key, ctrl) switch
        {
            (Key.Enter, false) => Activate(column, card),
            (Key.Up, false) => FocusCard(column, index - 1),
            (Key.Down, false) => FocusCard(column, index + 1),
            (Key.Left, false) => columnIndex > 0 && FocusCard(columns[columnIndex - 1], Math.Min(index, columns[columnIndex - 1].ItemCount - 1)),
            (Key.Right, false) => columnIndex < columns.Count - 1 && FocusCard(columns[columnIndex + 1], Math.Min(index, columns[columnIndex + 1].ItemCount - 1)),
            (Key.Up, true) when CanDragCards => MoveCard(column, index, column, index - 1),
            (Key.Down, true) when CanDragCards => MoveCard(column, index, column, index + 2),
            (Key.Left, true) when CanDragCards && columnIndex > 0 => MoveCard(column, index, columns[columnIndex - 1], index),
            (Key.Right, true) when CanDragCards && columnIndex < columns.Count - 1 => MoveCard(column, index, columns[columnIndex + 1], index),
            _ => false,
        };
    }

    private bool Activate(RiceKanbanColumn column, RiceKanbanCard card)
    {
        RaiseEvent(new RiceKanbanCardEventArgs(CardActivatedEvent, column.ItemFromContainer(card), column));
        return true;
    }

    private bool FocusCard(RiceKanbanColumn column, int index)
    {
        if (index < 0 || index >= column.ItemCount || column.ContainerFromIndex(index) is not RiceKanbanCard card)
            return false;
        SelectedCard = column.ItemFromContainer(card);
        card.Focus(NavigationMethod.Directional);
        card.BringIntoView();
        return true;
    }
}

public class RiceKanbanCardEventArgs(RoutedEvent routedEvent, object? item, RiceKanbanColumn column) : RoutedEventArgs(routedEvent)
{
    /// <summary>The card's item (the data item, or the <see cref="RiceKanbanCard"/> declared in XAML).</summary>
    public object? Item { get; } = item;

    public RiceKanbanColumn Column { get; } = column;
}

public class RiceKanbanCardMovedEventArgs(RoutedEvent routedEvent, object? item, RiceKanbanColumn sourceColumn, int sourceIndex,
    RiceKanbanColumn targetColumn, int targetIndex) : RoutedEventArgs(routedEvent)
{
    /// <summary>The moved item (the data item, or the <see cref="RiceKanbanCard"/> declared in XAML).</summary>
    public object? Item { get; } = item;

    public RiceKanbanColumn SourceColumn { get; } = sourceColumn;

    public int SourceIndex { get; } = sourceIndex;

    public RiceKanbanColumn TargetColumn { get; } = targetColumn;

    /// <summary>The index of the card in the target column after the move.</summary>
    public int TargetIndex { get; } = targetIndex;
}

public class RiceKanbanCardMovingEventArgs(RoutedEvent routedEvent, object? item, RiceKanbanColumn sourceColumn, int sourceIndex,
    RiceKanbanColumn targetColumn, int targetIndex)
    : RiceKanbanCardMovedEventArgs(routedEvent, item, sourceColumn, sourceIndex, targetColumn, targetIndex)
{
    /// <summary>Keeps the card where it was.</summary>
    public bool Cancel { get; set; }
}
