using System.Collections.Specialized;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Docking layout like Visual Studio's: tool windows and documents (<see cref="RiceDockPane"/>s) in tab groups, split
/// around a document area. Drag a pane by its title (the whole group) or tab: dock guides show where it can go – an
/// edge of the layout, a side of a group, the group's tabs, a side of the document area; dropped elsewhere it floats in
/// a window of its own. Panes can be auto hidden to strips at the edges, closed and shown again, and added from code.
/// The declared panes start like the children of a <see cref="DockPanel"/>: the first takes its edge of the whole
/// layout, the next one its edge of the rest; <see cref="DockSide.Document"/> panes are the tabs of the document area,
/// another child is shown there while it has no documents (<see cref="EmptyContent"/>).
/// <see cref="SaveLayout"/> / <see cref="RestoreLayout"/> keep the whole arrangement; with <see cref="LayoutKey"/> that
/// happens by itself, in <c>~/.config/&lt;AppId&gt;/layouts/&lt;key&gt;.json</c>.
/// </summary>
public class RiceDockLayout : TemplatedControl
{
    public static readonly StyledProperty<double> ContentMinSizeProperty =
        AvaloniaProperty.Register<RiceDockLayout, double>(nameof(ContentMinSize), 120);

    public static readonly StyledProperty<string?> LayoutKeyProperty =
        AvaloniaProperty.Register<RiceDockLayout, string?>(nameof(LayoutKey));

    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<RiceDockLayout, object?>(nameof(EmptyContent));

    public static readonly DirectProperty<RiceDockLayout, RiceDockPane?> ActivePaneProperty =
        AvaloniaProperty.RegisterDirect<RiceDockLayout, RiceDockPane?>(nameof(ActivePane), o => o.ActivePane);

    public static readonly RoutedEvent<RoutedEventArgs> LayoutChangedEvent =
        RoutedEvent.Register<RiceDockLayout, RoutedEventArgs>(nameof(LayoutChanged), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RiceDockPaneClosingEventArgs> PaneClosingEvent =
        RoutedEvent.Register<RiceDockLayout, RiceDockPaneClosingEventArgs>(nameof(PaneClosing), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RiceDockPaneEventArgs> PaneClosedEvent =
        RoutedEvent.Register<RiceDockLayout, RiceDockPaneEventArgs>(nameof(PaneClosed), RoutingStrategies.Bubble);

    // Saving waits until the splitter has rested a little.
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(600);

    private readonly List<RiceDockPane> _known = [];
    private readonly List<(RiceDockPane Pane, DockSide Side)> _autoHidden = [];
    private readonly List<FloatingEntry> _floating = [];
    private readonly List<RiceDockPane> _closed = [];
    private readonly Dictionary<RiceDockPane, DockGroupNode> _lastGroup = [];
    private readonly Dictionary<RiceDockPane, DockSide> _lastSide = [];
    private readonly Dictionary<DockGroupNode, RiceDockGroupView> _groupViews = [];
    private readonly Dictionary<DockSplitNode, DockSplitPanel> _splitViews = [];
    private readonly Dictionary<DockSide, Panel> _strips = [];
    private readonly Dictionary<string, IBrush?> _brushes = [];
    private DockModel _model = DockModel.Build([]);
    private Decorator? _treeHost;
    private Panel? _flyoutLayer;
    private DockGuideOverlay? _guides;
    private RiceDockPane? _flyoutPane;
    private Control? _flyout;
    private RiceDockPane? _activePane;
    private bool _built;
    private bool _restoring;
    private bool _suppressCollapse;
    private DispatcherTimer? _saveTimer;
    private RiceDockLayoutState? _pendingState;
    private DragState? _drag;

    private sealed class FloatingEntry(DockGroupNode node, Window window, RiceDockGroupView view)
    {
        public DockGroupNode Node { get; } = node;
        public Window Window { get; } = window;
        public RiceDockGroupView View { get; } = view;
        public bool ClosingByLayout { get; set; }
    }

    private sealed class DragState
    {
        public required IReadOnlyList<RiceDockPane> Panes { get; init; }
        public required RiceDockGroupView Source { get; init; }
        public FloatingEntry? Floating { get; init; }
        public PixelPoint Start { get; init; }
        public PixelPoint WindowStart { get; init; }
        public Size SourceSize { get; init; }
        public DockGuide? Target { get; set; }
        public DockGroupNode? TargetGroup { get; set; }
    }

    public RiceDockLayout()
    {
        Children.CollectionChanged += OnChildrenChanged;
        AddHandler(PointerPressedEvent, OnPreviewPointerPressed, RoutingStrategies.Tunnel);
        ActualThemeVariantChanged += (_, _) => _brushes.Clear();
        ResourcesChanged += (_, _) => _brushes.Clear();
    }

    /// <summary>The declared panes, and optionally one other control shown in the empty document area.</summary>
    [Content]
    public global::Avalonia.Controls.Controls Children { get; } = [];

    /// <summary>Width and height the document area keeps when the tool windows are resized. Default 120.</summary>
    public double ContentMinSize
    {
        get => GetValue(ContentMinSizeProperty);
        set => SetValue(ContentMinSizeProperty, value);
    }

    /// <summary>Saves the layout under this key when it changes and restores it on load; null: not saved.</summary>
    public string? LayoutKey
    {
        get => GetValue(LayoutKeyProperty);
        set => SetValue(LayoutKeyProperty, value);
    }

    /// <summary>Shown in the document area while it has no documents (a declared child that is not a pane, by default).</summary>
    public object? EmptyContent
    {
        get => GetValue(EmptyContentProperty);
        set => SetValue(EmptyContentProperty, value);
    }

    /// <summary>The pane last clicked or focused; its title bar takes the accent color.</summary>
    public RiceDockPane? ActivePane
    {
        get => _activePane;
        private set => SetAndRaise(ActivePaneProperty, ref _activePane, value);
    }

    /// <summary>Raised (debounced) after the arrangement changed: a pane moved, was resized, hidden, closed or opened.</summary>
    public event EventHandler<RoutedEventArgs>? LayoutChanged
    {
        add => AddHandler(LayoutChangedEvent, value);
        remove => RemoveHandler(LayoutChangedEvent, value);
    }

    /// <summary>Raised before a pane closes; set <c>Cancel</c> to keep it (e.g. after an "unsaved changes" question).</summary>
    public event EventHandler<RiceDockPaneClosingEventArgs>? PaneClosing
    {
        add => AddHandler(PaneClosingEvent, value);
        remove => RemoveHandler(PaneClosingEvent, value);
    }

    public event EventHandler<RiceDockPaneEventArgs>? PaneClosed
    {
        add => AddHandler(PaneClosedEvent, value);
        remove => RemoveHandler(PaneClosedEvent, value);
    }

    /// <summary>Every pane of the layout that is open: docked, auto hidden or floating.</summary>
    public IEnumerable<RiceDockPane> Panes => _known.Where(p => p.IsOpen);

    /// <summary>The closed panes; <see cref="ShowPane"/> brings one back.</summary>
    public IEnumerable<RiceDockPane> ClosedPanes => _closed;

    /// <summary>Every pane the layout knows (open and closed).</summary>
    public IEnumerable<RiceDockPane> AllPanes => _known;

    protected override Type StyleKeyOverride => typeof(RiceDockLayout);

    // ---- Building -------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _treeHost = e.NameScope.Find<Decorator>("PART_TreeHost");
        _flyoutLayer = e.NameScope.Find<Panel>("PART_FlyoutLayer");
        _strips.Clear();
        foreach (var side in new[] { DockSide.Left, DockSide.Top, DockSide.Right, DockSide.Bottom })
        {
            if (e.NameScope.Find<Panel>($"PART_{side}Strip") is { } strip)
                _strips[side] = strip;
        }
        if (e.NameScope.Find<Panel>("PART_Root") is { } root)
        {
            _guides = new DockGuideOverlay(this) { IsHitTestVisible = false };
            root.Children.Add(_guides);
        }
        if (!_built)
        {
            _built = true;
            var panes = Children.OfType<RiceDockPane>().ToList();
            // Panes added from code before the template (e.g. in the view's constructor) keep their place after the declared ones.
            var added = _known.Where(p => p.IsOpen && !panes.Contains(p)).ToList();
            foreach (var pane in panes)
                Register(pane);
            EmptyContent ??= Children.FirstOrDefault(c => c is not RiceDockPane);
            _model = DockModel.Build(panes.Where(p => !(p.IsCollapsed && p.Dock != DockSide.Document)));
            foreach (var pane in panes.Where(p => p.IsCollapsed && p.Dock != DockSide.Document))
                _autoHidden.Add((pane, pane.Dock));
            foreach (var pane in panes)
                pane.IsOpen = true;
            foreach (var pane in added)
                Place(pane);
        }
        Rebuild();
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_built)
            return;
        foreach (var removed in e.OldItems?.OfType<RiceDockPane>() ?? [])
            RemovePane(removed);
        foreach (var added in e.NewItems?.OfType<Control>() ?? [])
        {
            if (added is RiceDockPane pane)
                AddPane(pane);
            else
                EmptyContent ??= added;
        }
    }

    private void Register(RiceDockPane pane)
    {
        if (!_known.Contains(pane))
            _known.Add(pane);
        pane.Layout = this;
    }

    /// <summary>Removes a control from the panel or decorator it is in, so it can be placed elsewhere.</summary>
    internal static void Detach(Control control)
    {
        switch (control.Parent)
        {
            case Panel panel:
                panel.Children.Remove(control);
                break;
            case Decorator decorator:
                decorator.Child = null;
                break;
            case ContentControl content when content.Content == control:
                content.Content = null;
                break;
        }
    }

    private void Rebuild()
    {
        if (_treeHost is null)
            return;
        var main = _model.MainDocumentGroup;
        // Views of nodes that are gone let go of the controls they show first (a control has one parent), and only
        // one view may hold the empty content.
        foreach (var (node, view) in _groupViews)
        {
            if (!_model.Contains(node))
                view.Release();
            else if (node != main)
                view.EmptyContent = null;
        }
        var used = new HashSet<DockNode>();
        var root = View(_model.Root, used);
        if (_treeHost.Child != root)
        {
            Detach(root);
            _treeHost.Child = root;
        }
        foreach (var stale in _groupViews.Keys.Where(n => !used.Contains(n)).ToList())
        {
            var view = _groupViews[stale];
            _groupViews.Remove(stale);
            view.EmptyContent = null;
            view.Release();
            Detach(view);
        }
        foreach (var stale in _splitViews.Keys.Where(n => !used.Contains(n)).ToList())
            _splitViews.Remove(stale);
        foreach (var entry in _floating)
            entry.View.Refresh();
        RebuildStrips();
        UpdateActive();
    }

    private Control View(DockNode node, HashSet<DockNode> used)
    {
        used.Add(node);
        switch (node)
        {
            case DockSplitNode split:
                if (!_splitViews.TryGetValue(split, out var panel))
                    _splitViews[split] = panel = new DockSplitPanel(this, split);
                panel.SetItems(split.Children.Select(c => View(c, used)).ToList());
                return panel;
            case DockGroupNode group:
                if (!_groupViews.TryGetValue(group, out var view))
                    _groupViews[group] = view = new RiceDockGroupView(this, group, RiceDockGroupView.ViewMode.Docked);
                if (group == _model.MainDocumentGroup)
                    view.EmptyContent = EmptyContent;
                view.Refresh();
                return view;
            default:
                throw new InvalidOperationException("Unknown dock node.");
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EmptyContentProperty && _built)
            Rebuild();
        else if (change.Property == ContentMinSizeProperty)
        {
            foreach (var panel in _splitViews.Values)
                panel.InvalidateMeasure();
        }
    }

    // ---- Panes ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// Adds a pane, or shows it when the layout knows it already: at <paramref name="side"/> (default: its
    /// <see cref="RiceDockPane.Dock"/>); a <see cref="DockSide.Document"/> pane becomes a tab of the document area.
    /// </summary>
    public void AddPane(RiceDockPane pane, DockSide? side = null)
    {
        Register(pane);
        if (side is { } s && !pane.IsOpen)
        {
            _lastSide[pane] = s;
            _lastGroup.Remove(pane);
        }
        ShowPane(pane);
    }

    /// <summary>Adds (or moves) a pane next to <paramref name="relativeTo"/>, or as another tab of its group.</summary>
    public void AddPane(RiceDockPane pane, RiceDockPane relativeTo, DockPosition position)
    {
        Register(pane);
        if (_model.GroupOf(relativeTo) is not { } group || pane == relativeTo)
        {
            ShowPane(pane);
            return;
        }
        Take(pane, remember: false);
        _closed.Remove(pane);
        _model.DockNextTo([pane], group, position, group.IsDocument, pane.Size);
        pane.IsOpen = true;
        Rebuild();
        ActivatePane(pane);
        OnLayoutChanged();
    }

    /// <summary>Removes a pane from the layout for good (it is not listed among the closed ones).</summary>
    public void RemovePane(RiceDockPane pane)
    {
        if (!_known.Contains(pane))
            return;
        Take(pane, remember: false);
        _known.Remove(pane);
        _closed.Remove(pane);
        _lastGroup.Remove(pane);
        _lastSide.Remove(pane);
        pane.IsOpen = false;
        pane.Layout = null;
        Detach(pane);
        Rebuild();
        OnLayoutChanged();
    }

    /// <summary>Closes a pane (raising <see cref="PaneClosing"/>, which may cancel it). <see cref="ShowPane"/> brings it back.</summary>
    public bool ClosePane(RiceDockPane pane)
    {
        if (!pane.IsOpen || !_known.Contains(pane))
            return false;
        var args = new RiceDockPaneClosingEventArgs(PaneClosingEvent, pane);
        RaiseEvent(args);
        if (args.Cancel)
            return false;
        Take(pane, remember: true);
        _closed.Add(pane);
        pane.IsOpen = false;
        Detach(pane);
        Rebuild();
        RaiseEvent(new RiceDockPaneEventArgs(PaneClosedEvent, pane));
        OnLayoutChanged();
        return true;
    }

    /// <summary>Shows a pane: a closed one at its last place, an auto hidden one slides out, a floating one comes to the front.</summary>
    public void ShowPane(RiceDockPane pane)
    {
        Register(pane);
        if (pane.IsOpen && _known.Contains(pane))
        {
            if (_autoHidden.Any(a => a.Pane == pane))
                ShowFlyout(pane);
            else if (FloatingOf(pane) is { } floating)
            {
                floating.Node.Selected = pane;
                floating.View.Refresh();
                floating.Window.Activate();
            }
            else
                ActivatePane(pane);
            return;
        }
        _closed.Remove(pane);
        Place(pane);
        pane.IsOpen = true;
        Rebuild();
        ActivatePane(pane);
        OnLayoutChanged();
    }

    /// <summary>Selects a docked pane's tab and makes it the active pane.</summary>
    public void ActivatePane(RiceDockPane pane)
    {
        if (_model.GroupOf(pane) is { } group)
        {
            group.Selected = pane;
            if (_groupViews.TryGetValue(group, out var view))
                view.Refresh();
        }
        else if (FloatingOf(pane) is { } floating)
        {
            floating.Node.Selected = pane;
            floating.View.Refresh();
        }
        SetActivePane(pane);
    }

    internal void SetActivePane(RiceDockPane pane)
    {
        ActivePane = pane;
        UpdateActive();
    }

    private void UpdateActive()
    {
        var flyout = _flyout is Border { Child: RiceDockGroupView flyoutView } ? [flyoutView] : Array.Empty<RiceDockGroupView>();
        foreach (var view in _groupViews.Values.Concat(_floating.Select(f => f.View)).Concat(flyout))
            view.SetActive(ActivePane is { } active && view.Node.Panes.Contains(active));
    }

    /// <summary>Moves a pane into a window of its own.</summary>
    public void FloatPane(RiceDockPane pane) => FloatPanes([pane], null);

    internal void FloatPanes(IReadOnlyList<RiceDockPane> panes, PixelPoint? position, Size? size = null)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner || panes.Count == 0)
            return;
        var bounds = size ?? (_model.GroupOf(panes[0]) is { } g && _groupViews.TryGetValue(g, out var view) && view.Bounds.Width > 0
            ? view.Bounds.Size
            : new Size(Math.Max(panes[0].Size, 280), 320));
        var at = position ?? (_model.GroupOf(panes[0]) is { } group && _groupViews.TryGetValue(group, out var v) && v.Bounds.Width > 0
            ? v.PointToScreen(default) + new PixelVector(24, 24)
            : owner.Position + new PixelVector(80, 80));
        foreach (var pane in panes)
            Take(pane, remember: true);
        var node = new DockGroupNode(false);
        DockModel.AddToGroup(node, panes);
        CreateFloating(node, at, bounds, owner);
        Rebuild();
        OnLayoutChanged();
    }

    private FloatingEntry CreateFloating(DockGroupNode node, PixelPoint position, Size size, Window owner)
    {
        var view = new RiceDockGroupView(this, node, RiceDockGroupView.ViewMode.Floating);
        var window = new RiceDockFloatingWindow
        {
            Content = view,
            Width = Math.Max(160, size.Width),
            Height = Math.Max(120, size.Height),
            Title = node.Selected?.Header?.ToString() ?? owner.Title,
            DataContext = DataContext,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = position,
            ShowInTaskbar = false,
        };
        var entry = new FloatingEntry(node, window, view);
        window.Closing += (_, e) => OnFloatingClosing(entry, e);
        _floating.Add(entry);
        foreach (var pane in node.Panes)
            pane.IsFloating = true;
        window.Show(owner);
        return entry;
    }

    private void OnFloatingClosing(FloatingEntry entry, WindowClosingEventArgs e)
    {
        if (entry.ClosingByLayout)
            return;
        // The app is closing: the floating windows stay in the saved layout.
        if (e.CloseReason is WindowCloseReason.OwnerWindowClosing or WindowCloseReason.ApplicationShutdown)
        {
            SaveNow();
            return;
        }
        foreach (var pane in entry.Node.Panes.ToList())
        {
            if (!ClosePane(pane))
                e.Cancel = true;
        }
    }

    private FloatingEntry? FloatingOf(RiceDockPane pane) => _floating.FirstOrDefault(f => f.Node.Panes.Contains(pane));

    /// <summary>Docks floating or auto hidden panes back where they were docked last.</summary>
    internal void DockBack(IReadOnlyList<RiceDockPane> panes)
    {
        if (panes.Count == 0)
            return;
        foreach (var pane in panes)
            Take(pane, remember: false);
        Place(panes[0]);
        if (_model.GroupOf(panes[0]) is { } group)
            DockModel.AddToGroup(group, panes.Skip(1));
        Rebuild();
        ActivatePane(panes[0]);
        OnLayoutChanged();
    }

    internal void DockAsDocuments(IReadOnlyList<RiceDockPane> panes)
    {
        foreach (var pane in panes)
            Take(pane, remember: true);
        DockModel.AddToGroup(_model.MainDocumentGroup, panes);
        Rebuild();
        ActivatePane(panes[0]);
        OnLayoutChanged();
    }

    // Takes a pane out of wherever it is (a group, a strip, a floating window). Remembering keeps its group and side,
    // so closing or auto hiding and showing it again puts it back there.
    private void Take(RiceDockPane pane, bool remember)
    {
        if (_model.GroupOf(pane) is { } group)
        {
            if (remember)
            {
                _lastGroup[pane] = group;
                _lastSide[pane] = SideOf(group);
            }
            if (!group.IsDocument && !group.Size.IsStar)
                pane.Size = Math.Clamp(group.Size.Value, pane.MinSize, Math.Max(pane.MinSize, pane.MaxSize));
            _model.Remove(pane);
        }
        var hidden = _autoHidden.FindIndex(a => a.Pane == pane);
        if (hidden >= 0)
        {
            if (remember)
                _lastSide[pane] = _autoHidden[hidden].Side;
            _autoHidden.RemoveAt(hidden);
            if (_flyoutPane == pane)
                HideFlyout();
            SetCollapsed(pane, false);
        }
        if (FloatingOf(pane) is { } floating)
        {
            floating.Node.Panes.Remove(pane);
            floating.Node.Selected = floating.Node.Panes.FirstOrDefault();
            pane.IsFloating = false;
            if (floating.Node.Panes.Count == 0)
            {
                floating.ClosingByLayout = true;
                floating.View.Release();
                _floating.Remove(floating);
                floating.Window.Close();
            }
            else
                floating.View.Refresh();
        }
    }

    // Puts a pane back: into its last group when that is still docked, next to a pane that shared it, else at its side.
    private void Place(RiceDockPane pane)
    {
        if (_lastGroup.TryGetValue(pane, out var last))
        {
            if (_model.Contains(last))
            {
                DockModel.AddToGroup(last, [pane]);
                return;
            }
            var sibling = _lastGroup.Where(x => x.Value == last && x.Key != pane).Select(x => _model.GroupOf(x.Key)).OfType<DockGroupNode>().FirstOrDefault();
            if (sibling is not null)
            {
                DockModel.AddToGroup(sibling, [pane]);
                _lastGroup[pane] = sibling;
                return;
            }
        }
        var side = _lastSide.TryGetValue(pane, out var s) ? s : pane.Dock;
        _lastGroup[pane] = _model.DockToEdge([pane], side, pane.Size);
    }

    // The edge a group is nearest to (documents: the document area).
    private DockSide SideOf(DockGroupNode group)
    {
        if (group.IsDocument)
            return DockSide.Document;
        if (_treeHost is null || !_groupViews.TryGetValue(group, out var view) || view.TranslatePoint(default, _treeHost) is not { } origin)
            return group.Panes.FirstOrDefault()?.Dock ?? DockSide.Left;
        return NearestSide(new Rect(origin, view.Bounds.Size), _treeHost.Bounds.Size);
    }

    /// <summary>The edge of the layout a group touches; a wide group (spanning the width, so touching the side edges too) counts as top or bottom.</summary>
    internal static DockSide NearestSide(Rect rect, Size layout)
    {
        var wide = rect.Width > rect.Height;
        var distances = new[]
        {
            (DockSide.Left, rect.Left + (wide ? 0.5 : 0)), (DockSide.Top, rect.Top + (wide ? 0 : 0.5)),
            (DockSide.Right, layout.Width - rect.Right + (wide ? 0.5 : 0)), (DockSide.Bottom, layout.Height - rect.Bottom + (wide ? 0 : 0.5)),
        };
        return distances.MinBy(d => d.Item2).Item1;
    }

    // ---- Auto hide --------------------------------------------------------------------------------------------------

    /// <summary>Auto hides every pane of a group to the strip of its edge.</summary>
    internal void AutoHideGroup(DockGroupNode group)
    {
        var side = SideOf(group);
        if (side == DockSide.Document)
            return;
        foreach (var pane in group.Panes.ToList())
        {
            Take(pane, remember: true);
            _autoHidden.Add((pane, side));
            SetCollapsed(pane, true);
        }
        Rebuild();
        OnLayoutChanged();
    }

    internal void OnPaneCollapsedChanged(RiceDockPane pane)
    {
        if (_suppressCollapse || !_built)
            return;
        if (pane.IsCollapsed)
        {
            if (_model.GroupOf(pane) is { IsDocument: false } group)
            {
                var side = SideOf(group);
                Take(pane, remember: true);
                _autoHidden.Add((pane, side));
                SetCollapsed(pane, true);
                Rebuild();
                OnLayoutChanged();
            }
            else if (_autoHidden.All(a => a.Pane != pane))
                SetCollapsed(pane, false);
        }
        else if (_autoHidden.Any(a => a.Pane == pane))
        {
            Take(pane, remember: false);
            Place(pane);
            Rebuild();
            ActivatePane(pane);
            OnLayoutChanged();
        }
    }

    private void SetCollapsed(RiceDockPane pane, bool collapsed)
    {
        _suppressCollapse = true;
        pane.IsCollapsed = collapsed;
        _suppressCollapse = false;
    }

    private void RebuildStrips()
    {
        foreach (var (side, strip) in _strips)
        {
            strip.Children.Clear();
            foreach (var (pane, _) in _autoHidden.Where(a => a.Side == side))
                strip.Children.Add(StripButton(pane, side));
            if (strip.Parent is Control host)
                host.IsVisible = strip.Children.Count > 0;
        }
    }

    private Control StripButton(RiceDockPane pane, DockSide side)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (pane.Icon is not null)
            content.Children.Add(new RiceIconPresenter { Content = pane.Icon, Width = 14, Height = 14 });
        content.Children.Add(new TextBlock { Text = pane.Header?.ToString(), VerticalAlignment = VerticalAlignment.Center });
        var button = new Button
        {
            Content = side is DockSide.Left or DockSide.Right
                ? new LayoutTransformControl { Child = content, LayoutTransform = new RotateTransform(90) }
                : content,
        };
        button.Classes.Add("rice-dock-strip");
        if (_flyoutPane == pane)
            button.Classes.Add("open");
        ToolTip.SetTip(button, pane.Header);
        button.Click += (_, _) =>
        {
            if (_flyoutPane == pane)
                HideFlyout();
            else
                ShowFlyout(pane);
        };
        return button;
    }

    private void ShowFlyout(RiceDockPane pane)
    {
        HideFlyout();
        if (_flyoutLayer is null || _autoHidden.FirstOrDefault(a => a.Pane == pane) is not { Pane: not null } hidden)
            return;
        var node = new DockGroupNode(false) { Selected = pane };
        node.Panes.Add(pane);
        var view = new RiceDockGroupView(this, node, RiceDockGroupView.ViewMode.Flyout);
        var horizontal = hidden.Side is DockSide.Left or DockSide.Right;
        var room = horizontal ? _flyoutLayer.Bounds.Width : _flyoutLayer.Bounds.Height;
        var extent = Math.Clamp(pane.Size, pane.MinSize, Math.Max(pane.MinSize, room * 0.8));
        var border = new Border
        {
            Child = view,
            Width = horizontal ? extent : double.NaN,
            Height = horizontal ? double.NaN : extent,
            HorizontalAlignment = hidden.Side switch
            {
                DockSide.Left => HorizontalAlignment.Left,
                DockSide.Right => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Stretch,
            },
            VerticalAlignment = hidden.Side switch
            {
                DockSide.Top => VerticalAlignment.Top,
                DockSide.Bottom => VerticalAlignment.Bottom,
                _ => VerticalAlignment.Stretch,
            },
        };
        border.Classes.Add("rice-dock-flyout");
        _flyoutLayer.Children.Add(border);
        _flyout = border;
        _flyoutPane = pane;
        RebuildStrips();
        SetActivePane(pane);
    }

    private void HideFlyout()
    {
        if (_flyout is null)
            return;
        if (_flyout is Border { Child: RiceDockGroupView view })
        {
            view.Release();
            foreach (var pane in view.Node.Panes)
                Detach(pane);
        }
        _flyoutLayer?.Children.Remove(_flyout);
        _flyout = null;
        _flyoutPane = null;
        RebuildStrips();
    }

    // A press outside the slid out pane (and its strip) hides it again.
    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_flyout is null || e.Source is not Visual source)
            return;
        if (_flyout.IsVisualAncestorOf(source) || source == _flyout
            || source.FindAncestorOfType<Button>(includeSelf: true)?.Classes.Contains("rice-dock-strip") == true)
            return;
        HideFlyout();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape)
            return;
        if (_drag is not null)
        {
            CancelDockDrag();
            e.Handled = true;
        }
        else if (_flyout is not null)
        {
            HideFlyout();
            e.Handled = true;
        }
    }

    // ---- Dragging ---------------------------------------------------------------------------------------------------

    internal bool BeginDockDrag(RiceDockGroupView source, IReadOnlyList<RiceDockPane> panes, PixelPoint start, PixelPoint screen)
    {
        if (_guides is null || _treeHost is null || source.Mode == RiceDockGroupView.ViewMode.Flyout)
            return false;
        var floating = _floating.FirstOrDefault(f => f.View == source);
        _drag = new DragState
        {
            Panes = panes,
            Source = source,
            Floating = floating,
            Start = start,
            WindowStart = floating?.Window.Position ?? default,
            SourceSize = source.Bounds.Size,
        };
        // A floating window moves with the pointer; see through it to the guides below.
        floating?.Window.Opacity = 0.7;
        HideFlyout();
        UpdateDockDrag(screen);
        return true;
    }

    internal void UpdateDockDrag(PixelPoint screen)
    {
        if (_drag is not { } drag || _guides is null || _treeHost is null)
            return;
        if (drag.Floating is { } floating)
            floating.Window.Position = drag.WindowStart + (screen - drag.Start);

        var local = this.PointToClient(screen);
        drag.Target = null;
        drag.TargetGroup = null;
        if (!new Rect(Bounds.Size).Contains(local) || _treeHost.TranslatePoint(default, this) is not { } hostOrigin)
        {
            _guides.Hide();
            return;
        }
        var layoutRect = new Rect(hostOrigin, _treeHost.Bounds.Size);
        var (group, groupRect) = GroupAt(local, drag.Panes);
        var documentArea = RectOf(_model.DocumentRoot);
        var guides = DockGuides.Place(layoutRect, groupRect, group?.IsDocument == true, documentArea);
        var hit = DockGuides.HitTest(guides, local);
        drag.Target = hit;
        drag.TargetGroup = group;
        _guides.Show(guides, hit, hit is { } guide
            ? DockGuides.Preview(guide, layoutRect, groupRect, documentArea, drag.Panes[0].Size)
            : null);
    }

    private (DockGroupNode? Group, Rect? Rect) GroupAt(Point point, IReadOnlyList<RiceDockPane> dragged)
    {
        foreach (var (node, view) in _groupViews)
        {
            // A group whose every pane is being dragged would be gone after the drop.
            if (node.Panes.Count > 0 && node.Panes.All(dragged.Contains))
                continue;
            if (RectOf(view) is { } rect && rect.Contains(point))
                return (node, rect);
        }
        return (null, null);
    }

    private Rect? RectOf(DockNode node) => node switch
    {
        DockGroupNode g when _groupViews.TryGetValue(g, out var view) => RectOf(view),
        DockSplitNode s when _splitViews.TryGetValue(s, out var panel) => RectOf(panel),
        _ => null,
    };

    private Rect? RectOf(Control control) =>
        control.IsEffectivelyVisible && control.TranslatePoint(default, this) is { } origin ? new Rect(origin, control.Bounds.Size) : null;

    internal void EndDockDrag(PixelPoint screen)
    {
        if (_drag is null)
            return;
        UpdateDockDrag(screen);
        var drag = _drag;
        _drag = null;
        _guides?.Hide();
        drag.Floating?.Window.Opacity = 1;
        if (drag.Target is { } target)
            Drop(drag, target);
        else if (drag.Floating is not null)
            OnLayoutChanged();
        else if (drag.Panes.All(p => p.CanFloat))
            FloatPanes(drag.Panes, screen - new PixelVector(40, 14), drag.SourceSize);
    }

    internal void CancelDockDrag()
    {
        if (_drag is not { } drag)
            return;
        _drag = null;
        _guides?.Hide();
        if (drag.Floating is { } floating)
        {
            floating.Window.Opacity = 1;
            floating.Window.Position = drag.WindowStart;
        }
    }

    private void Drop(DragState drag, DockGuide target)
    {
        var panes = drag.Panes;
        var group = drag.TargetGroup;
        foreach (var pane in panes)
            Take(pane, remember: false);
        // Taken out of a tool group, the pane remembers that group's size.
        var size = panes[0].Size;
        switch (target.Kind)
        {
            case DockGuideKind.Edge:
                _model.DockToEdge(panes, ToSide(target.Position), size);
                break;
            case DockGuideKind.Group when group is not null && _model.Contains(group):
                _model.DockNextTo(panes, group, target.Position, group.IsDocument, size);
                break;
            case DockGuideKind.DocumentArea:
                _model.DockNextTo(panes, _model.DocumentRoot, target.Position, asDocuments: false, size);
                break;
            default:
                _model.DockToEdge(panes, panes[0].Dock == DockSide.Document ? DockSide.Document : DockSide.Left, size);
                break;
        }
        foreach (var pane in panes)
            _lastGroup[pane] = _model.GroupOf(pane)!;
        Rebuild();
        ActivatePane(panes[0]);
        OnLayoutChanged();
    }

    private static DockSide ToSide(DockPosition position) => position switch
    {
        DockPosition.Left => DockSide.Left,
        DockPosition.Top => DockSide.Top,
        DockPosition.Right => DockSide.Right,
        _ => DockSide.Bottom,
    };

    internal void OnPaneChromeChanged(RiceDockPane pane)
    {
        if (_model.GroupOf(pane) is { } group && _groupViews.TryGetValue(group, out var view))
            view.Refresh();
        else if (FloatingOf(pane) is { } floating)
            floating.View.Refresh();
        else if (_autoHidden.Any(a => a.Pane == pane))
            RebuildStrips();
    }

    // ---- Saving -----------------------------------------------------------------------------------------------------

    /// <summary>The whole arrangement as JSON: groups, splits and sizes, auto hidden, floating and closed panes.</summary>
    public string SaveLayout() => JsonSerializer.Serialize(CaptureState(), DockLayoutContext.Default.RiceDockLayoutState);

    /// <summary>Applies a layout from <see cref="SaveLayout"/>; unknown panes are skipped, panes it does not know start at their default place. False when the text cannot be read.</summary>
    public bool RestoreLayout(string json)
    {
        RiceDockLayoutState? state;
        try
        {
            state = JsonSerializer.Deserialize(json, DockLayoutContext.Default.RiceDockLayoutState);
        }
        catch (JsonException)
        {
            return false;
        }
        if (state is null)
            return false;
        ApplyState(state);
        return true;
    }

    /// <summary>Puts every pane back to its declared place and size, open; closes the floating windows.</summary>
    public void ResetLayout()
    {
        CloseFloatingWindows();
        HideFlyout();
        _autoHidden.Clear();
        _closed.Clear();
        _lastGroup.Clear();
        _lastSide.Clear();
        foreach (var pane in _known)
        {
            pane.Size = pane.DefaultSize;
            pane.IsFloating = false;
            pane.IsOpen = true;
            SetCollapsed(pane, false);
        }
        _model = DockModel.Build(_known);
        Rebuild();
        OnLayoutChanged();
    }

    internal RiceDockLayoutState CaptureState() => new()
    {
        Root = DockStateConverter.ToState(_model.Root),
        AutoHidden = _autoHidden.Where(a => a.Pane.LayoutId is not null).Select(a => new RiceDockAutoHideState(a.Pane.LayoutId!, a.Side)).ToList(),
        Floating = _floating.Select(f => new RiceDockFloatingState
        {
            X = f.Window.Position.X,
            Y = f.Window.Position.Y,
            Width = Math.Round(f.Window.Bounds.Width),
            Height = Math.Round(f.Window.Bounds.Height),
            PaneIds = f.Node.Panes.Select(p => p.LayoutId).OfType<string>().ToList(),
            Selected = f.Node.Selected?.LayoutId,
        }).Where(f => f.PaneIds.Count > 0).ToList(),
        Closed = _closed.Select(p => p.LayoutId).OfType<string>().ToList(),
        Panes = _known.Where(p => p.LayoutId is not null)
            .ToDictionary(p => p.LayoutId!, p => new RiceDockPaneState(Math.Round(p.Size, 1), p.IsCollapsed)),
    };

    internal void ApplyState(RiceDockLayoutState state)
    {
        if (TopLevel.GetTopLevel(this) is null && state.Floating.Count > 0)
        {
            // Floating windows need the owner window: the state is applied when the layout is loaded.
            _pendingState = state;
            return;
        }
        _restoring = true;
        try
        {
            RiceDockPane? Find(string id) => _known.FirstOrDefault(p => p.LayoutId == id);
            foreach (var (id, saved) in state.Panes)
            {
                if (Find(id) is { } pane && double.IsFinite(saved.Size) && saved.Size > 0)
                    pane.Size = Math.Clamp(saved.Size, pane.MinSize, Math.Max(pane.MinSize, pane.MaxSize));
            }
            if (state.Root is null)
            {
                // A version 1 layout: sizes and auto hidden panes only.
                foreach (var (id, saved) in state.Panes)
                {
                    if (Find(id) is { } pane && saved.IsCollapsed && pane.CanCollapse)
                        pane.IsCollapsed = true;
                }
                return;
            }

            CloseFloatingWindows();
            HideFlyout();
            _autoHidden.Clear();
            _closed.Clear();
            _lastGroup.Clear();
            var used = new HashSet<RiceDockPane>();
            var root = DockStateConverter.FromState(state.Root, Find, used);
            _model = new DockModel(root ?? DockModel.Build([]).Root);
            foreach (var hidden in state.AutoHidden)
            {
                if (Find(hidden.Id) is { } pane && used.Add(pane) && hidden.Side != DockSide.Document)
                    _autoHidden.Add((pane, hidden.Side));
            }
            foreach (var id in state.Closed)
            {
                if (Find(id) is { } pane && used.Add(pane))
                    _closed.Add(pane);
            }
            var owner = TopLevel.GetTopLevel(this) as Window;
            var floatingGroups = owner is null
                ? []
                : state.Floating.Select(f => (State: f, Panes: f.PaneIds.Select(Find).OfType<RiceDockPane>().Where(used.Add).ToList()))
                    .Where(f => f.Panes.Count > 0)
                    .ToList();
            foreach (var pane in _known)
            {
                pane.IsOpen = !_closed.Contains(pane);
                pane.IsFloating = false;
                SetCollapsed(pane, _autoHidden.Any(a => a.Pane == pane));
                if (_model.GroupOf(pane) is { } group)
                    _lastGroup[pane] = group;
            }
            // Panes the saved layout does not know (e.g. new in this version) start at their declared place.
            foreach (var pane in _known.Where(p => !used.Contains(p)))
                Place(pane);
            foreach (var (floating, panes) in floatingGroups)
            {
                var node = new DockGroupNode(false);
                DockModel.AddToGroup(node, panes);
                node.Selected = panes.FirstOrDefault(p => p.LayoutId == floating.Selected) ?? panes[0];
                CreateFloating(node, new PixelPoint(floating.X, floating.Y), new Size(floating.Width, floating.Height), owner!);
            }
            Rebuild();
        }
        finally
        {
            _restoring = false;
        }
    }

    private void CloseFloatingWindows()
    {
        foreach (var entry in _floating.ToList())
        {
            entry.ClosingByLayout = true;
            entry.View.Release();
            foreach (var pane in entry.Node.Panes)
            {
                pane.IsFloating = false;
                Detach(pane);
            }
            entry.Window.Close();
        }
        _floating.Clear();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        var state = _pendingState ?? Store()?.Load();
        _pendingState = null;
        if (state is not null)
            ApplyState(state);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (_saveTimer?.IsEnabled == true)
            SaveNow();
    }

    internal void OnLayoutChanged()
    {
        if (_restoring || !_built)
            return;
        _saveTimer ??= new DispatcherTimer(SaveDelay, DispatcherPriority.Background, (_, _) => SaveNow());
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveNow()
    {
        _saveTimer?.Stop();
        Store()?.Save(CaptureState());
        RaiseEvent(new RoutedEventArgs(LayoutChangedEvent));
    }

    private DockLayoutStore? Store() =>
        string.IsNullOrWhiteSpace(LayoutKey) ? null : new DockLayoutStore(ThemeManager.Instance.Options.AppInfo.AppId, LayoutKey);

    internal IBrush? Brush(string key)
    {
        if (_brushes.TryGetValue(key, out var brush))
            return brush;
        brush = this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;
        _brushes[key] = brush;
        return brush;
    }
}

/// <summary>The window of floating panes: a <see cref="RiceWindow"/> without its title bar (the group's title moves it).</summary>
internal sealed class RiceDockFloatingWindow : RiceWindow
{
    public RiceDockFloatingWindow()
    {
        ShowTitleBar = false;
    }

    protected override Type StyleKeyOverride => typeof(RiceWindow);
}

public class RiceDockPaneEventArgs(RoutedEvent routedEvent, RiceDockPane pane) : RoutedEventArgs(routedEvent)
{
    public RiceDockPane Pane { get; } = pane;
}

public class RiceDockPaneClosingEventArgs(RoutedEvent routedEvent, RiceDockPane pane) : RiceDockPaneEventArgs(routedEvent, pane)
{
    /// <summary>Keeps the pane open.</summary>
    public bool Cancel { get; set; }
}
