using System.Collections;
using System.Collections.ObjectModel;
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
/// With <see cref="CanTearOffTabs"/> a tab dragged out of the strip becomes a window of its own, or moves into the tab
/// strip of another view of the same <see cref="TabGroup"/> it is dropped on.
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

    /// <summary>Lets tabs be dragged out of the strip: into a new window, or into another view of the same <see cref="TabGroup"/>.</summary>
    public static readonly StyledProperty<bool> CanTearOffTabsProperty =
        AvaloniaProperty.Register<RiceTabView, bool>(nameof(CanTearOffTabs));

    /// <summary>Views exchange dragged tabs only within the same group (null is a group too).</summary>
    public static readonly StyledProperty<string?> TabGroupProperty =
        AvaloniaProperty.Register<RiceTabView, string?>(nameof(TabGroup));

    public static readonly RoutedEvent<RiceTabTearOffEventArgs> TabTearOffRequestedEvent =
        RoutedEvent.Register<RiceTabView, RiceTabTearOffEventArgs>(nameof(TabTearOffRequested), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RiceTabWindowCreatedEventArgs> TearOffWindowCreatedEvent =
        RoutedEvent.Register<RiceTabView, RiceTabWindowCreatedEventArgs>(nameof(TearOffWindowCreated), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RiceTabCloseRequestedEventArgs> TabCloseRequestedEvent =
        RoutedEvent.Register<RiceTabView, RiceTabCloseRequestedEventArgs>(nameof(TabCloseRequested), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> AddTabRequestedEvent =
        RoutedEvent.Register<RiceTabView, RoutedEventArgs>(nameof(AddTabRequested), RoutingStrategies.Bubble);

    // The pointer must move this far before a press on a tab becomes a drag, and this far out of the strip to tear it off.
    private const double DragThreshold = 6;
    private const double TearOffDistance = 32;

    // The views on screen, to find the one a torn off tab is dropped on.
    private static readonly List<WeakReference<RiceTabView>> AttachedViews = [];

    private Control? _tabStrip;
    private bool _outside;
    private bool _closeWindowWhenEmpty;

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

    public bool CanTearOffTabs
    {
        get => GetValue(CanTearOffTabsProperty);
        set => SetValue(CanTearOffTabsProperty, value);
    }

    public string? TabGroup
    {
        get => GetValue(TabGroupProperty);
        set => SetValue(TabGroupProperty, value);
    }

    /// <summary>
    /// Raised when a tab is dropped outside the strip, before it moves (<c>TargetView</c> set) or gets a window of its own.
    /// Set <c>Cancel</c> to keep it, or <c>Handled</c> to place it yourself.
    /// </summary>
    public event EventHandler<RiceTabTearOffEventArgs>? TabTearOffRequested
    {
        add => AddHandler(TabTearOffRequestedEvent, value);
        remove => RemoveHandler(TabTearOffRequestedEvent, value);
    }

    /// <summary>Raised on the source view after a torn off tab got its window, before it is shown: wire up the new view here.</summary>
    public event EventHandler<RiceTabWindowCreatedEventArgs>? TearOffWindowCreated
    {
        add => AddHandler(TearOffWindowCreatedEvent, value);
        remove => RemoveHandler(TearOffWindowCreatedEvent, value);
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
        _tabStrip = e.NameScope.Find<Control>("PART_TabStrip");
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
        if (RemoveItem(index))
            CloseWindowIfEmpty();
    }

    private bool RemoveItem(int index)
    {
        var wasSelected = index == SelectedIndex;
        if (ItemsSource is IList { IsReadOnly: false, IsFixedSize: false } source)
            source.RemoveAt(index);
        else if (ItemsSource is null)
            Items.RemoveAt(index);
        else
            return false;
        // The neighbour on the right (else the left) takes over, like in browsers.
        if (wasSelected && ItemCount > 0)
            SelectedIndex = Math.Min(index, ItemCount - 1);
        return true;
    }

    // False when the item cannot be added, e.g. a typed collection of another type.
    private bool InsertItem(int index, object? item)
    {
        index = index < 0 ? ItemCount : Math.Min(index, ItemCount);
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
        SelectedIndex = index;
        return true;
    }

    private bool CanChangeItems => ItemsSource is null or IList { IsReadOnly: false, IsFixedSize: false };

    // A window made for torn off tabs goes away with its last tab.
    private void CloseWindowIfEmpty()
    {
        if (_closeWindowWhenEmpty && ItemCount == 0 && TopLevel.GetTopLevel(this) is Window window)
            window.Close();
    }

    /// <summary>
    /// Moves the tab at <paramref name="index"/> into <paramref name="target"/> at <paramref name="targetIndex"/>
    /// (-1: at the end) and selects it there. False when the target cannot take it.
    /// </summary>
    public bool MoveTab(int index, RiceTabView target, int targetIndex = -1)
    {
        if (target == this || index < 0 || index >= ItemCount || !CanChangeItems || !target.CanChangeItems)
            return false;
        var item = Items[index];
        if (!RemoveItem(index))
            return false;
        if (!target.InsertItem(targetIndex, item))
        {
            InsertItem(index, item);
            return false;
        }
        (TopLevel.GetTopLevel(target) as Window)?.Activate();
        CloseWindowIfEmpty();
        return true;
    }

    /// <summary>
    /// Moves the tab at <paramref name="index"/> into a new window (<see cref="CreateTearOffWindow"/>) at
    /// <paramref name="position"/> (screen pixels, where the tab should be) and shows it. Null when it cannot be moved.
    /// </summary>
    public Window? TearOff(int index, PixelPoint? position = null)
    {
        if (index < 0 || index >= ItemCount || !CanChangeItems)
            return null;
        var view = CreateTearOffView();
        view._closeWindowWhenEmpty = true;
        var window = CreateTearOffWindow(view, position);
        if (!MoveTab(index, view))
            return null;
        RaiseEvent(new RiceTabWindowCreatedEventArgs(TearOffWindowCreatedEvent, window, view));
        window.Show();
        return window;
    }

    /// <summary>
    /// The view of a torn off tab: the settings and templates of this one, and an empty collection of the same type
    /// as <c>ItemsSource</c> (an <see cref="ObservableCollection{T}"/> of objects when it cannot be created).
    /// </summary>
    protected virtual RiceTabView CreateTearOffView()
    {
        var view = new RiceTabView
        {
            ShowAddButton = ShowAddButton,
            AddTabCommand = AddTabCommand,
            CanReorderTabs = CanReorderTabs,
            CanCloseTabs = CanCloseTabs,
            CanTearOffTabs = CanTearOffTabs,
            TabGroup = TabGroup,
            ItemTemplate = ItemTemplate,
            ContentTemplate = ContentTemplate,
            CornerRadius = new CornerRadius(0),
            BorderThickness = new Thickness(0),
        };
        if (ItemsSource is IList source)
            view.ItemsSource = NewListLike(source);
        return view;
    }

    /// <summary>The window of a torn off tab: a <see cref="RiceWindow"/> as large as this view, with the same title and data context.</summary>
    protected virtual Window CreateTearOffWindow(RiceTabView view, PixelPoint? position)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var window = new RiceWindow
        {
            Content = view,
            Title = owner?.Title,
            DataContext = DataContext,
            Width = Math.Max(Bounds.Width, 360),
            Height = Math.Max(Bounds.Height, 240) + 40,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };
        // The tab lands under the pointer: the strip starts below the title bar, the tab a little to the left.
        if (position is { } p)
            window.Position = new PixelPoint(Math.Max(p.X - 80, 0), Math.Max(p.Y - 56, 0));
        else if (owner is not null)
            window.Position = owner.Position + new PixelVector(40, 40);
        return window;
    }

    private static IList NewListLike(IList source)
    {
        var type = source.GetType();
        if (!type.IsArray && type.GetConstructor(Type.EmptyTypes) is not null
            && Activator.CreateInstance(type) is IList { IsReadOnly: false, IsFixedSize: false } list)
            return list;
        return new ObservableCollection<object?>();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        lock (AttachedViews)
            AttachedViews.Add(new WeakReference<RiceTabView>(this));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        lock (AttachedViews)
            AttachedViews.RemoveAll(r => !r.TryGetTarget(out var view) || view == this);
    }

    // The view of the same group whose tab strip is under the screen point.
    private RiceTabView? FindDropTarget(PixelPoint screen)
    {
        List<RiceTabView> views;
        lock (AttachedViews)
            views = AttachedViews.Select(r => r.TryGetTarget(out var v) ? v : null).OfType<RiceTabView>().ToList();
        return views.FirstOrDefault(v => v != this && v.CanTearOffTabs && v.TabGroup == TabGroup
                                         && v.IsEffectivelyVisible && v.StripContains(screen));
    }

    private bool StripContains(PixelPoint screen)
    {
        if (_tabStrip is null || TopLevel.GetTopLevel(_tabStrip) is null)
            return false;
        var point = _tabStrip.PointToClient(screen);
        return new Rect(_tabStrip.Bounds.Size).Inflate(new Thickness(0, TearOffDistance / 2)).Contains(point);
    }

    // Where a tab dropped at the screen point goes: before the tab whose middle is right of it, else at the end.
    private int InsertIndexAt(PixelPoint screen)
    {
        _tabsPanel ??= this.GetVisualDescendants().OfType<ItemsPresenter>().FirstOrDefault()?.Panel;
        if (_tabsPanel is null)
            return -1;
        var x = _tabsPanel.PointToClient(screen).X;
        var tab = _tabsPanel.Children.OfType<RiceTabViewItem>().FirstOrDefault(t => x < t.Bounds.Center.X);
        return tab is null ? -1 : IndexFromContainer(tab);
    }

    private bool IsOutsideStrip(PointerEventArgs e)
    {
        if (_tabStrip is null)
            return false;
        var p = e.GetPosition(_tabStrip);
        var size = _tabStrip.Bounds.Size;
        return p.Y < -TearOffDistance || p.Y > size.Height + TearOffDistance
               || p.X < -TearOffDistance || p.X > size.Width + TearOffDistance;
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
            var moved = position - _dragStart;
            if (Math.Abs(moved.X) < DragThreshold && (!CanTearOffTabs || Math.Abs(moved.Y) < DragThreshold))
                return;
            // Captured by the tab view, not the tab: moving the item detaches its container, which would lose the capture.
            _dragging = true;
            e.Pointer.Capture(this);
            _dragTab.SetDragging(true);
        }

        // Out of the strip the tab is being torn off; back in, it is reordered again.
        _outside = CanTearOffTabs && IsOutsideStrip(e);
        _dragTab.SetTearing(_outside);
        Cursor = _outside ? new Cursor(StandardCursorType.DragMove) : null;
        if (!_outside)
        {
            var from = IndexFromContainer(_dragTab);
            var to = TabIndexAt(e);
            if (from >= 0 && to >= 0 && to != from)
                Move(from, to);
        }
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var dropped = _dragging && _outside && _dragTab is not null ? IndexFromContainer(_dragTab) : -1;
        var screen = this.PointToScreen(e.GetPosition(this));
        EndDrag(e.Pointer);
        if (dropped >= 0)
            DropOutside(dropped, screen);
    }

    private void DropOutside(int index, PixelPoint screen)
    {
        var target = FindDropTarget(screen);
        var args = new RiceTabTearOffEventArgs(TabTearOffRequestedEvent, Items[index], screen, target);
        RaiseEvent(args);
        if (args.Cancel || args.Handled)
            return;
        if (target is not null)
            MoveTab(index, target, target.InsertIndexAt(screen));
        else
            TearOff(index, screen);
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
        _dragTab.SetTearing(false);
        _dragTab = null;
        _outside = false;
        Cursor = null;
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

public class RiceTabTearOffEventArgs(RoutedEvent routedEvent, object? item, PixelPoint screenPosition, RiceTabView? targetView)
    : RoutedEventArgs(routedEvent)
{
    /// <summary>The dragged item (the data item, or the <see cref="RiceTabViewItem"/> declared in XAML).</summary>
    public object? Item { get; } = item;

    /// <summary>Where it was dropped, in screen pixels.</summary>
    public PixelPoint ScreenPosition { get; } = screenPosition;

    /// <summary>The view whose tab strip it was dropped on; null when it gets a window of its own.</summary>
    public RiceTabView? TargetView { get; } = targetView;

    /// <summary>Keeps the tab where it was.</summary>
    public bool Cancel { get; set; }
}

public class RiceTabWindowCreatedEventArgs(RoutedEvent routedEvent, Window window, RiceTabView view) : RoutedEventArgs(routedEvent)
{
    public Window Window { get; } = window;

    /// <summary>The tab view in the new window, holding the torn off tab.</summary>
    public RiceTabView View { get; } = view;
}
