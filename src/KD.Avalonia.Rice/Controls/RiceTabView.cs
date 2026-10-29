using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Tabs like a browser or an editor: every <see cref="RiceTabViewItem"/> has an optional icon and a close button
/// (middle click and Ctrl+W close too), the tabs can be reordered by dragging, and an optional "+" button asks for a
/// new tab. A <see cref="TabControl"/> underneath: bind <c>ItemsSource</c> / <c>SelectedItem</c> as usual.
/// Closing removes the item from <c>Items</c> or from an <c>ItemsSource</c> that is a modifiable <see cref="IList"/>,
/// unless a <see cref="TabCloseRequested"/> handler cancels it.
/// </summary>
public class RiceTabView : TabControl
{
    public static readonly StyledProperty<bool> ShowAddButtonProperty =
        AvaloniaProperty.Register<RiceTabView, bool>(nameof(ShowAddButton));

    public static readonly StyledProperty<ICommand?> AddTabCommandProperty =
        AvaloniaProperty.Register<RiceTabView, ICommand?>(nameof(AddTabCommand));

    public static readonly StyledProperty<bool> CanReorderTabsProperty =
        AvaloniaProperty.Register<RiceTabView, bool>(nameof(CanReorderTabs), true);

    /// <summary>Default of <see cref="RiceTabViewItem.IsClosable"/> for the tabs of this view (inherited).</summary>
    public static readonly StyledProperty<bool> CanCloseTabsProperty =
        AvaloniaProperty.Register<RiceTabView, bool>(nameof(CanCloseTabs), true, inherits: true);

    /// <summary>Content at the right end of the tab strip, after the add button (e.g. a menu button).</summary>
    public static readonly StyledProperty<object?> TabStripFooterProperty =
        AvaloniaProperty.Register<RiceTabView, object?>(nameof(TabStripFooter));

    public static readonly RoutedEvent<RiceTabCloseRequestedEventArgs> TabCloseRequestedEvent =
        RoutedEvent.Register<RiceTabView, RiceTabCloseRequestedEventArgs>(nameof(TabCloseRequested), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> AddTabRequestedEvent =
        RoutedEvent.Register<RiceTabView, RoutedEventArgs>(nameof(AddTabRequested), RoutingStrategies.Bubble);

    // The pointer must move this far before a press on a tab becomes a drag.
    private const double DragThreshold = 6;

    private Button? _addButton;
    private Panel? _tabsPanel;
    private RiceTabViewItem? _dragTab;
    private Point _dragStart;
    private bool _dragging;

    public RiceTabView()
    {
        AddHandler(PointerPressedEvent, OnTabPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>Shows the "+" button after the tabs; it raises <see cref="AddTabRequested"/> and runs <see cref="AddTabCommand"/>.</summary>
    public bool ShowAddButton
    {
        get => GetValue(ShowAddButtonProperty);
        set => SetValue(ShowAddButtonProperty, value);
    }

    public ICommand? AddTabCommand
    {
        get => GetValue(AddTabCommandProperty);
        set => SetValue(AddTabCommandProperty, value);
    }

    /// <summary>Lets the tabs be dragged to a new position. Default true.</summary>
    public bool CanReorderTabs
    {
        get => GetValue(CanReorderTabsProperty);
        set => SetValue(CanReorderTabsProperty, value);
    }

    public bool CanCloseTabs
    {
        get => GetValue(CanCloseTabsProperty);
        set => SetValue(CanCloseTabsProperty, value);
    }

    public object? TabStripFooter
    {
        get => GetValue(TabStripFooterProperty);
        set => SetValue(TabStripFooterProperty, value);
    }

    /// <summary>Raised before a tab closes; set <c>Cancel</c> to keep it, or <c>Handled</c> to remove it yourself.</summary>
    public event EventHandler<RiceTabCloseRequestedEventArgs>? TabCloseRequested
    {
        add => AddHandler(TabCloseRequestedEvent, value);
        remove => RemoveHandler(TabCloseRequestedEvent, value);
    }

    public event EventHandler<RoutedEventArgs>? AddTabRequested
    {
        add => AddHandler(AddTabRequestedEvent, value);
        remove => RemoveHandler(AddTabRequestedEvent, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceTabView);

    /// <summary>Closes the tab at <paramref name="index"/> as its close button would. False when it was cancelled.</summary>
    public bool CloseTab(int index)
    {
        if (index < 0 || index >= ItemCount)
            return false;
        var item = Items[index];
        var args = new RiceTabCloseRequestedEventArgs(TabCloseRequestedEvent, item, ContainerFromIndex(index));
        RaiseEvent(args);
        if (args.Cancel)
            return false;
        if (!args.Handled)
            RemoveAt(index);
        return true;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new RiceTabViewItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<RiceTabViewItem>(item, out recycleKey);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _addButton?.Click -= OnAddButtonClick;
        _addButton = e.NameScope.Find<Button>("PART_AddButton");
        _addButton?.Click += OnAddButtonClick;
        if (_addButton is not null)
            ToolTip.SetTip(_addButton, RiceStrings.TabAdd);
        _tabsPanel = null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.Control)
            return;
        switch (e.Key)
        {
            case Key.W when SelectedIndex >= 0 && ContainerFromIndex(SelectedIndex) is RiceTabViewItem { IsClosable: true }:
                CloseTab(SelectedIndex);
                break;
            case Key.PageDown when ItemCount > 0:
                SelectedIndex = (SelectedIndex + 1) % ItemCount;
                break;
            case Key.PageUp when ItemCount > 0:
                SelectedIndex = (SelectedIndex - 1 + ItemCount) % ItemCount;
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    internal void OnCloseRequested(RiceTabViewItem tab)
    {
        var index = IndexFromContainer(tab);
        if (index >= 0)
            CloseTab(index);
    }

    private void OnAddButtonClick(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(AddTabRequestedEvent));
        if (AddTabCommand?.CanExecute(null) == true)
            AddTabCommand.Execute(null);
    }

    private void RemoveAt(int index)
    {
        var wasSelected = index == SelectedIndex;
        if (ItemsSource is IList { IsReadOnly: false, IsFixedSize: false } source)
            source.RemoveAt(index);
        else if (ItemsSource is null)
            Items.RemoveAt(index);
        else
            return;
        // The neighbour on the right (else the left) takes over, like in browsers.
        if (wasSelected && ItemCount > 0)
            SelectedIndex = Math.Min(index, ItemCount - 1);
    }

    private void Move(int from, int to)
    {
        var selected = SelectedItem;
        if (ItemsSource is IList { IsReadOnly: false, IsFixedSize: false } source)
        {
            var item = source[from];
            source.RemoveAt(from);
            source.Insert(to, item);
        }
        else if (ItemsSource is null)
        {
            var item = Items[from];
            Items.RemoveAt(from);
            Items.Insert(to, item);
        }
        SelectedItem = selected;
    }

    private void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Visual source || source.FindAncestorOfType<RiceTabViewItem>(includeSelf: true) is not { } tab
            || ItemsControlFromItemContainer(tab) != this || source.FindAncestorOfType<Button>(includeSelf: true) is not null)
            return;
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsMiddleButtonPressed)
        {
            if (tab.IsClosable)
                OnCloseRequested(tab);
            e.Handled = true;
        }
        else if (point.Properties.IsLeftButtonPressed && CanReorderTabs)
        {
            _dragTab = tab;
            _dragStart = point.Position;
            _dragging = false;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragTab is null)
            return;
        var position = e.GetPosition(this);
        if (!_dragging)
        {
            if (Math.Abs(position.X - _dragStart.X) < DragThreshold)
                return;
            // Captured by the tab view, not the tab: moving the item detaches its container, which would lose the capture.
            _dragging = true;
            e.Pointer.Capture(this);
            _dragTab.SetDragging(true);
        }

        var from = IndexFromContainer(_dragTab);
        var to = TabIndexAt(e);
        if (from >= 0 && to >= 0 && to != from)
            Move(from, to);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndDrag(e.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndDrag(null);
    }

    private void EndDrag(IPointer? pointer)
    {
        if (_dragTab is null)
            return;
        _dragTab.SetDragging(false);
        _dragTab = null;
        if (_dragging)
            pointer?.Capture(null);
        _dragging = false;
    }

    // The index of the tab under the pointer; past the ends, the first or last one.
    private int TabIndexAt(PointerEventArgs e)
    {
        _tabsPanel ??= this.GetVisualDescendants().OfType<ItemsPresenter>().FirstOrDefault()?.Panel;
        if (_tabsPanel is null)
            return -1;
        var x = e.GetPosition(_tabsPanel).X;
        var tabs = _tabsPanel.Children.OfType<RiceTabViewItem>().Where(t => t.IsVisible).ToList();
        if (tabs.Count == 0)
            return -1;
        var target = tabs.FirstOrDefault(t => x >= t.Bounds.Left && x < t.Bounds.Right)
                     ?? (x < tabs[0].Bounds.Left ? tabs[0] : tabs[^1]);
        return IndexFromContainer(target);
    }
}

public class RiceTabCloseRequestedEventArgs(RoutedEvent routedEvent, object? item, Control? container) : RoutedEventArgs(routedEvent)
{
    /// <summary>The closing item (the data item, or the <see cref="RiceTabViewItem"/> declared in XAML).</summary>
    public object? Item { get; } = item;

    public Control? Container { get; } = container;

    /// <summary>Keeps the tab open.</summary>
    public bool Cancel { get; set; }
}
