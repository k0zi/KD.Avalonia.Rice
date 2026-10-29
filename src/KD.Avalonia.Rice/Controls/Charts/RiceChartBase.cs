using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using KD.Avalonia.Rice.Controls.Uml;

namespace KD.Avalonia.Rice.Controls.Charts;

public enum ChartLegendPosition
{
    Bottom,
    Top,
    Right,
    Left,
}

public class ChartSelectionChangedEventArgs(RoutedEvent routedEvent, object? oldItem, object? newItem) : RoutedEventArgs(routedEvent)
{
    public object? OldItem { get; } = oldItem;

    public object? NewItem { get; } = newItem;
}

/// <summary>
/// Base of the Rice charts: title, legend (click an entry to hide or show it), tooltips, selection, the load
/// animation and the theme colors. The charts fill their bounds; in an unbounded direction they take a default size.
/// </summary>
[PseudoClasses(":hasselection")]
public abstract class RiceChartBase : Control
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<RiceChartBase, string?>(nameof(Title));

    public static readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<RiceChartBase, bool>(nameof(ShowLegend), true);

    public static readonly StyledProperty<ChartLegendPosition> LegendPositionProperty =
        AvaloniaProperty.Register<RiceChartBase, ChartLegendPosition>(nameof(LegendPosition));

    public static readonly StyledProperty<bool> ShowTooltipsProperty =
        AvaloniaProperty.Register<RiceChartBase, bool>(nameof(ShowTooltips), true);

    public static readonly StyledProperty<bool> ShowValuesProperty =
        AvaloniaProperty.Register<RiceChartBase, bool>(nameof(ShowValues));

    public static readonly StyledProperty<bool> IsSelectionEnabledProperty =
        AvaloniaProperty.Register<RiceChartBase, bool>(nameof(IsSelectionEnabled), true);

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<RiceChartBase, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsAnimatedProperty =
        AvaloniaProperty.Register<RiceChartBase, bool>(nameof(IsAnimated), true);

    public static readonly StyledProperty<TimeSpan> AnimationDurationProperty =
        AvaloniaProperty.Register<RiceChartBase, TimeSpan>(nameof(AnimationDuration), TimeSpan.FromMilliseconds(700));

    public static readonly StyledProperty<string?> ValueFormatProperty =
        AvaloniaProperty.Register<RiceChartBase, string?>(nameof(ValueFormat));

    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<RiceChartBase>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<RiceChartBase>();

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<RiceChartBase, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<RiceChartBase, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<RiceChartBase, IBrush?>(nameof(MutedForeground));

    public static readonly StyledProperty<IBrush?> AxisBrushProperty =
        AvaloniaProperty.Register<RiceChartBase, IBrush?>(nameof(AxisBrush));

    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<RiceChartBase, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<IBrush?> SeparatorBrushProperty =
        AvaloniaProperty.Register<RiceChartBase, IBrush?>(nameof(SeparatorBrush));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<RiceChartBase, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> PositiveBrushProperty =
        AvaloniaProperty.Register<RiceChartBase, IBrush?>(nameof(PositiveBrush));

    public static readonly StyledProperty<IBrush?> NegativeBrushProperty =
        AvaloniaProperty.Register<RiceChartBase, IBrush?>(nameof(NegativeBrush));

    public static readonly StyledProperty<IBrush?> HeatLowBrushProperty =
        AvaloniaProperty.Register<RiceChartBase, IBrush?>(nameof(HeatLowBrush));

    public static readonly StyledProperty<IBrush?> HeatHighBrushProperty =
        AvaloniaProperty.Register<RiceChartBase, IBrush?>(nameof(HeatHighBrush));

    public static readonly StyledProperty<IBrush?> SelectionBrushProperty =
        AvaloniaProperty.Register<RiceChartBase, IBrush?>(nameof(SelectionBrush));

    public static readonly StyledProperty<IBrush?> TooltipBackgroundProperty =
        AvaloniaProperty.Register<RiceChartBase, IBrush?>(nameof(TooltipBackground));

    public static readonly StyledProperty<IBrush?> TooltipBorderBrushProperty =
        AvaloniaProperty.Register<RiceChartBase, IBrush?>(nameof(TooltipBorderBrush));

    public static readonly StyledProperty<BoxShadows> TooltipShadowProperty =
        AvaloniaProperty.Register<RiceChartBase, BoxShadows>(nameof(TooltipShadow));

    public static readonly RoutedEvent<ChartSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<RiceChartBase, ChartSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    private const double DefaultWidth = 480;
    private const double DefaultHeight = 300;

    // Properties holding the data (a change restarts the animation and shows hidden series again) and properties that
    // only change the layout. Registered by the derived charts.
    private static readonly HashSet<AvaloniaProperty> DataProperties = [];
    private static readonly HashSet<AvaloniaProperty> LayoutProperties =
    [
        TitleProperty, ShowLegendProperty, LegendPositionProperty, ShowValuesProperty, ValueFormatProperty,
    ];

    private readonly Dictionary<AvaloniaProperty, IChartChangeSource> _sources = [];
    private readonly HashSet<int> _hidden = [];
    private readonly IBrush?[] _series = new IBrush?[8];
    private ChartScene? _scene;
    private ChartRenderer? _renderer;
    private AvaloniaTextMeasurer? _measurer;
    private double _progress = 1;
    private TimeSpan? _animationStart;
    private bool _animationPending;
    private bool _frameRequested;
    private int _hover = -1;
    private int _hoverLegend = -1;
    private Point _pointer;

    static RiceChartBase()
    {
        ClipToBoundsProperty.OverrideDefaultValue<RiceChartBase>(true);
        AffectsRender<RiceChartBase>(SelectedItemProperty, ShowTooltipsProperty);
    }

    protected RiceChartBase()
    {
        ResourcesChanged += (_, _) => OnThemeChanged();
        ActualThemeVariantChanged += (_, _) => OnThemeChanged();
    }

    /// <summary>Shown centered above the chart.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Legend of the series (or slices); hidden when there is only one series.</summary>
    public bool ShowLegend
    {
        get => GetValue(ShowLegendProperty);
        set => SetValue(ShowLegendProperty, value);
    }

    public ChartLegendPosition LegendPosition
    {
        get => GetValue(LegendPositionProperty);
        set => SetValue(LegendPositionProperty, value);
    }

    /// <summary>A box with the value of the bar, slice or point under the pointer.</summary>
    public bool ShowTooltips
    {
        get => GetValue(ShowTooltipsProperty);
        set => SetValue(ShowTooltipsProperty, value);
    }

    /// <summary>Value labels on bars and heatmap cells (column, bar, waterfall, histogram, heatmap).</summary>
    public bool ShowValues
    {
        get => GetValue(ShowValuesProperty);
        set => SetValue(ShowValuesProperty, value);
    }

    public bool IsSelectionEnabled
    {
        get => GetValue(IsSelectionEnabledProperty);
        set => SetValue(IsSelectionEnabledProperty, value);
    }

    /// <summary>
    /// The clicked part of the data: a <see cref="ChartDataPoint"/> (category charts), <see cref="ChartSlice"/>,
    /// <see cref="ChartPoint"/>, <see cref="HeatmapCell"/>, <see cref="WaterfallStep"/>, <see cref="HistogramBin"/>,
    /// <see cref="GanttTask"/>, <see cref="BulletItem"/> or <see cref="GaugeBand"/>.
    /// </summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>Bars grow, slices open and lines rise when the data is shown or replaced.</summary>
    public bool IsAnimated
    {
        get => GetValue(IsAnimatedProperty);
        set => SetValue(IsAnimatedProperty, value);
    }

    public TimeSpan AnimationDuration
    {
        get => GetValue(AnimationDurationProperty);
        set => SetValue(AnimationDurationProperty, value);
    }

    /// <summary>.NET number format of the values, e.g. <c>N1</c>, <c>C0</c>, <c>0.0 'kg'</c>; by default k / M / G shortened.</summary>
    public string? ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }
    public IBrush? AxisBrush { get => GetValue(AxisBrushProperty); set => SetValue(AxisBrushProperty, value); }
    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    /// <summary>Lines between slices and around markers; set it to the color behind the chart (e.g. a card).</summary>
    public IBrush? SeparatorBrush { get => GetValue(SeparatorBrushProperty); set => SetValue(SeparatorBrushProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public IBrush? PositiveBrush { get => GetValue(PositiveBrushProperty); set => SetValue(PositiveBrushProperty, value); }
    public IBrush? NegativeBrush { get => GetValue(NegativeBrushProperty); set => SetValue(NegativeBrushProperty, value); }
    public IBrush? HeatLowBrush { get => GetValue(HeatLowBrushProperty); set => SetValue(HeatLowBrushProperty, value); }
    public IBrush? HeatHighBrush { get => GetValue(HeatHighBrushProperty); set => SetValue(HeatHighBrushProperty, value); }
    public IBrush? SelectionBrush { get => GetValue(SelectionBrushProperty); set => SetValue(SelectionBrushProperty, value); }
    public IBrush? TooltipBackground { get => GetValue(TooltipBackgroundProperty); set => SetValue(TooltipBackgroundProperty, value); }
    public IBrush? TooltipBorderBrush { get => GetValue(TooltipBorderBrushProperty); set => SetValue(TooltipBorderBrushProperty, value); }

    public BoxShadows TooltipShadow
    {
        get => GetValue(TooltipShadowProperty);
        set => SetValue(TooltipShadowProperty, value);
    }

    public event EventHandler<ChartSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <summary>Lays the chart out again, e.g. after changing data that does not notify (a plain list).</summary>
    public void Refresh() => InvalidateScene();

    /// <summary>Plays the load animation again.</summary>
    public void Replay()
    {
        if (IsAnimated)
            StartAnimation();
    }

    /// <summary>Series (or slices) hidden by a click on the legend; cleared when the data is replaced.</summary>
    public bool IsSeriesHidden(int index) => _hidden.Contains(index);

    public void SetSeriesHidden(int index, bool hidden)
    {
        if (hidden ? _hidden.Add(index) : _hidden.Remove(index))
            InvalidateScene();
    }

    /// <summary>Marks properties of a derived chart as its data: a change restarts the animation.</summary>
    private protected static void RegisterData(params AvaloniaProperty[] properties)
    {
        foreach (var property in properties)
            DataProperties.Add(property);
    }

    /// <summary>Marks properties of a derived chart that change its layout.</summary>
    private protected static void RegisterLayout(params AvaloniaProperty[] properties)
    {
        foreach (var property in properties)
            LayoutProperties.Add(property);
    }

    /// <summary>The scene of the chart for the given size and context.</summary>
    private protected abstract ChartScene BuildScene(ChartLayoutContext context);

    /// <summary>Lays the chart out without a window, for tests.</summary>
    internal ChartScene Layout(Size size, IUmlTextMeasurer measurer, double progress = 1) =>
        BuildScene(Context(size, measurer, progress));

    private ChartLayoutContext Context(Size size, IUmlTextMeasurer measurer, double progress) =>
        new(size, new ChartStyle(measurer, FontSize > 0 ? FontSize : 13, ValueFormat))
        {
            Progress = progress,
            Hidden = _hidden,
            Title = Title,
            ShowLegend = ShowLegend,
            LegendPosition = LegendPosition,
        };

    private protected void InvalidateScene()
    {
        _scene = null;
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (DataProperties.Contains(property))
        {
            if (_sources.Remove(property, out var old))
                old.Changed -= OnDataChanged;
            if (change.NewValue is IChartChangeSource source)
            {
                source.Changed += OnDataChanged;
                _sources[property] = source;
            }
            _hidden.Clear();
            _hover = -1;
            if (IsAnimated)
                StartAnimation();
            InvalidateScene();
        }
        else if (LayoutProperties.Contains(property) || property == FontSizeProperty || property == FontFamilyProperty)
        {
            if (property == FontFamilyProperty)
                _measurer = null;
            InvalidateScene();
        }
        else if (property == SelectedItemProperty)
        {
            PseudoClasses.Set(":hasselection", SelectedItem is not null);
            RaiseEvent(new ChartSelectionChangedEventArgs(SelectionChangedEvent, change.OldValue, change.NewValue));
        }
        else if (property == BoundsProperty)
            _scene = null;
        else if (property == IsAnimatedProperty && !IsAnimated)
        {
            _animationPending = false;
            _animationStart = null;
            _progress = 1;
            InvalidateScene();
        }
        else if (property.Name.EndsWith("Brush") || property == ForegroundProperty || property == MutedForegroundProperty
                 || property == BackgroundProperty || property == TooltipBackgroundProperty)
        {
            _renderer = null;
            InvalidateVisual();
        }
    }

    // Data changed in place (a value edited, a series added): redrawn right away, without the animation.
    private void OnDataChanged(object? sender, EventArgs e) => InvalidateScene();

    // ---- animation ----

    private void StartAnimation()
    {
        _progress = 0;
        _animationStart = null;
        _animationPending = true;
        RequestFrame();
    }

    private void RequestFrame()
    {
        if (_frameRequested || !_animationPending || TopLevel.GetTopLevel(this) is not { } topLevel)
            return;
        _frameRequested = true;
        topLevel.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan time)
    {
        _frameRequested = false;
        if (!_animationPending)
            return;
        _animationStart ??= time;
        var duration = Math.Max(1, AnimationDuration.TotalMilliseconds);
        var t = Math.Clamp((time - _animationStart.Value).TotalMilliseconds / duration, 0, 1);
        _progress = 1 - Math.Pow(1 - t, 3);
        if (t >= 1)
        {
            _animationPending = false;
            _progress = 1;
        }
        InvalidateScene();
        RequestFrame();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Shown for the first time (or again after navigating back): the animation plays.
        if (IsAnimated)
            StartAnimation();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animationPending = false;
        _progress = 1;
    }

    // ---- layout and rendering ----

    protected override Size MeasureOverride(Size availableSize) => new(
        double.IsInfinity(availableSize.Width) ? DefaultWidth : availableSize.Width,
        double.IsInfinity(availableSize.Height) ? DefaultHeight : availableSize.Height);

    private ChartScene Scene
    {
        get
        {
            if (_scene is not null)
                return _scene;
            _measurer ??= new AvaloniaTextMeasurer(FontFamily);
            try
            {
                _scene = BuildScene(Context(Bounds.Size, _measurer, _progress));
            }
            catch (Exception)
            {
                // Odd data must not break the window; the chart is just not shown.
                _scene = new ChartScene();
            }
            if (_hover >= _scene.Items.Count)
                _hover = -1;
            return _scene;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;
        if (Background is { } background)
            context.FillRectangle(background, new Rect(Bounds.Size));
        var scene = Scene;
        _measurer ??= new AvaloniaTextMeasurer(FontFamily);
        _renderer ??= new ChartRenderer(BrushFor, _measurer);

        var selected = SelectedItem;
        var hoverModel = _hover >= 0 ? scene.Items[_hover].Model : null;
        var focus = hoverModel ?? selected;
        // A selection that is not shown (its series hidden in the legend) dims nothing.
        if (focus is not null && !scene.Items.Any(i => Equals(i.Model, focus)))
            focus = null;
        var dim = hoverModel is not null ? 0.45 : 0.6;
        _renderer.Render(context, scene, i => focus is null || Equals(scene.Items[i].Model, focus) ? 1 : dim);

        if (selected is not null)
            foreach (var item in scene.Items.Where(i => Equals(i.Model, selected)))
                DrawOutline(context, item, SelectionBrush ?? Foreground, 2);
        if (_hover >= 0 && scene.Items[_hover].Shape == ChartHitShape.Circle)
        {
            var item = scene.Items[_hover];
            context.DrawEllipse(null, new Pen(_renderer.Brush(item.Paint), 2), item.Center, Math.Min(item.Radius, 7), Math.Min(item.Radius, 7));
        }
        if (_hover >= 0 && ShowTooltips)
            DrawTooltip(context, scene.Items[_hover]);
    }

    private void DrawOutline(DrawingContext context, ChartHitItem item, IBrush? brush, double thickness)
    {
        if (brush is null)
            return;
        var pen = new Pen(brush, thickness, lineJoin: PenLineJoin.Round);
        switch (item.Shape)
        {
            case ChartHitShape.Circle:
                context.DrawEllipse(null, pen, item.Center, Math.Min(item.Radius, 8) + 1, Math.Min(item.Radius, 8) + 1);
                break;
            case ChartHitShape.Arc:
                context.DrawGeometry(null, pen, ChartRenderer.ArcGeometry(item.Center, item.Radius + 1, Math.Max(0, item.InnerRadius - 1), item.StartAngle, item.SweepAngle));
                break;
            default:
                context.DrawRectangle(null, pen, new RoundedRect(item.Bounds.Inflate(1.5), item.CornerRadius + 1.5));
                break;
        }
    }

    private void DrawTooltip(DrawingContext context, ChartHitItem item)
    {
        var measurer = _measurer!;
        var fontSize = FontSize > 0 ? FontSize : 13;
        var small = Math.Max(8, fontSize - 1);
        var foreground = Foreground ?? Brushes.Black;
        var title = string.IsNullOrEmpty(item.TooltipTitle) ? null : measurer.Format(item.TooltipTitle, small, true, false, foreground);
        var lines = item.Tooltip.Split('\n').Select(l => measurer.Format(l, small, false, false, MutedForeground ?? foreground)).ToList();
        const double padding = 8;
        const double swatch = 9;
        var width = Math.Max(title is null ? 0 : title.WidthIncludingTrailingWhitespace + swatch + 6, lines.Max(l => l.WidthIncludingTrailingWhitespace));
        var height = (title?.Height ?? 0) + lines.Sum(l => l.Height) + (title is null ? 0 : 2);
        var box = new Size(width + padding * 2, height + padding * 2);

        // Beside the pointer, flipped to the other side near the edges.
        var x = _pointer.X + 14;
        var y = _pointer.Y + 14;
        if (x + box.Width > Bounds.Width - 2)
            x = _pointer.X - 14 - box.Width;
        if (y + box.Height > Bounds.Height - 2)
            y = _pointer.Y - 14 - box.Height;
        var rect = new Rect(Math.Max(2, x), Math.Max(2, y), box.Width, box.Height);
        var border = TooltipBorderBrush is { } b ? new Pen(b, 1) : null;
        context.DrawRectangle(TooltipBackground ?? Brushes.White, border, new RoundedRect(rect, 6), TooltipShadow);

        var at = new Point(rect.X + padding, rect.Y + padding);
        if (title is not null)
        {
            context.DrawRectangle(_renderer!.Brush(item.Paint), null, new RoundedRect(new Rect(at.X, at.Y + (title.Height - swatch) / 2, swatch, swatch), 2));
            context.DrawText(title, new Point(at.X + swatch + 6, at.Y));
            at = new Point(at.X, at.Y + title.Height + 2);
        }
        foreach (var line in lines)
        {
            context.DrawText(line, at);
            at = new Point(at.X, at.Y + line.Height);
        }
    }

    private IBrush? BrushFor(ChartBrush role) => role switch
    {
        ChartBrush.Text => Foreground,
        ChartBrush.MutedText => MutedForeground ?? Foreground,
        ChartBrush.Axis => AxisBrush ?? MutedForeground,
        ChartBrush.Grid => GridBrush,
        ChartBrush.Separator => SeparatorBrush,
        ChartBrush.Track => TrackBrush ?? GridBrush,
        ChartBrush.Positive => PositiveBrush ?? SeriesBrush(2),
        ChartBrush.Negative => NegativeBrush ?? SeriesBrush(4),
        ChartBrush.HeatLow => HeatLowBrush ?? TrackBrush,
        ChartBrush.HeatHigh => HeatHighBrush ?? SeriesBrush(0),
        >= ChartBrush.Series0 and <= ChartBrush.Series7 => SeriesBrush(role - ChartBrush.Series0),
        _ => null,
    };

    /// <summary>Chart colors come from the theme resources (RiceChartSeries0Brush …), with fixed ones as fallback.</summary>
    private IBrush? SeriesBrush(int index)
    {
        if (_series[index] is { } cached)
            return cached;
        if (this.TryFindResource($"RiceChartSeries{index}Brush", ActualThemeVariant, out var resource) && resource is IBrush brush)
            return _series[index] = brush;
        string[] fallback = ["#4C78A8", "#4C9AFF", "#36B37E", "#FFAB00", "#FF5630", "#8B5CF6", "#00B8D9", "#E5892E"];
        return _series[index] = new SolidColorBrush(Color.Parse(fallback[index]));
    }

    private void OnThemeChanged()
    {
        Array.Clear(_series);
        _renderer = null;
        InvalidateVisual();
    }

    // ---- input ----

    private int HitTest(Point p)
    {
        var scene = Scene;
        for (var i = scene.Items.Count - 1; i >= 0; i--)
            if (scene.Items[i].Contains(p, 2))
                return i;
        return -1;
    }

    private int HitLegend(Point p) => Scene.Legend.FindIndex(l => l.Bounds.Contains(p));

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointer = e.GetPosition(this);
        var legend = HitLegend(_pointer);
        var hit = legend >= 0 ? -1 : HitTest(_pointer);
        var changed = hit != _hover || legend != _hoverLegend;
        _hover = hit;
        _hoverLegend = legend;
        Cursor = legend >= 0 || hit >= 0 && IsSelectionEnabled ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
        // The tooltip follows the pointer.
        if (changed || hit >= 0 && ShowTooltips)
            InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover < 0 && _hoverLegend < 0)
            return;
        _hover = -1;
        _hoverLegend = -1;
        Cursor = Cursor.Default;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;
        var position = e.GetPosition(this);
        var legend = HitLegend(position);
        if (legend >= 0)
        {
            var index = Scene.Legend[legend].Index;
            SetSeriesHidden(index, !IsSeriesHidden(index));
            _hover = -1;
            e.Handled = true;
            return;
        }
        if (!IsSelectionEnabled)
            return;
        var hit = HitTest(position);
        var model = hit >= 0 ? Scene.Items[hit].Model : null;
        SetCurrentValue(SelectedItemProperty, Equals(model, SelectedItem) ? null : model);
        e.Handled = hit >= 0;
    }
}
