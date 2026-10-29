using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// One column (list) of a <see cref="RiceKanbanBoard"/>: a header with the card count and the cards below it, which
/// can be dragged within the column and to the other columns. Bind <c>ItemsSource</c> to a modifiable list (e.g. an
/// <c>ObservableCollection</c>) or add <see cref="RiceKanbanCard"/>s / other items to <c>Items</c>.
/// Pseudo classes: <c>:empty</c>, <c>:at-limit</c> (as many cards as <see cref="WipLimit"/>), <c>:over-limit</c>,
/// <c>:drop-target</c> (a dragged card would land here) and <c>:drop-refused</c> (it cannot be dropped here).
/// </summary>
public class RiceKanbanColumn : HeaderedItemsControl
{
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<RiceKanbanColumn, object?>(nameof(Icon));

    public static readonly StyledProperty<IBrush?> AccentProperty =
        AvaloniaProperty.Register<RiceKanbanColumn, IBrush?>(nameof(Accent));

    public static readonly StyledProperty<int> WipLimitProperty =
        AvaloniaProperty.Register<RiceKanbanColumn, int>(nameof(WipLimit), coerce: (_, value) => Math.Max(value, 0));

    public static readonly StyledProperty<object?> HeaderActionsProperty =
        AvaloniaProperty.Register<RiceKanbanColumn, object?>(nameof(HeaderActions));

    public static readonly StyledProperty<bool> AllowDropProperty =
        AvaloniaProperty.Register<RiceKanbanColumn, bool>(nameof(AllowDrop), true);

    public static readonly StyledProperty<IDataTemplate?> CardTemplateProperty =
        RiceKanbanBoard.CardTemplateProperty.AddOwner<RiceKanbanColumn>();

    public static readonly StyledProperty<bool> ShowAddCardButtonProperty =
        RiceKanbanBoard.ShowAddCardButtonProperty.AddOwner<RiceKanbanColumn>();

    public static readonly DirectProperty<RiceKanbanColumn, string> CountTextProperty =
        AvaloniaProperty.RegisterDirect<RiceKanbanColumn, string>(nameof(CountText), o => o.CountText);

    private string _countText = "0";
    private Button? _addButton;
    private Canvas? _dropLayer;
    private Border? _dropIndicator;
    private IDisposable? _childrenBinding;

    public RiceKanbanColumn()
    {
        UpdateCount();
    }

    /// <summary>An <c>IImage</c>, a <c>MaterialIconKind</c>, a string (e.g. an emoji) or any control before the header.</summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Color of the dot before the header; none when null.</summary>
    public IBrush? Accent
    {
        get => GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>
    /// Work in progress limit: the count shows "cards / limit" and turns red when it is exceeded; with
    /// <see cref="RiceKanbanBoard.EnforceWipLimits"/> no card can be moved into a full column. 0 = no limit.
    /// </summary>
    public int WipLimit
    {
        get => GetValue(WipLimitProperty);
        set => SetValue(WipLimitProperty, value);
    }

    /// <summary>Right side of the header, e.g. a menu button.</summary>
    public object? HeaderActions
    {
        get => GetValue(HeaderActionsProperty);
        set => SetValue(HeaderActionsProperty, value);
    }

    /// <summary>Lets cards be dropped into this column. Default true.</summary>
    public bool AllowDrop
    {
        get => GetValue(AllowDropProperty);
        set => SetValue(AllowDropProperty, value);
    }

    /// <summary>Template of the data cards when <c>ItemTemplate</c> is not set; inherited from the board.</summary>
    public IDataTemplate? CardTemplate
    {
        get => GetValue(CardTemplateProperty);
        set => SetValue(CardTemplateProperty, value);
    }

    public bool ShowAddCardButton
    {
        get => GetValue(ShowAddCardButtonProperty);
        set => SetValue(ShowAddCardButtonProperty, value);
    }

    /// <summary>"3", or "3 / 5" with a <see cref="WipLimit"/>.</summary>
    public string CountText
    {
        get => _countText;
        private set => SetAndRaise(CountTextProperty, ref _countText, value);
    }

    /// <summary>The board of the column; null when it is used on its own.</summary>
    public RiceKanbanBoard? Board => ItemsControlFromItemContainer(this) as RiceKanbanBoard;

    /// <summary>Whether the cards can be moved: <c>Items</c>, or an <c>ItemsSource</c> that is a modifiable <see cref="IList"/>.</summary>
    public bool CanChangeItems => ItemsSource is null or IList { IsReadOnly: false, IsFixedSize: false };

    protected override Type StyleKeyOverride => typeof(RiceKanbanColumn);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new RiceKanbanCard();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<RiceKanbanCard>(item, out recycleKey);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is not RiceKanbanCard card)
            return;
        if (container != item && ItemTemplate is null && CardTemplate is { } template && !card.IsSet(ContentControl.ContentTemplateProperty))
            card.ContentTemplate = template;
        if (Board is { } board)
        {
            card.IsSelected = board.SelectedCard is { } selected && Equals(selected, item);
            card.SetDragging(board.IsDraggingItem(item));
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _addButton?.Click -= OnAddButtonClick;
        _addButton = e.NameScope.Find<Button>("PART_AddButton");
        _addButton?.Click += OnAddButtonClick;
        _dropLayer = e.NameScope.Find<Canvas>("PART_DropLayer");
        _dropIndicator = e.NameScope.Find<Border>("PART_DropIndicator");
        if (_addButton is not null && _addButton.Content is null)
            _addButton.Content = RiceStrings.KanbanAddCard;
        e.NameScope.Find<TextBlock>("PART_EmptyText")?.Text = RiceStrings.KanbanEmptyColumn;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemCountProperty || change.Property == WipLimitProperty)
            UpdateCount();
    }

    private void UpdateCount()
    {
        var count = ItemCount;
        CountText = WipLimit > 0 ? $"{count.ToString(CultureInfo.CurrentCulture)} / {WipLimit.ToString(CultureInfo.CurrentCulture)}"
                                 : count.ToString(CultureInfo.CurrentCulture);
        PseudoClasses.Set(":empty", count == 0);
        PseudoClasses.Set(":at-limit", WipLimit > 0 && count == WipLimit);
        PseudoClasses.Set(":over-limit", WipLimit > 0 && count > WipLimit);
    }

    private void OnAddButtonClick(object? sender, RoutedEventArgs e) =>
        RaiseEvent(new RiceKanbanColumnEventArgs(RiceKanbanBoard.AddCardRequestedEvent, this));

    // The board binds the cards of a data column from its TreeDataTemplate; released with the container.
    internal void SetChildrenBinding(IDisposable? binding)
    {
        _childrenBinding?.Dispose();
        _childrenBinding = binding;
    }

    internal Panel? CardsPanel => this.GetVisualDescendants().OfType<ItemsPresenter>().FirstOrDefault()?.Panel;

    internal ScrollViewer? CardsScrollViewer => this.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();

    /// <summary>Where a card dropped at <paramref name="point"/> (in column coordinates) goes: before the first card whose middle is below it.</summary>
    internal int InsertIndexAt(Point point)
    {
        if (CardsPanel is not { } panel)
            return ItemCount;
        var y = this.TranslatePoint(point, panel)?.Y ?? 0;
        var cards = panel.Children.OfType<RiceKanbanCard>().Where(c => c.IsVisible).ToList();
        var before = cards.FirstOrDefault(c => y < c.Bounds.Center.Y);
        return before is null ? ItemCount : IndexFromContainer(before);
    }

    internal void ShowDropIndicator(int index)
    {
        PseudoClasses.Set(":drop-target", true);
        PseudoClasses.Set(":drop-refused", false);
        if (_dropLayer is null || _dropIndicator is null || CardsPanel is not { } panel)
            return;
        var cards = panel.Children.OfType<RiceKanbanCard>().Where(c => c.IsVisible).OrderBy(c => c.Bounds.Top).ToList();
        var spacing = panel is StackPanel stack ? stack.Spacing : 0;
        double y;
        if (cards.Count == 0)
            y = 0;
        else if (index < ItemCount && ContainerFromIndex(index) is RiceKanbanCard card)
            y = card.Bounds.Top - spacing / 2;
        else
            y = cards[^1].Bounds.Bottom + spacing / 2;
        var top = panel.TranslatePoint(new Point(0, y), _dropLayer)?.Y ?? 0;
        Canvas.SetTop(_dropIndicator, top - _dropIndicator.Height / 2);
        _dropIndicator.Width = _dropLayer.Bounds.Width;
        _dropIndicator.IsVisible = true;
    }

    internal void ShowDropRefused()
    {
        HideDropIndicator();
        PseudoClasses.Set(":drop-refused", true);
    }

    internal void HideDropIndicator()
    {
        PseudoClasses.Set(":drop-target", false);
        PseudoClasses.Set(":drop-refused", false);
        _dropIndicator?.IsVisible = false;
    }

    internal object? RemoveAt(int index)
    {
        if (ItemsSource is IList { IsReadOnly: false, IsFixedSize: false } source)
        {
            var item = source[index];
            source.RemoveAt(index);
            return item;
        }
        var own = Items[index];
        Items.RemoveAt(index);
        return own;
    }

    // False when the item cannot be added, e.g. a typed collection of another type.
    internal bool Insert(int index, object? item)
    {
        index = Math.Clamp(index, 0, ItemCount);
        try
        {
            if (ItemsSource is IList { IsReadOnly: false, IsFixedSize: false } source)
                source.Insert(index, item);
            else if (ItemsSource is null)
                Items.Insert(index, item);
            else
                return false;
        }
        catch (Exception e) when (e is ArgumentException or InvalidCastException or NotSupportedException)
        {
            return false;
        }
        return true;
    }

    internal object? ItemAt(int index) => index >= 0 && index < ItemCount ? Items[index] : null;
}

public class RiceKanbanColumnEventArgs(RoutedEvent routedEvent, RiceKanbanColumn column) : RoutedEventArgs(routedEvent)
{
    public RiceKanbanColumn Column { get; } = column;
}
