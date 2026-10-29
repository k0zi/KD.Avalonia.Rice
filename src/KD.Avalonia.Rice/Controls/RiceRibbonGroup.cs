using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Named group of commands in a <see cref="RiceRibbonTab"/>: Large items take the full height, Medium and Small ones
/// stack in columns of three (<see cref="RiceRibbonGroupPanel"/>). The header is under the items, with an optional
/// launcher button ("more options", <see cref="ShowLauncher"/>) in the corner. When the tab is too narrow the group
/// collapses into a large button (<see cref="Icon"/> and header) that opens the items in a flyout.
/// </summary>
public class RiceRibbonGroup : HeaderedItemsControl
{
    public static readonly StyledProperty<object?> IconProperty =
        RiceCard.IconProperty.AddOwner<RiceRibbonGroup>();

    public static readonly StyledProperty<bool> ShowLauncherProperty =
        AvaloniaProperty.Register<RiceRibbonGroup, bool>(nameof(ShowLauncher));

    /// <summary>Key tip of the launcher button; when not set, one is chosen from the header.</summary>
    public static readonly StyledProperty<string?> LauncherKeyTipProperty =
        AvaloniaProperty.Register<RiceRibbonGroup, string?>(nameof(LauncherKeyTip));

    public static readonly StyledProperty<ICommand?> LauncherCommandProperty =
        AvaloniaProperty.Register<RiceRibbonGroup, ICommand?>(nameof(LauncherCommand));

    /// <summary>Lets the group collapse into a button when the tab is too narrow. Default true.</summary>
    public static readonly StyledProperty<bool> CanCollapseProperty =
        AvaloniaProperty.Register<RiceRibbonGroup, bool>(nameof(CanCollapse), true);

    public static readonly DirectProperty<RiceRibbonGroup, bool> IsCollapsedProperty =
        AvaloniaProperty.RegisterDirect<RiceRibbonGroup, bool>(nameof(IsCollapsed), g => g.IsCollapsed);

    public static readonly RoutedEvent<RoutedEventArgs> LauncherClickEvent =
        RoutedEvent.Register<RiceRibbonGroup, RoutedEventArgs>(nameof(LauncherClick), RoutingStrategies.Bubble);

    private static readonly FuncTemplate<Panel?> DefaultPanel = new(() => new RiceRibbonGroupPanel());

    private bool _isCollapsed;
    private Button? _launcherButton;
    private RiceRibbonButton? _collapsedButton;
    private Decorator? _expandedHost;
    private Control? _expanded;
    private Flyout? _flyout;

    static RiceRibbonGroup()
    {
        ItemsPanelProperty.OverrideDefaultValue<RiceRibbonGroup>(DefaultPanel);
    }

    /// <summary>An <c>IImage</c>, a <c>MaterialIconKind</c>, a string (e.g. an emoji) or any control; shown when collapsed.</summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Shows the launcher button next to the header; it raises <see cref="LauncherClick"/> and runs <see cref="LauncherCommand"/>.</summary>
    public bool ShowLauncher
    {
        get => GetValue(ShowLauncherProperty);
        set => SetValue(ShowLauncherProperty, value);
    }

    public ICommand? LauncherCommand
    {
        get => GetValue(LauncherCommandProperty);
        set => SetValue(LauncherCommandProperty, value);
    }

    public string? LauncherKeyTip
    {
        get => GetValue(LauncherKeyTipProperty);
        set => SetValue(LauncherKeyTipProperty, value);
    }

    public bool CanCollapse
    {
        get => GetValue(CanCollapseProperty);
        set => SetValue(CanCollapseProperty, value);
    }

    /// <summary>True while the group is shown as a single button; set by the tab's layout.</summary>
    public bool IsCollapsed
    {
        get => _isCollapsed;
        internal set
        {
            if (!SetAndRaise(IsCollapsedProperty, ref _isCollapsed, value))
                return;
            if (!value)
                _flyout?.Hide();
            PseudoClasses.Set(":collapsed", value);
            // The template parts change visibility; the layout re-measures the group in the same pass.
            InvalidateMeasureUpTo(_expandedHost);
            InvalidateMeasureUpTo(_collapsedButton);
        }
    }

    public event EventHandler<RoutedEventArgs>? LauncherClick
    {
        add => AddHandler(LauncherClickEvent, value);
        remove => RemoveHandler(LauncherClickEvent, value);
    }

    // Width of the group when it was last measured expanded; the layout decides on it while the group is collapsed.
    internal double ExpandedWidth { get; set; } = double.NaN;

    internal double CollapsedWidth { get; set; } = double.NaN;

    internal Button? LauncherButton => _launcherButton;

    internal RiceRibbonButton? CollapsedButton => _collapsedButton;

    protected override Type StyleKeyOverride => typeof(RiceRibbonGroup);

    internal void HideFlyout() => _flyout?.Hide();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _flyout?.Hide();
        _launcherButton?.Click -= OnLauncherClick;
        _expanded?.RemoveHandler(Button.ClickEvent, OnItemClick);

        _launcherButton = e.NameScope.Find<Button>("PART_LauncherButton");
        _collapsedButton = e.NameScope.Find<RiceRibbonButton>("PART_CollapsedButton");
        _expandedHost = e.NameScope.Find<Decorator>("PART_ExpandedHost");
        _expanded = _expandedHost?.Child;

        _launcherButton?.Click += OnLauncherClick;
        if (_launcherButton is not null)
            ToolTip.SetTip(_launcherButton, RiceStrings.RibbonGroupLauncher);
        _expanded?.AddHandler(Button.ClickEvent, OnItemClick, RoutingStrategies.Bubble, handledEventsToo: true);
        if (_collapsedButton is not null)
        {
            _flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
            _flyout.Opening += OnFlyoutOpening;
            _flyout.Closed += OnFlyoutClosed;
            _collapsedButton.Flyout = _flyout;
        }
        else
        {
            _flyout = null;
        }
    }

    // The items are moved into the flyout while it is open, and back afterwards.
    private void OnFlyoutOpening(object? sender, EventArgs e)
    {
        if (_expandedHost is null || _flyout is null)
            return;
        _expandedHost.Child = null;
        _flyout.Content = _expanded;
    }

    private void OnFlyoutClosed(object? sender, EventArgs e)
    {
        if (_expandedHost is null || _flyout is null || _flyout.Content != _expanded)
            return;
        _flyout.Content = null;
        _expandedHost.Child = _expanded;
    }

    // A command run from the flyout closes it, like a menu; a button that opens a flyout of its own keeps it open.
    private void OnItemClick(object? sender, RoutedEventArgs e)
    {
        if (_flyout is { IsOpen: true } && e.Source is Button { Flyout: null } button && button != _launcherButton)
            _flyout.Hide();
    }

    private void OnLauncherClick(object? sender, RoutedEventArgs e)
    {
        _flyout?.Hide();
        RaiseEvent(new RoutedEventArgs(LauncherClickEvent));
        if (LauncherCommand?.CanExecute(null) == true)
            LauncherCommand.Execute(null);
    }

    private void InvalidateMeasureUpTo(Visual? visual)
    {
        for (; visual is not null && visual != this; visual = visual.GetVisualParent())
            (visual as Layoutable)?.InvalidateMeasure();
        InvalidateMeasure();
    }
}

/// <summary>
/// Lays out the groups of a <see cref="RiceRibbonTab"/> from the left. When they do not fit, groups collapse into a
/// button from the right until they do (groups with <c>CanCollapse</c> false are skipped).
/// </summary>
public class RiceRibbonGroupsPanel : Panel
{
    // Width of a collapsed group before it was ever measured collapsed.
    private const double CollapsedWidthGuess = 64;

    protected override Size MeasureOverride(Size availableSize)
    {
        var childSize = new Size(double.PositiveInfinity, availableSize.Height);
        var children = Children.Where(c => c.IsVisible).ToList();
        foreach (var child in children)
            MeasureChild(child, childSize);

        var collapse = ChooseCollapsed(
            children.Select(c => c is RiceRibbonGroup g ? Known(g.ExpandedWidth, g.DesiredSize.Width) : c.DesiredSize.Width).ToList(),
            children.Select(c => c is RiceRibbonGroup g ? Known(g.CollapsedWidth, CollapsedWidthGuess) : c.DesiredSize.Width).ToList(),
            children.Select(c => c is RiceRibbonGroup { CanCollapse: true }).ToList(),
            availableSize.Width);
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i] is RiceRibbonGroup group && group.IsCollapsed != collapse[i])
            {
                group.IsCollapsed = collapse[i];
                MeasureChild(group, childSize);
            }
        }

        double width = 0, height = 0;
        foreach (var child in children)
        {
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        foreach (var child in Children.Where(c => c.IsVisible))
        {
            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            x += child.DesiredSize.Width;
        }
        return finalSize;
    }

    /// <summary>
    /// Which items to collapse so that they fit into <paramref name="available"/>: from the right, only those that
    /// <paramref name="canCollapse"/>, and only as many as needed.
    /// </summary>
    internal static bool[] ChooseCollapsed(IReadOnlyList<double> expandedWidths, IReadOnlyList<double> collapsedWidths,
        IReadOnlyList<bool> canCollapse, double available)
    {
        var collapse = new bool[expandedWidths.Count];
        if (double.IsInfinity(available))
            return collapse;
        var total = expandedWidths.Sum();
        for (var i = expandedWidths.Count - 1; i >= 0 && total > available; i--)
        {
            if (!canCollapse[i] || collapsedWidths[i] >= expandedWidths[i])
                continue;
            collapse[i] = true;
            total -= expandedWidths[i] - collapsedWidths[i];
        }
        return collapse;
    }

    private static void MeasureChild(Control child, Size size)
    {
        child.Measure(size);
        if (child is RiceRibbonGroup group)
        {
            if (group.IsCollapsed)
                group.CollapsedWidth = group.DesiredSize.Width;
            else
                group.ExpandedWidth = group.DesiredSize.Width;
        }
    }

    private static double Known(double value, double fallback) => double.IsNaN(value) ? fallback : value;
}

/// <summary>
/// Items panel of a <see cref="RiceRibbonGroup"/>: an item of <see cref="RiceRibbonItemSize.Large"/> size (attached
/// <c>RiceRibbon.Size</c>) takes a column of the full height, Medium and Small items fill columns of up to three rows.
/// </summary>
public class RiceRibbonGroupPanel : Panel
{
    public const int Rows = 3;

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<RiceRibbonGroupPanel, double>(nameof(ColumnSpacing), 2);

    static RiceRibbonGroupPanel()
    {
        AffectsMeasure<RiceRibbonGroupPanel>(ColumnSpacingProperty);
        AffectsParentMeasure<RiceRibbonGroupPanel>(RiceRibbon.SizeProperty);
    }

    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var children = Children.Where(c => c.IsVisible).ToList();
        foreach (var child in children)
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        var columns = BuildColumns(Items(children), availableSize.Height);
        var width = columns.Sum(c => c.Width) + ColumnSpacing * Math.Max(0, columns.Count - 1);
        var height = columns.Count == 0 ? 0 : columns.Max(c => c.Height);
        return new Size(width, double.IsInfinity(availableSize.Height) ? height : Math.Max(height, availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = Children.Where(c => c.IsVisible).ToList();
        var columns = BuildColumns(Items(children), finalSize.Height);
        double x = 0;
        foreach (var column in columns)
        {
            if (column.IsLarge)
            {
                children[column.First].Arrange(new Rect(x, 0, column.Width, finalSize.Height));
            }
            else
            {
                // Rows of a third of the height, or of the item when it is taller, from the top.
                double y = 0;
                for (var i = column.First; i < column.First + column.Count; i++)
                {
                    var rowHeight = Math.Max(finalSize.Height / Rows, children[i].DesiredSize.Height);
                    var itemHeight = children[i].DesiredSize.Height;
                    children[i].Arrange(new Rect(x, y + (rowHeight - itemHeight) / 2, column.Width, itemHeight));
                    y += rowHeight;
                }
            }
            x += column.Width + ColumnSpacing;
        }
        return finalSize;
    }

    /// <summary>
    /// Splits the items into columns: a Large item is a column of its own, the others fill columns of up to
    /// <see cref="Rows"/> items, as long as their heights fit into <paramref name="height"/>.
    /// </summary>
    internal static List<Column> BuildColumns(IReadOnlyList<(bool IsLarge, Size Size)> items, double height)
    {
        var columns = new List<Column>();
        Column? current = null;
        for (var i = 0; i < items.Count; i++)
        {
            var (isLarge, size) = items[i];
            if (isLarge)
            {
                current = null;
                columns.Add(new Column(i, 1, true, size.Width, size.Height));
                continue;
            }
            var rowHeight = Math.Max(double.IsInfinity(height) ? 0 : height / Rows, size.Height);
            if (current is { } c && c.Count < Rows && (double.IsInfinity(height) || c.Height + rowHeight <= height + 0.5))
            {
                current = c with { Count = c.Count + 1, Width = Math.Max(c.Width, size.Width), Height = c.Height + rowHeight };
                columns[^1] = current.Value;
            }
            else
            {
                current = new Column(i, 1, false, size.Width, rowHeight);
                columns.Add(current.Value);
            }
        }
        return columns;
    }

    private static List<(bool, Size)> Items(List<Control> children) =>
        children.Select(c => (RiceRibbon.GetSize(c) == RiceRibbonItemSize.Large, c.DesiredSize)).ToList();

    /// <summary>Items <paramref name="First"/>..<paramref name="First"/>+<paramref name="Count"/>-1 of the panel.</summary>
    internal readonly record struct Column(int First, int Count, bool IsLarge, double Width, double Height);
}
