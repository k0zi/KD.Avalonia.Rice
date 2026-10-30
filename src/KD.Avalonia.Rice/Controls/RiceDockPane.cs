using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// A tool window or document of a <see cref="RiceDockLayout"/>. The layout shows it in a tab group with a title bar
/// (icon, <c>Header</c>, <see cref="HeaderActions"/>, menu, auto hide and close buttons); the pane itself holds only
/// its <c>Content</c>. It can be dragged by its title or tab to another place, floated in a window of its own, auto
/// hidden to a strip at the edge (<see cref="IsCollapsed"/>) and closed, then shown again (<see cref="Show"/>).
/// </summary>
public class RiceDockPane : HeaderedContentControl
{
    /// <summary>The edge (or the document area) the pane starts at; see <see cref="RiceDockLayout"/> for the order.</summary>
    public static readonly StyledProperty<DockSide> DockProperty =
        AvaloniaProperty.Register<RiceDockPane, DockSide>(nameof(Dock));

    /// <summary>Width (left / right) or height (top / bottom) when docked at an edge or slid out from a strip.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<RiceDockPane, double>(nameof(Size), 240,
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinSizeProperty =
        AvaloniaProperty.Register<RiceDockPane, double>(nameof(MinSize), 80);

    public static readonly StyledProperty<double> MaxSizeProperty =
        AvaloniaProperty.Register<RiceDockPane, double>(nameof(MaxSize), double.PositiveInfinity);

    /// <summary>Auto hidden: a button on a strip at the edge, sliding out over the content when clicked.</summary>
    public static readonly StyledProperty<bool> IsCollapsedProperty =
        AvaloniaProperty.Register<RiceDockPane, bool>(nameof(IsCollapsed),
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> CanCollapseProperty =
        AvaloniaProperty.Register<RiceDockPane, bool>(nameof(CanCollapse), true);

    public static readonly StyledProperty<bool> CanCloseProperty =
        AvaloniaProperty.Register<RiceDockPane, bool>(nameof(CanClose), true);

    public static readonly StyledProperty<bool> CanFloatProperty =
        AvaloniaProperty.Register<RiceDockPane, bool>(nameof(CanFloat), true);

    public static readonly StyledProperty<bool> CanMoveProperty =
        AvaloniaProperty.Register<RiceDockPane, bool>(nameof(CanMove), true);

    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<RiceDockPane, object?>(nameof(Icon));

    public static readonly StyledProperty<object?> HeaderActionsProperty =
        AvaloniaProperty.Register<RiceDockPane, object?>(nameof(HeaderActions));

    /// <summary>Key of the pane in a saved layout; defaults to <c>Name</c>.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<RiceDockPane, string?>(nameof(Id));

    /// <summary>Panes with the same group name start as tabs of one group.</summary>
    public static readonly StyledProperty<string?> GroupProperty =
        AvaloniaProperty.Register<RiceDockPane, string?>(nameof(Group));

    public static readonly DirectProperty<RiceDockPane, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<RiceDockPane, bool>(nameof(IsOpen), o => o.IsOpen);

    public static readonly DirectProperty<RiceDockPane, bool> IsFloatingProperty =
        AvaloniaProperty.RegisterDirect<RiceDockPane, bool>(nameof(IsFloating), o => o.IsFloating);

    private double? _defaultSize;
    private bool _isOpen;
    private bool _isFloating;

    public DockSide Dock
    {
        get => GetValue(DockProperty);
        set => SetValue(DockProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public double MinSize
    {
        get => GetValue(MinSizeProperty);
        set => SetValue(MinSizeProperty, value);
    }

    public double MaxSize
    {
        get => GetValue(MaxSizeProperty);
        set => SetValue(MaxSizeProperty, value);
    }

    public bool IsCollapsed
    {
        get => GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    /// <summary>Offers auto hide (the pin button). Documents are never auto hidden. Default true.</summary>
    public bool CanCollapse
    {
        get => GetValue(CanCollapseProperty);
        set => SetValue(CanCollapseProperty, value);
    }

    /// <summary>Shows the close button. Default true.</summary>
    public bool CanClose
    {
        get => GetValue(CanCloseProperty);
        set => SetValue(CanCloseProperty, value);
    }

    /// <summary>Can be floated in a window of its own. Default true.</summary>
    public bool CanFloat
    {
        get => GetValue(CanFloatProperty);
        set => SetValue(CanFloatProperty, value);
    }

    /// <summary>Can be dragged to another place. Default true.</summary>
    public bool CanMove
    {
        get => GetValue(CanMoveProperty);
        set => SetValue(CanMoveProperty, value);
    }

    /// <summary>An <c>IImage</c>, a <c>MaterialIconKind</c>, a string (e.g. an emoji) or any control; on the title, tab and strip.</summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Content in the title bar while the pane is selected, e.g. small buttons.</summary>
    public object? HeaderActions
    {
        get => GetValue(HeaderActionsProperty);
        set => SetValue(HeaderActionsProperty, value);
    }

    public string? Id
    {
        get => GetValue(IdProperty);
        set => SetValue(IdProperty, value);
    }

    public string? Group
    {
        get => GetValue(GroupProperty);
        set => SetValue(GroupProperty, value);
    }

    /// <summary>The <see cref="Id"/>, else the <c>Name</c>; panes without either are not saved.</summary>
    public string? LayoutId => Id ?? Name;

    /// <summary>The <see cref="Size"/> the pane started with.</summary>
    public double DefaultSize => _defaultSize ?? Size;

    /// <summary>Shown somewhere: docked, auto hidden or floating (not closed).</summary>
    public bool IsOpen
    {
        get => _isOpen;
        internal set => SetAndRaise(IsOpenProperty, ref _isOpen, value);
    }

    public bool IsFloating
    {
        get => _isFloating;
        internal set => SetAndRaise(IsFloatingProperty, ref _isFloating, value);
    }

    /// <summary>The layout the pane belongs to.</summary>
    public RiceDockLayout? Layout { get; internal set; }

    protected override Type StyleKeyOverride => typeof(RiceDockPane);

    /// <summary>Closes the pane (the layout may cancel it, see <see cref="RiceDockLayout.PaneClosing"/>).</summary>
    public bool Close() => Layout?.ClosePane(this) ?? false;

    /// <summary>Shows a closed pane at its last place, or brings an open one to the front.</summary>
    public void Show() => Layout?.ShowPane(this);

    /// <summary>Moves the pane into a window of its own.</summary>
    public void Float() => Layout?.FloatPane(this);

    public void ToggleCollapsed() => IsCollapsed = !IsCollapsed;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _defaultSize ??= Size;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (Layout is not { } layout)
            return;
        if (change.Property == IsCollapsedProperty)
            layout.OnPaneCollapsedChanged(this);
        else if (change.Property == HeaderProperty || change.Property == IconProperty || change.Property == HeaderActionsProperty
                 || change.Property == CanCloseProperty || change.Property == CanCollapseProperty)
            layout.OnPaneChromeChanged(this);
        else if (change.Property == SizeProperty)
            layout.OnLayoutChanged();
    }
}
