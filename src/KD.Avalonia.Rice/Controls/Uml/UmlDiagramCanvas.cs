using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>
/// Draws a <see cref="UmlDiagram"/> laid out automatically, with panning (drag, scroll wheel), zooming (Ctrl + wheel,
/// +/-), hover highlight and selection. <see cref="UmlDiagramView"/> wraps it with zoom buttons and error display.
/// </summary>
public class UmlDiagramCanvas : Control
{
    public static readonly StyledProperty<UmlDiagram?> DiagramProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, UmlDiagram?>(nameof(Diagram));

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, double>(nameof(Zoom), 1, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinZoomProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, double>(nameof(MinZoom), 0.1);

    public static readonly StyledProperty<double> MaxZoomProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, double>(nameof(MaxZoom), 4);

    public static readonly StyledProperty<object?> SelectedElementProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, object?>(nameof(SelectedElement), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsSelectionEnabledProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, bool>(nameof(IsSelectionEnabled), true);

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, bool>(nameof(ShowGrid), true);

    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<UmlDiagramCanvas>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<UmlDiagramCanvas>();

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, IBrush?>(nameof(MutedForeground));

    public static readonly StyledProperty<IBrush?> NodeBrushProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, IBrush?>(nameof(NodeBrush));

    public static readonly StyledProperty<IBrush?> NodeHeaderBrushProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, IBrush?>(nameof(NodeHeaderBrush));

    public static readonly StyledProperty<IBrush?> NodeBorderBrushProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, IBrush?>(nameof(NodeBorderBrush));

    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, IBrush?>(nameof(LineBrush));

    public static readonly StyledProperty<IBrush?> NoteBrushProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, IBrush?>(nameof(NoteBrush));

    public static readonly StyledProperty<IBrush?> NoteBorderBrushProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, IBrush?>(nameof(NoteBorderBrush));

    public static readonly StyledProperty<IBrush?> ContainerBrushProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, IBrush?>(nameof(ContainerBrush));

    public static readonly StyledProperty<IBrush?> LaneBrushProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, IBrush?>(nameof(LaneBrush));

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> SelectionBrushProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, IBrush?>(nameof(SelectionBrush));

    public static readonly StyledProperty<IBrush?> HoverBrushProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, IBrush?>(nameof(HoverBrush));

    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, IBrush?>(nameof(GridBrush));


    public static readonly StyledProperty<BoxShadows> NodeShadowProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, BoxShadows>(nameof(NodeShadow));

    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<UmlDiagramCanvas, bool>(nameof(IsInteractive), true);

    private const double InteractiveMargin = 32;
    private const double StaticMargin = 12;
    private const double DragThreshold = 4;
    private const double GridStep = 24;

    // Fixed colors of the member visibility icons and class badges, readable on light and dark backgrounds.
    private static readonly IBrush PublicBrush = new SolidColorBrush(Color.Parse("#3FB950"));
    private static readonly IBrush PrivateBrush = new SolidColorBrush(Color.Parse("#F0524F"));
    private static readonly IBrush ProtectedBrush = new SolidColorBrush(Color.Parse("#D9A400"));
    private static readonly IBrush PackageBrush = new SolidColorBrush(Color.Parse("#4C9AFF"));
    private static readonly IBrush BadgeAbstractBrush = new SolidColorBrush(Color.Parse("#2AA198"));
    private static readonly IBrush BadgeInterfaceBrush = new SolidColorBrush(Color.Parse("#8B5CF6"));
    private static readonly IBrush BadgeEnumBrush = new SolidColorBrush(Color.Parse("#E5892E"));
    private static readonly IBrush BadgeOtherBrush = new SolidColorBrush(Color.Parse("#708090"));

    private UmlScene _scene = UmlScene.Empty;
    private UmlRenderer? _renderer;
    private AvaloniaTextMeasurer? _measurer;
    private bool _sceneDirty = true;
    private Vector _offset;
    private bool _userMoved;
    private bool _viewInitialized;
    private Point? _zoomAnchor;
    private int _hover = -1;
    private Point? _pressPosition;
    private Point _lastPointer;
    private bool _panning;
    private IUmlSceneSource? _sceneSource;

    public UmlDiagramCanvas()
    {
        ResourcesChanged += (sender, _) => OnThemeResourcesChanged(sender, EventArgs.Empty);
        ActualThemeVariantChanged += OnThemeResourcesChanged;
    }

    static UmlDiagramCanvas()
    {
        FocusableProperty.OverrideDefaultValue<UmlDiagramCanvas>(true);
        ClipToBoundsProperty.OverrideDefaultValue<UmlDiagramCanvas>(true);
        AffectsRender<UmlDiagramCanvas>(SelectedElementProperty, ShowGridProperty);
    }

    public UmlDiagram? Diagram
    {
        get => GetValue(DiagramProperty);
        set => SetValue(DiagramProperty, value);
    }

    /// <summary>Scale of the drawing, 1 = 100 %; clamped between <see cref="MinZoom"/> and <see cref="MaxZoom"/>.</summary>
    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double MinZoom
    {
        get => GetValue(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    public double MaxZoom
    {
        get => GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    /// <summary>
    /// The selected model object: a <see cref="UmlElement"/>, <see cref="UmlRelation"/>, <see cref="UmlParticipant"/>,
    /// <see cref="UmlMessage"/>, <see cref="UmlActivityStatement"/>, <see cref="UmlTimingLifeline"/>, …
    /// </summary>
    public object? SelectedElement
    {
        get => GetValue(SelectedElementProperty);
        set => SetValue(SelectedElementProperty, value);
    }

    public bool IsSelectionEnabled
    {
        get => GetValue(IsSelectionEnabledProperty);
        set => SetValue(IsSelectionEnabledProperty, value);
    }

    /// <summary>Dot grid in the background.</summary>
    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
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
    public IBrush? NodeBrush { get => GetValue(NodeBrushProperty); set => SetValue(NodeBrushProperty, value); }
    public IBrush? NodeHeaderBrush { get => GetValue(NodeHeaderBrushProperty); set => SetValue(NodeHeaderBrushProperty, value); }
    public IBrush? NodeBorderBrush { get => GetValue(NodeBorderBrushProperty); set => SetValue(NodeBorderBrushProperty, value); }
    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    public IBrush? NoteBrush { get => GetValue(NoteBrushProperty); set => SetValue(NoteBrushProperty, value); }
    public IBrush? NoteBorderBrush { get => GetValue(NoteBorderBrushProperty); set => SetValue(NoteBorderBrushProperty, value); }
    public IBrush? ContainerBrush { get => GetValue(ContainerBrushProperty); set => SetValue(ContainerBrushProperty, value); }
    public IBrush? LaneBrush { get => GetValue(LaneBrushProperty); set => SetValue(LaneBrushProperty, value); }
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public IBrush? SelectionBrush { get => GetValue(SelectionBrushProperty); set => SetValue(SelectionBrushProperty, value); }
    public IBrush? HoverBrush { get => GetValue(HoverBrushProperty); set => SetValue(HoverBrushProperty, value); }
    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }

    public BoxShadows NodeShadow
    {
        get => GetValue(NodeShadowProperty);
        set => SetValue(NodeShadowProperty, value);
    }

    /// <summary>
    /// False makes a picture of the diagram, e.g. in a document: the control takes the size of the diagram (scaled
    /// down to the available space), and there is no panning, zooming, hover or selection; the scroll wheel scrolls
    /// the parent.
    /// </summary>
    public bool IsInteractive
    {
        get => GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    /// <summary>Scene of a diagram that is not a <see cref="UmlDiagram"/> (Mermaid); replaces <see cref="Diagram"/>.</summary>
    internal IUmlSceneSource? SceneSource
    {
        get => _sceneSource;
        set
        {
            _sceneSource = value;
            _sceneDirty = true;
            EnsureScene();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private double ViewMargin => IsInteractive ? InteractiveMargin : StaticMargin;

    /// <summary>Size of the laid out diagram at 100 %.</summary>
    public Size ContentSize
    {
        get
        {
            EnsureScene();
            return _scene.Bounds.Size;
        }
    }

    public void ZoomIn() => ZoomAround(ViewportCenter, Zoom * 1.25);

    public void ZoomOut() => ZoomAround(ViewportCenter, Zoom / 1.25);

    /// <summary>100 %, keeping the point in the middle of the view.</summary>
    public void ResetZoom() => ZoomAround(ViewportCenter, 1);

    /// <summary>Scales the diagram to fill the view (at most 100 %) and centers it.</summary>
    public void ZoomToFit()
    {
        EnsureScene();
        var size = Bounds.Size;
        var content = _scene.Bounds;
        if (size.Width <= 0 || size.Height <= 0 || content.Width <= 0 || content.Height <= 0)
            return;
        var zoom = Math.Min((size.Width - 2 * ViewMargin) / content.Width, (size.Height - 2 * ViewMargin) / content.Height);
        zoom = Math.Clamp(Math.Min(zoom, 1), MinZoom, Math.Max(MinZoom, MaxZoom));
        SetZoomSilently(zoom);
        CenterContent();
        _userMoved = false;
    }

    /// <summary>Lays the diagram out again; call it after changing the model in place.</summary>
    public void Refresh()
    {
        _sceneDirty = true;
        InvalidateVisual();
    }

    private Point ViewportCenter => new(Bounds.Width / 2, Bounds.Height / 2);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsInteractive)
            return base.MeasureOverride(availableSize);
        // A picture: the size of the diagram, scaled down when there is not enough room.
        EnsureScene();
        var content = _scene.Bounds.Size;
        if (content.Width <= 0 || content.Height <= 0)
            return default;
        var size = new Size(content.Width + 2 * ViewMargin, content.Height + 2 * ViewMargin);
        var scale = Math.Min(1, Math.Min(availableSize.Width / size.Width, availableSize.Height / size.Height));
        return new Size(size.Width * scale, size.Height * scale);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == DiagramProperty || property == FontSizeProperty || property == FontFamilyProperty)
        {
            // Rebuilt right away, never in Render: it may change the zoom, which must not happen during rendering.
            _sceneDirty = true;
            if (property != DiagramProperty)
                _measurer = null;
            EnsureScene();
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (property == IsInteractiveProperty)
        {
            Focusable = IsInteractive;
            _userMoved = false;
            _hover = -1;
            InvalidateMeasure();
            InitializeView();
        }
        else if (property == ZoomProperty)
        {
            var clamped = Math.Clamp(Zoom, MinZoom, Math.Max(MinZoom, MaxZoom));
            if (Math.Abs(clamped - Zoom) > 1e-9)
            {
                SetCurrentValue(ZoomProperty, clamped);
                return;
            }
            // Keep the anchor (pointer or the middle of the view) at the same place on screen.
            var anchor = _zoomAnchor ?? ViewportCenter;
            var old = change.GetOldValue<double>();
            if (old > 0)
                _offset = (Vector)anchor - ((Vector)anchor - _offset) * (Zoom / old);
            _zoomAnchor = null;
            InvalidateVisual();
        }
        else if (property == BoundsProperty)
        {
            var old = change.GetOldValue<Rect>();
            if (!_userMoved || !IsInteractive)
                InitializeView();
            else
                _offset += new Vector((Bounds.Width - old.Width) / 2, (Bounds.Height - old.Height) / 2);
        }
        else if (property.Name.EndsWith("Brush") || property == ForegroundProperty || property == MutedForegroundProperty
                 || property == NodeShadowProperty || property == BackgroundProperty)
        {
            _renderer = null;
            InvalidateVisual();
        }
    }

    // ---- scene ----

    private void EnsureScene()
    {
        if (!_sceneDirty)
            return;
        _sceneDirty = false;
        _measurer ??= new AvaloniaTextMeasurer(FontFamily);
        var previous = _scene;
        try
        {
            var style = new UmlStyle(_measurer, FontSize > 0 ? FontSize : 13);
            _scene = _sceneSource?.Build(style) ?? UmlSceneBuilder.Build(Diagram, style);
        }
        catch (Exception)
        {
            // A layout failure on odd input must not break the window; the diagram is just not shown.
            _scene = UmlScene.Empty;
        }
        _renderer = null;
        _hover = -1;
        if (!_userMoved || !_viewInitialized)
        {
            _viewInitialized = false;
            InitializeView();
        }
        else if (previous.Bounds.Width <= 0)
            CenterContent();
        KeepSelection();
    }

    /// <summary>First view of a diagram: 100 % when it fits, otherwise zoomed to fit.</summary>
    private void InitializeView()
    {
        if (Bounds.Width <= 0 || _sceneDirty || _scene.Bounds.Width <= 0)
            return;
        var content = _scene.Bounds;
        if (content.Width + 2 * ViewMargin <= Bounds.Width && content.Height + 2 * ViewMargin <= Bounds.Height)
        {
            SetZoomSilently(Math.Clamp(1, MinZoom, MaxZoom));
            CenterContent();
        }
        else
            ZoomToFit();
        _viewInitialized = true;
    }

    private void CenterContent()
    {
        var content = _scene.Bounds;
        var zoom = Zoom;
        var x = (Bounds.Width - content.Width * zoom) / 2 - content.X * zoom;
        var y = (Bounds.Height - content.Height * zoom) / 2 - content.Y * zoom;
        // Taller than the view: start at the top instead of the middle.
        if (content.Height * zoom + 2 * ViewMargin > Bounds.Height)
            y = ViewMargin - content.Y * zoom;
        _offset = new Vector(x, y);
        InvalidateVisual();
    }

    private void SetZoomSilently(double zoom)
    {
        _zoomAnchor = null;
        var offset = _offset;
        SetCurrentValue(ZoomProperty, zoom);
        _offset = offset;
    }

    /// <summary>After a new diagram (e.g. the source was edited) the selection moves to the element with the same id.</summary>
    private void KeepSelection()
    {
        var selected = SelectedElement;
        if (selected is null || _scene.Items.Any(i => ReferenceEquals(i.Model, selected)))
            return;
        var match = _scene.Items.Select(i => i.Model).FirstOrDefault(m => (m, selected) switch
        {
            (UmlElement a, UmlElement b) => a.Id == b.Id,
            (UmlParticipant a, UmlParticipant b) => a.Id == b.Id,
            (UmlTimingLifeline a, UmlTimingLifeline b) => a.Id == b.Id,
            (UmlRelation a, UmlRelation b) => a.From == b.From && a.To == b.To,
            _ => false,
        });
        SetCurrentValue(SelectedElementProperty, match);
    }

    private IBrush? BrushFor(UmlBrush role) => role switch
    {
        UmlBrush.Node => NodeBrush,
        UmlBrush.Header => NodeHeaderBrush ?? NodeBrush,
        UmlBrush.Border => NodeBorderBrush,
        UmlBrush.Line => LineBrush ?? Foreground,
        UmlBrush.Text => Foreground,
        UmlBrush.MutedText => MutedForeground ?? Foreground,
        UmlBrush.Note => NoteBrush ?? NodeBrush,
        UmlBrush.NoteBorder => NoteBorderBrush ?? NodeBorderBrush,
        UmlBrush.Container => ContainerBrush,
        UmlBrush.Accent => AccentBrush ?? LineBrush,
        UmlBrush.Solid => Foreground,
        UmlBrush.Background => Background ?? Brushes.White,
        UmlBrush.Lane => LaneBrush,
        UmlBrush.Public => PublicBrush,
        UmlBrush.Private => PrivateBrush,
        UmlBrush.Protected => ProtectedBrush,
        UmlBrush.PackageVisibility => PackageBrush,
        UmlBrush.BadgeClass => AccentBrush,
        UmlBrush.BadgeAbstract => BadgeAbstractBrush,
        UmlBrush.BadgeInterface => BadgeInterfaceBrush,
        UmlBrush.BadgeEnum => BadgeEnumBrush,
        UmlBrush.BadgeOther => BadgeOtherBrush,
        UmlBrush.OnSeries => Brushes.White,
        >= UmlBrush.Series0 and <= UmlBrush.Series7 => SeriesBrush(role - UmlBrush.Series0),
        _ => null,
    };

    private readonly IBrush?[] _series = new IBrush?[8];

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

    private void OnThemeResourcesChanged(object? sender, EventArgs e)
    {
        Array.Clear(_series);
        _renderer = null;
        InvalidateVisual();
    }

    // ---- rendering ----

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (Background is { } background)
            context.FillRectangle(background, bounds);

        var zoom = Zoom;
        if (ShowGrid && GridBrush is { } grid && zoom * GridStep >= 8)
            DrawGrid(context, grid, bounds, zoom);

        _measurer ??= new AvaloniaTextMeasurer(FontFamily);
        _renderer ??= new UmlRenderer(_scene, BrushFor, _measurer, NodeShadow);
        using (context.PushTransform(Matrix.CreateScale(zoom, zoom) * Matrix.CreateTranslation(_offset)))
        {
            _renderer.Render(context);
            DrawHighlights(context);
        }
    }

    private void DrawGrid(DrawingContext context, IBrush brush, Rect bounds, double zoom)
    {
        var step = GridStep * zoom;
        var startX = _offset.X % step;
        var startY = _offset.Y % step;
        var size = Math.Max(1, 1.5 * Math.Min(1, zoom));
        for (var x = startX; x < bounds.Width; x += step)
            for (var y = startY; y < bounds.Height; y += step)
                context.FillRectangle(brush, new Rect(x - size / 2, y - size / 2, size, size));
    }

    private void DrawHighlights(DrawingContext context)
    {
        var selected = SelectedElement;
        var hoverModel = _hover >= 0 && _hover < _scene.Items.Count ? _scene.Items[_hover].Model : null;

        // Lines connected to the hovered or selected element are highlighted with it.
        foreach (var item in _scene.Items.Where(i => i.IsEdge))
        {
            var related = selected is not null && (ReferenceEquals(item.From, selected) || ReferenceEquals(item.To, selected))
                          || hoverModel is not null && !ReferenceEquals(hoverModel, item.Model)
                                                    && (ReferenceEquals(item.From, hoverModel) || ReferenceEquals(item.To, hoverModel));
            if (related)
                DrawItemOutline(context, item, AccentBrush, 0.35, 4);
        }

        if (hoverModel is not null && !ReferenceEquals(hoverModel, selected))
            foreach (var item in _scene.Items.Where(i => ReferenceEquals(i.Model, hoverModel)))
                DrawItemOutline(context, item, HoverBrush ?? AccentBrush, 1, 1.5);

        if (selected is not null)
            foreach (var item in _scene.Items.Where(i => ReferenceEquals(i.Model, selected)))
            {
                DrawItemOutline(context, item, SelectionBrush ?? AccentBrush, 0.25, 7);
                DrawItemOutline(context, item, SelectionBrush ?? AccentBrush, 1, 2);
            }
    }

    private static void DrawItemOutline(DrawingContext context, UmlHitItem item, IBrush? brush, double opacity, double thickness)
    {
        if (brush is null)
            return;
        using var _ = context.PushOpacity(opacity);
        var pen = new Pen(brush, thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        if (item.Path is { Count: >= 2 } path)
            context.DrawGeometry(null, pen, UmlRenderer.LineGeometry(path, item.Curve == UmlCurve.Polyline && path.Count > 2 ? UmlCurve.Smooth : item.Curve));
        else
            context.DrawRectangle(null, pen, new RoundedRect(item.Bounds.Inflate(3), item.CornerRadius + 3));
    }

    // ---- hit testing ----

    private Point ToScene(Point p) => new((p.X - _offset.X) / Zoom, (p.Y - _offset.Y) / Zoom);

    private int HitTest(Point screen)
    {
        var p = ToScene(screen);
        var tolerance = 5 / Zoom;
        for (var i = _scene.Items.Count - 1; i >= 0; i--)
        {
            var item = _scene.Items[i];
            if (!item.Bounds.Inflate(tolerance).Contains(p))
                continue;
            if (item.Path is { } path)
            {
                var flat = UmlGeometry.Flatten(path, item.Curve);
                if (UmlGeometry.DistanceToPolyline(p, flat) <= tolerance)
                    return i;
                // The labels of an edge are inside its bounds but away from the line: they count only off the line.
                continue;
            }
            if (item.IsContainer)
            {
                var r = item.Bounds;
                var onHeader = p.Y <= r.Y + item.HeaderHeight;
                var onBorder = p.X - r.X <= tolerance * 2 || r.Right - p.X <= tolerance * 2 || r.Bottom - p.Y <= tolerance * 2;
                if (onHeader || onBorder)
                    return i;
                continue;
            }
            return i;
        }
        return -1;
    }

    // ---- input ----

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsInteractive)
            return;
        Focus();
        var point = e.GetCurrentPoint(this);
        _lastPointer = point.Position;
        if (point.Properties.IsLeftButtonPressed || point.Properties.IsMiddleButtonPressed)
        {
            _pressPosition = point.Position;
            _panning = point.Properties.IsMiddleButtonPressed;
            e.Pointer.Capture(this);
            e.Handled = true;
        }

        if (e.ClickCount == 2 && point.Properties.IsLeftButtonPressed && HitTest(point.Position) < 0)
            ZoomToFit();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!IsInteractive)
            return;
        var position = e.GetPosition(this);
        if (_pressPosition is { } press)
        {
            if (!_panning && GeometryDistance(position, press) > DragThreshold)
                _panning = true;
            if (_panning)
            {
                _offset += position - _lastPointer;
                _userMoved = true;
                Cursor = new Cursor(StandardCursorType.SizeAll);
                InvalidateVisual();
            }
            _lastPointer = position;
            return;
        }

        var hit = HitTest(position);
        if (hit != _hover)
        {
            _hover = hit;
            Cursor = hit >= 0 && IsSelectionEnabled ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressPosition is not null && !_panning && e.InitialPressMouseButton == MouseButton.Left && IsSelectionEnabled)
        {
            var hit = HitTest(e.GetPosition(this));
            SetCurrentValue(SelectedElementProperty, hit >= 0 ? _scene.Items[hit].Model : null);
        }
        _pressPosition = null;
        _panning = false;
        Cursor = _hover >= 0 && IsSelectionEnabled ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressPosition = null;
        _panning = false;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover >= 0 && _pressPosition is null)
        {
            _hover = -1;
            InvalidateVisual();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (!IsInteractive)
            return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
            ZoomAround(e.GetPosition(this), Zoom * Math.Pow(1.15, e.Delta.Y));
        else
        {
            var delta = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? new Vector(e.Delta.Y, e.Delta.X) : e.Delta;
            _offset += delta * 48;
            _userMoved = true;
            InvalidateVisual();
        }
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsInteractive)
            return;
        const double step = 48;
        switch (e.Key)
        {
            case Key.Add or Key.OemPlus:
                ZoomIn();
                break;
            case Key.Subtract or Key.OemMinus:
                ZoomOut();
                break;
            case Key.D0 or Key.NumPad0:
                ResetZoom();
                break;
            case Key.F:
                ZoomToFit();
                break;
            case Key.Escape when SelectedElement is not null:
                SetCurrentValue(SelectedElementProperty, null);
                break;
            case Key.Left:
                Pan(new Vector(step, 0));
                break;
            case Key.Right:
                Pan(new Vector(-step, 0));
                break;
            case Key.Up:
                Pan(new Vector(0, step));
                break;
            case Key.Down:
                Pan(new Vector(0, -step));
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void Pan(Vector delta)
    {
        _offset += delta;
        _userMoved = true;
        InvalidateVisual();
    }

    private void ZoomAround(Point anchor, double zoom)
    {
        zoom = Math.Clamp(zoom, MinZoom, Math.Max(MinZoom, MaxZoom));
        if (Math.Abs(zoom - Zoom) < 1e-9)
            return;
        _userMoved = true;
        _zoomAnchor = anchor;
        SetCurrentValue(ZoomProperty, zoom);
    }

    private static double GeometryDistance(Point a, Point b) => UmlGeometry.Length(a - b);
}
