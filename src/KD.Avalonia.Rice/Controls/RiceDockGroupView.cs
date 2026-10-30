using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// A tab group of a <see cref="RiceDockLayout"/> on screen: the title bar of the selected pane (tool windows), the
/// tabs (on top for documents, at the bottom for two or more tool windows) and the selected pane.
/// Dragging the title moves the whole group, dragging a tab only its pane (along the tabs it reorders them).
/// Pseudo classes: <c>:document</c>, <c>:floating</c>, <c>:flyout</c> (slid out from an auto hide strip),
/// <c>:has-tabs</c>, <c>:empty</c>.
/// </summary>
public class RiceDockGroupView : TemplatedControl
{
    public static readonly StyledProperty<object?> TitleProperty =
        AvaloniaProperty.Register<RiceDockGroupView, object?>(nameof(Title));

    public static readonly StyledProperty<object?> TitleIconProperty =
        AvaloniaProperty.Register<RiceDockGroupView, object?>(nameof(TitleIcon));

    public static readonly StyledProperty<bool> CanCloseProperty =
        AvaloniaProperty.Register<RiceDockGroupView, bool>(nameof(CanClose));

    public static readonly StyledProperty<bool> CanCollapseProperty =
        AvaloniaProperty.Register<RiceDockGroupView, bool>(nameof(CanCollapse));

    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<RiceDockGroupView, object?>(nameof(EmptyContent));

    internal enum ViewMode
    {
        Docked,
        Floating,
        Flyout,
    }

    private const double DragThreshold = 6;

    private Panel? _paneHost;
    private Panel? _topTabs;
    private Panel? _bottomTabs;
    private Control? _titleBar;
    private Decorator? _actionsHost;
    private Decorator? _emptyHost;
    private Point? _pressPoint;
    private IReadOnlyList<RiceDockPane>? _pressedPanes;
    private RiceDockTab? _pressedTab;
    private bool _reordering;
    private bool _dragging;

    internal RiceDockGroupView(RiceDockLayout layout, DockGroupNode node, ViewMode mode)
    {
        Layout = layout;
        Node = node;
        Mode = mode;
        PseudoClasses.Set(":document", node.IsDocument);
        PseudoClasses.Set(":floating", mode == ViewMode.Floating);
        PseudoClasses.Set(":flyout", mode == ViewMode.Flyout);
    }

    internal RiceDockLayout Layout { get; }

    internal DockGroupNode Node { get; }

    internal ViewMode Mode { get; }

    public object? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? TitleIcon
    {
        get => GetValue(TitleIconProperty);
        set => SetValue(TitleIconProperty, value);
    }

    public bool CanClose
    {
        get => GetValue(CanCloseProperty);
        set => SetValue(CanCloseProperty, value);
    }

    public bool CanCollapse
    {
        get => GetValue(CanCollapseProperty);
        set => SetValue(CanCollapseProperty, value);
    }

    /// <summary>Shown by an empty document group (the layout's <see cref="RiceDockLayout.EmptyContent"/>).</summary>
    public object? EmptyContent
    {
        get => GetValue(EmptyContentProperty);
        set => SetValue(EmptyContentProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceDockGroupView);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _paneHost = e.NameScope.Find<Panel>("PART_PaneHost");
        _topTabs = e.NameScope.Find<Panel>("PART_TopTabs");
        _bottomTabs = e.NameScope.Find<Panel>("PART_BottomTabs");
        _titleBar = e.NameScope.Find<Control>("PART_TitleBar");
        _actionsHost = e.NameScope.Find<Decorator>("PART_TitleActions");
        _emptyHost = e.NameScope.Find<Decorator>("PART_Empty");
        Wire(e, "PART_CloseButton", RiceStrings.DockClose, () => { if (Node.Selected is { } p) Layout.ClosePane(p); });
        Wire(e, "PART_PinButton", Mode == ViewMode.Flyout ? RiceStrings.DockPin : RiceStrings.DockAutoHide, OnPinClick);
        Wire(e, "PART_MenuButton", RiceStrings.DockOptions, null);
        if (e.NameScope.Find<Button>("PART_MenuButton") is { } menu)
            menu.Click += (_, _) => ShowMenu(menu);
        Refresh();
    }

    private static void Wire(TemplateAppliedEventArgs e, string name, string tip, Action? action)
    {
        if (e.NameScope.Find<Button>(name) is not { } button)
            return;
        ToolTip.SetTip(button, tip);
        AutomationProperties.SetName(button, tip);
        if (action is not null)
            button.Click += (_, _) => action();
    }

    private void OnPinClick()
    {
        if (Mode == ViewMode.Flyout)
        {
            foreach (var pane in Node.Panes.ToList())
                pane.IsCollapsed = false;
        }
        else
            Layout.AutoHideGroup(Node);
    }

    internal void SetActive(bool active) => PseudoClasses.Set(":active", active);

    /// <summary>Brings the panes, tabs and title in line with the node.</summary>
    internal void Refresh()
    {
        var selected = Node.Selected;
        if (_paneHost is not null)
        {
            foreach (var stale in _paneHost.Children.OfType<RiceDockPane>().Where(p => !Node.Panes.Contains(p)).ToList())
                _paneHost.Children.Remove(stale);
            foreach (var pane in Node.Panes)
            {
                if (pane.Parent != _paneHost)
                {
                    RiceDockLayout.Detach(pane);
                    _paneHost.Children.Add(pane);
                }
                pane.IsVisible = pane == selected;
            }
        }

        var strip = Node.IsDocument ? _topTabs : _bottomTabs;
        _topTabs?.Children.Clear();
        _bottomTabs?.Children.Clear();
        if (strip is not null && (Node.IsDocument || Node.Panes.Count > 1))
        {
            foreach (var pane in Node.Panes)
                strip.Children.Add(new RiceDockTab(pane, Node.IsDocument, pane == selected));
        }
        PseudoClasses.Set(":has-tabs", !Node.IsDocument && Node.Panes.Count > 1);
        PseudoClasses.Set(":empty", Node.Panes.Count == 0);

        Title = selected?.Header;
        TitleIcon = selected?.Icon;
        // Controls are moved by hand: a stale view of a rebuilt layout may still hold them.
        Place(_actionsHost, selected?.HeaderActions);
        Place(_emptyHost, Node.Panes.Count == 0 ? EmptyContent : null);
        CanClose = selected?.CanClose == true;
        CanCollapse = !Node.IsDocument && Mode != ViewMode.Floating && Node.Panes.Count > 0 && Node.Panes.All(p => p.CanCollapse);
    }

    private static void Place(Decorator? host, object? content)
    {
        if (host is null)
            return;
        var control = content switch
        {
            null => null,
            Control c => c,
            _ => new ContentControl { Content = content },
        };
        if (host.Child == control)
            return;
        if (control is not null)
            RiceDockLayout.Detach(control);
        host.Child = control;
        host.IsVisible = control is not null;
    }

    /// <summary>Lets go of the pane's actions and the empty content (the view is no longer shown).</summary>
    internal void Release()
    {
        Place(_actionsHost, null);
        Place(_emptyHost, null);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EmptyContentProperty)
            Place(_emptyHost, Node.Panes.Count == 0 ? EmptyContent : null);
    }

    private void ShowMenu(Control anchor)
    {
        var selected = Node.Selected;
        var menu = new MenuFlyout();
        void Item(string header, bool enabled, Action action)
        {
            var item = new MenuItem { Header = header, IsEnabled = enabled };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }

        var all = Node.Panes.ToList();
        if (Mode == ViewMode.Docked)
            Item(RiceStrings.DockFloat, all.All(p => p.CanFloat), () => Layout.FloatPanes(all, null));
        else
            Item(RiceStrings.DockDock, true, () => Layout.DockBack(all));
        if (!Node.IsDocument)
            Item(RiceStrings.DockAsDocument, all.All(p => p.CanMove), () => Layout.DockAsDocuments(all));
        if (Mode == ViewMode.Docked && !Node.IsDocument)
            Item(RiceStrings.DockAutoHide, CanCollapse, () => Layout.AutoHideGroup(Node));
        menu.Items.Add(new Separator());
        Item(RiceStrings.DockClose, selected?.CanClose == true, () => { if (selected is not null) Layout.ClosePane(selected); });
        menu.ShowAt(anchor);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Buttons (title bar, tab close, the content's own) keep their clicks.
        if (e.Source is not Visual source || source.FindAncestorOfType<Button>(includeSelf: true) is not null)
            return;
        var point = e.GetCurrentPoint(this);
        var tab = source.FindAncestorOfType<RiceDockTab>(includeSelf: true);
        var onTitle = _titleBar is not null && (source == _titleBar || _titleBar.IsVisualAncestorOf(source));
        if (tab is null && !onTitle)
            return;

        if (tab is not null && point.Properties.IsMiddleButtonPressed)
        {
            if (Node.IsDocument && tab.Pane.CanClose)
                Layout.ClosePane(tab.Pane);
            e.Handled = true;
            return;
        }
        if (!point.Properties.IsLeftButtonPressed)
            return;

        if (tab is not null)
            Layout.ActivatePane(tab.Pane);
        else if (Node.Selected is { } selected)
            Layout.SetActivePane(selected);

        if (e.ClickCount == 2 && onTitle)
        {
            // Double click on the title floats the group, or docks a floating one again.
            var all = Node.Panes.ToList();
            if (Mode == ViewMode.Docked && all.All(p => p.CanFloat))
                Layout.FloatPanes(all, null);
            else if (Mode == ViewMode.Floating)
                Layout.DockBack(all);
            e.Handled = true;
            return;
        }

        _pressPoint = point.Position;
        _pressedTab = tab;
        _pressedPanes = tab is not null ? [tab.Pane] : Node.Panes.ToList();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pressPoint is not { } press || _pressedPanes is not { } panes)
            return;
        var position = e.GetPosition(this);
        var screen = this.PointToScreen(position);
        if (_dragging)
        {
            Layout.UpdateDockDrag(screen);
            e.Handled = true;
            return;
        }
        if (_reordering)
        {
            if (WithinTabStrip(position))
            {
                Reorder(e);
                e.Handled = true;
                return;
            }
            _reordering = false;
        }
        var moved = position - press;
        if (Math.Abs(moved.X) < DragThreshold && Math.Abs(moved.Y) < DragThreshold)
            return;

        if (_pressedTab is not null && Node.Panes.Count > 1 && WithinTabStrip(position))
        {
            _reordering = true;
            e.Pointer.Capture(this);
            Reorder(e);
        }
        else if (panes.All(p => p.CanMove) && Layout.BeginDockDrag(this, panes, this.PointToScreen(press), screen))
        {
            _dragging = true;
            e.Pointer.Capture(this);
        }
        else
            ResetPress();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var dragging = _dragging;
        var reordering = _reordering;
        ResetPress();
        if (dragging || reordering)
            e.Pointer.Capture(null);
        if (dragging)
            Layout.EndDockDrag(this.PointToScreen(e.GetPosition(this)));
        else if (reordering)
            Layout.OnLayoutChanged();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragging)
            Layout.CancelDockDrag();
        ResetPress();
    }

    private void ResetPress()
    {
        _pressPoint = null;
        _pressedPanes = null;
        _pressedTab = null;
        _dragging = false;
        _reordering = false;
    }

    private Panel? TabStrip => Node.IsDocument ? _topTabs : _bottomTabs;

    private bool WithinTabStrip(Point position)
    {
        if (TabStrip is not { } strip || strip.TranslatePoint(default, this) is not { } origin)
            return false;
        return position.Y >= origin.Y - 12 && position.Y <= origin.Y + strip.Bounds.Height + 12;
    }

    // Moves the dragged tab to the place of the tab under the pointer.
    private void Reorder(PointerEventArgs e)
    {
        if (TabStrip is not { } strip || _pressedTab?.Pane is not { } pane)
            return;
        var x = e.GetPosition(strip).X;
        var tabs = strip.Children.OfType<RiceDockTab>().ToList();
        var target = tabs.FirstOrDefault(t => x >= t.Bounds.Left && x < t.Bounds.Right) ?? (x < 0 ? tabs.FirstOrDefault() : tabs.LastOrDefault());
        if (target is null || target.Pane == pane)
            return;
        var to = Node.Panes.IndexOf(target.Pane);
        Node.Panes.Remove(pane);
        Node.Panes.Insert(to, pane);
        Refresh();
        _pressedTab = strip.Children.OfType<RiceDockTab>().FirstOrDefault(t => t.Pane == pane);
    }

    // The pane the focus is in is the active one (its title bar takes the accent color).
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (Node.Selected is { } selected)
            Layout.SetActivePane(selected);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.Control || Node.Panes.Count < 2 || Node.Selected is not { } selected)
            return;
        var index = Node.Panes.IndexOf(selected);
        var next = e.Key switch
        {
            Key.PageDown => (index + 1) % Node.Panes.Count,
            Key.PageUp => (index - 1 + Node.Panes.Count) % Node.Panes.Count,
            _ => -1,
        };
        if (next < 0)
            return;
        Layout.ActivatePane(Node.Panes[next]);
        e.Handled = true;
    }
}
