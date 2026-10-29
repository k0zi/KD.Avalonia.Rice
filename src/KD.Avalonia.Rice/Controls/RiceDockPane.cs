using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// A side panel of a <see cref="RiceDockLayout"/>: docked to an edge (<see cref="Dock"/>), with a header bar (icon,
/// title, actions, collapse button) and a splitter on its inner edge that resizes it (<see cref="Size"/>; double click:
/// back to the starting size; arrow keys when focused). Collapsed it shrinks to a strip with its icon and title;
/// clicking the strip opens it again.
/// Pseudo classes: <c>:left</c>, <c>:top</c>, <c>:right</c>, <c>:bottom</c>, <c>:collapsed</c>, <c>:resizing</c>.
/// </summary>
public class RiceDockPane : HeaderedContentControl
{
    public static readonly StyledProperty<Dock> DockProperty =
        AvaloniaProperty.Register<RiceDockPane, Dock>(nameof(Dock));

    /// <summary>Width (left / right) or height (top / bottom) of the open pane.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<RiceDockPane, double>(nameof(Size), 240,
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinSizeProperty =
        AvaloniaProperty.Register<RiceDockPane, double>(nameof(MinSize), 120);

    public static readonly StyledProperty<double> MaxSizeProperty =
        AvaloniaProperty.Register<RiceDockPane, double>(nameof(MaxSize), double.PositiveInfinity);

    public static readonly StyledProperty<bool> IsCollapsedProperty =
        AvaloniaProperty.Register<RiceDockPane, bool>(nameof(IsCollapsed),
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> CanCollapseProperty =
        AvaloniaProperty.Register<RiceDockPane, bool>(nameof(CanCollapse), true);

    public static readonly StyledProperty<bool> CanResizeProperty =
        AvaloniaProperty.Register<RiceDockPane, bool>(nameof(CanResize), true);

    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<RiceDockPane, object?>(nameof(Icon));

    public static readonly StyledProperty<object?> HeaderActionsProperty =
        AvaloniaProperty.Register<RiceDockPane, object?>(nameof(HeaderActions));

    /// <summary>Key of the pane in a saved layout; defaults to <c>Name</c>.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<RiceDockPane, string?>(nameof(Id));

    // Arrow keys on the focused splitter resize by this much (Ctrl: ten times as much).
    private const double KeyboardStep = 10;

    private static readonly (Dock Dock, string PseudoClass)[] DockClasses =
        [(Dock.Left, ":left"), (Dock.Top, ":top"), (Dock.Right, ":right"), (Dock.Bottom, ":bottom")];

    private Control? _splitter;
    private Button? _collapseButton;
    private Button? _strip;
    private double? _defaultSize;
    private Point? _dragStart;
    private double _dragStartSize;

    public RiceDockPane() => UpdatePseudoClasses();

    public Dock Dock
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

    /// <summary>Shows the collapse button in the header. Default true.</summary>
    public bool CanCollapse
    {
        get => GetValue(CanCollapseProperty);
        set => SetValue(CanCollapseProperty, value);
    }

    /// <summary>Shows the splitter on the inner edge. Default true.</summary>
    public bool CanResize
    {
        get => GetValue(CanResizeProperty);
        set => SetValue(CanResizeProperty, value);
    }

    /// <summary>An <c>IImage</c>, a <c>MaterialIconKind</c>, a string (e.g. an emoji) or any control; also shown on the collapsed strip.</summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Content in the header bar before the collapse button, e.g. small buttons.</summary>
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

    /// <summary>The <see cref="Id"/>, else the <c>Name</c>; panes without either are not saved.</summary>
    public string? LayoutId => Id ?? Name;

    /// <summary>The <see cref="Size"/> the pane started with; a double click on the splitter goes back to it.</summary>
    public double DefaultSize => _defaultSize ?? Size;

    private bool IsHorizontal => Dock is Dock.Left or Dock.Right;

    protected override Type StyleKeyOverride => typeof(RiceDockPane);

    public void ToggleCollapsed() => IsCollapsed = !IsCollapsed;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _defaultSize ??= Size;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_splitter is not null)
        {
            _splitter.PointerPressed -= OnSplitterPressed;
            _splitter.PointerMoved -= OnSplitterMoved;
            _splitter.PointerReleased -= OnSplitterReleased;
            _splitter.PointerCaptureLost -= OnSplitterCaptureLost;
            _splitter.DoubleTapped -= OnSplitterDoubleTapped;
            _splitter.KeyDown -= OnSplitterKeyDown;
        }
        _collapseButton?.Click -= OnCollapseClick;
        _strip?.Click -= OnStripClick;

        _splitter = e.NameScope.Find<Control>("PART_Splitter");
        _collapseButton = e.NameScope.Find<Button>("PART_CollapseButton");
        _strip = e.NameScope.Find<Button>("PART_Strip");

        if (_splitter is not null)
        {
            _splitter.PointerPressed += OnSplitterPressed;
            _splitter.PointerMoved += OnSplitterMoved;
            _splitter.PointerReleased += OnSplitterReleased;
            _splitter.PointerCaptureLost += OnSplitterCaptureLost;
            _splitter.DoubleTapped += OnSplitterDoubleTapped;
            _splitter.KeyDown += OnSplitterKeyDown;
            AutomationProperties.SetName(_splitter, RiceStrings.DockResize);
        }
        if (_collapseButton is not null)
        {
            _collapseButton.Click += OnCollapseClick;
            ToolTip.SetTip(_collapseButton, RiceStrings.DockCollapse);
            AutomationProperties.SetName(_collapseButton, RiceStrings.DockCollapse);
        }
        if (_strip is not null)
        {
            _strip.Click += OnStripClick;
            ToolTip.SetTip(_strip, RiceStrings.DockExpand);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DockProperty || change.Property == IsCollapsedProperty)
            UpdatePseudoClasses();
        if (change.Property == DockProperty || change.Property == SizeProperty || change.Property == MinSizeProperty
            || change.Property == MaxSizeProperty || change.Property == IsCollapsedProperty)
            (Parent as RiceDockLayout)?.InvalidateMeasure();
        if (change.Property == SizeProperty || change.Property == IsCollapsedProperty)
            (Parent as RiceDockLayout)?.OnPaneChanged();
    }

    private void UpdatePseudoClasses()
    {
        foreach (var (dock, pseudoClass) in DockClasses)
            PseudoClasses.Set(pseudoClass, Dock == dock);
        PseudoClasses.Set(":collapsed", IsCollapsed);
    }

    private void OnCollapseClick(object? sender, RoutedEventArgs e) => IsCollapsed = true;

    private void OnStripClick(object? sender, RoutedEventArgs e) => IsCollapsed = false;

    // The pointer position is taken in the layout's coordinates: the splitter itself moves while the pane resizes.
    private Visual Reference => (Visual?)Parent ?? this;

    private void OnSplitterPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!CanResize || IsCollapsed || !e.GetCurrentPoint(_splitter).Properties.IsLeftButtonPressed)
            return;
        _dragStart = e.GetPosition(Reference);
        _dragStartSize = Bounds.Width > 0 && IsHorizontal ? Bounds.Width : Bounds.Height > 0 && !IsHorizontal ? Bounds.Height : Size;
        e.Pointer.Capture(_splitter);
        PseudoClasses.Set(":resizing", true);
        e.Handled = true;
    }

    private void OnSplitterMoved(object? sender, PointerEventArgs e)
    {
        if (_dragStart is not { } start)
            return;
        var moved = e.GetPosition(Reference) - start;
        var delta = Dock switch
        {
            Dock.Left => moved.X,
            Dock.Right => -moved.X,
            Dock.Top => moved.Y,
            _ => -moved.Y,
        };
        Resize(_dragStartSize + delta);
        e.Handled = true;
    }

    private void OnSplitterReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragStart is null)
            return;
        e.Pointer.Capture(null);
        EndResize();
        e.Handled = true;
    }

    private void OnSplitterCaptureLost(object? sender, PointerCaptureLostEventArgs e) => EndResize();

    private void EndResize()
    {
        _dragStart = null;
        PseudoClasses.Set(":resizing", false);
    }

    private void OnSplitterDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (CanResize && !IsCollapsed)
            Resize(DefaultSize);
        e.Handled = true;
    }

    private void OnSplitterKeyDown(object? sender, KeyEventArgs e)
    {
        if (!CanResize || IsCollapsed)
            return;
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Control) ? KeyboardStep * 10 : KeyboardStep;
        // Toward the content grows the pane.
        var direction = (e.Key, Dock) switch
        {
            (Key.Right, Dock.Left) or (Key.Left, Dock.Right) or (Key.Down, Dock.Top) or (Key.Up, Dock.Bottom) => 1,
            (Key.Left, Dock.Left) or (Key.Right, Dock.Right) or (Key.Up, Dock.Top) or (Key.Down, Dock.Bottom) => -1,
            _ => 0,
        };
        if (direction == 0)
            return;
        Resize((IsHorizontal ? Bounds.Width : Bounds.Height) + direction * step);
        e.Handled = true;
    }

    // The layout may give the pane less than asked (the content keeps its minimum), so the size is limited to that too.
    private void Resize(double size)
    {
        var limit = (Parent as RiceDockLayout)?.MaxExtentFor(this) ?? double.PositiveInfinity;
        Size = RiceDockLayout.ClampSize(size, MinSize, Math.Min(MaxSize, limit));
    }
}
