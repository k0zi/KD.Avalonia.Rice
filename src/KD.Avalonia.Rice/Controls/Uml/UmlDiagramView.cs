using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace KD.Avalonia.Rice.Controls.Uml;

public class UmlSelectionChangedEventArgs(RoutedEvent routedEvent, object? oldElement, object? newElement)
    : RoutedEventArgs(routedEvent)
{
    public object? OldElement { get; } = oldElement;

    public object? NewElement { get; } = newElement;
}

/// <summary>
/// Shows a UML diagram, given as PlantUML text (<see cref="Source"/>) or as a model (<see cref="Diagram"/>), laid out
/// automatically and drawn in the colors of the current Rice theme. Pan by dragging or scrolling, zoom with
/// Ctrl + wheel or the buttons, click an element to select it.
/// </summary>
[TemplatePart(PartCanvas, typeof(UmlDiagramCanvas))]
[TemplatePart(PartZoomInButton, typeof(Button))]
[TemplatePart(PartZoomOutButton, typeof(Button))]
[TemplatePart(PartZoomToFitButton, typeof(Button))]
[TemplatePart(PartResetZoomButton, typeof(Button))]
[PseudoClasses(":empty", ":error", ":hasselection")]
public class UmlDiagramView : TemplatedControl
{
    public const string PartCanvas = "PART_Canvas";
    public const string PartZoomInButton = "PART_ZoomInButton";
    public const string PartZoomOutButton = "PART_ZoomOutButton";
    public const string PartZoomToFitButton = "PART_ZoomToFitButton";
    public const string PartResetZoomButton = "PART_ResetZoomButton";

    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<UmlDiagramView, string?>(nameof(Source));

    public static readonly StyledProperty<int> DiagramIndexProperty =
        AvaloniaProperty.Register<UmlDiagramView, int>(nameof(DiagramIndex));

    public static readonly StyledProperty<UmlDiagram?> DiagramProperty =
        AvaloniaProperty.Register<UmlDiagramView, UmlDiagram?>(nameof(Diagram));

    public static readonly DirectProperty<UmlDiagramView, UmlDocument?> DocumentProperty =
        AvaloniaProperty.RegisterDirect<UmlDiagramView, UmlDocument?>(nameof(Document), o => o.Document);

    public static readonly DirectProperty<UmlDiagramView, IReadOnlyList<UmlDiagnostic>> DiagnosticsProperty =
        AvaloniaProperty.RegisterDirect<UmlDiagramView, IReadOnlyList<UmlDiagnostic>>(nameof(Diagnostics), o => o.Diagnostics);

    public static readonly DirectProperty<UmlDiagramView, string?> ErrorTextProperty =
        AvaloniaProperty.RegisterDirect<UmlDiagramView, string?>(nameof(ErrorText), o => o.ErrorText);

    public static readonly StyledProperty<double> ZoomProperty =
        UmlDiagramCanvas.ZoomProperty.AddOwner<UmlDiagramView>(new StyledPropertyMetadata<double>(1, BindingMode.TwoWay));

    public static readonly StyledProperty<double> MinZoomProperty = UmlDiagramCanvas.MinZoomProperty.AddOwner<UmlDiagramView>();

    public static readonly StyledProperty<double> MaxZoomProperty = UmlDiagramCanvas.MaxZoomProperty.AddOwner<UmlDiagramView>();

    public static readonly StyledProperty<object?> SelectedElementProperty =
        UmlDiagramCanvas.SelectedElementProperty.AddOwner<UmlDiagramView>(new StyledPropertyMetadata<object?>(null, BindingMode.TwoWay));

    public static readonly StyledProperty<bool> IsSelectionEnabledProperty = UmlDiagramCanvas.IsSelectionEnabledProperty.AddOwner<UmlDiagramView>();

    public static readonly StyledProperty<bool> ShowGridProperty = UmlDiagramCanvas.ShowGridProperty.AddOwner<UmlDiagramView>();

    public static readonly StyledProperty<bool> ShowZoomControlsProperty =
        AvaloniaProperty.Register<UmlDiagramView, bool>(nameof(ShowZoomControls), true);

    public static readonly RoutedEvent<UmlSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<UmlDiagramView, UmlSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    private UmlDiagramCanvas? _canvas;
    private UmlDocument? _document;
    private IReadOnlyList<UmlDiagnostic> _diagnostics = [];
    private string? _errorText;
    private readonly List<(Button Button, EventHandler<RoutedEventArgs> Handler)> _buttons = [];

    public UmlDiagramView() => UpdatePseudoClasses();

    /// <summary>PlantUML text (<c>@startuml</c> … <c>@enduml</c>); setting it parses it and fills <see cref="Diagram"/>.</summary>
    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Which diagram of a <see cref="Source"/> with several <c>@startuml</c> blocks is shown.</summary>
    public int DiagramIndex
    {
        get => GetValue(DiagramIndexProperty);
        set => SetValue(DiagramIndexProperty, value);
    }

    /// <summary>The shown diagram; set it directly to show a model built in code.</summary>
    public UmlDiagram? Diagram
    {
        get => GetValue(DiagramProperty);
        set => SetValue(DiagramProperty, value);
    }

    /// <summary>The document parsed from <see cref="Source"/>.</summary>
    public UmlDocument? Document
    {
        get => _document;
        private set => SetAndRaise(DocumentProperty, ref _document, value);
    }

    /// <summary>Problems found in <see cref="Source"/>, with line numbers.</summary>
    public IReadOnlyList<UmlDiagnostic> Diagnostics
    {
        get => _diagnostics;
        private set => SetAndRaise(DiagnosticsProperty, ref _diagnostics, value);
    }

    /// <summary>The first error of <see cref="Diagnostics"/>, shown in the corner of the view.</summary>
    public string? ErrorText
    {
        get => _errorText;
        private set => SetAndRaise(ErrorTextProperty, ref _errorText, value);
    }

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

    /// <inheritdoc cref="UmlDiagramCanvas.SelectedElement"/>
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

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <summary>The zoom buttons in the bottom right corner.</summary>
    public bool ShowZoomControls
    {
        get => GetValue(ShowZoomControlsProperty);
        set => SetValue(ShowZoomControlsProperty, value);
    }

    public event EventHandler<UmlSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    protected override Type StyleKeyOverride => typeof(UmlDiagramView);

    public void ZoomIn() => _canvas?.ZoomIn();

    public void ZoomOut() => _canvas?.ZoomOut();

    public void ZoomToFit() => _canvas?.ZoomToFit();

    public void ResetZoom() => _canvas?.ResetZoom();

    /// <inheritdoc cref="UmlDiagramCanvas.Refresh"/>
    public void Refresh() => _canvas?.Refresh();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        foreach (var (button, handler) in _buttons)
            button.Click -= handler;
        _buttons.Clear();

        _canvas = e.NameScope.Find<UmlDiagramCanvas>(PartCanvas);
        Hook(e.NameScope.Find<Button>(PartZoomInButton), ZoomIn, RiceStrings.UmlZoomIn);
        Hook(e.NameScope.Find<Button>(PartZoomOutButton), ZoomOut, RiceStrings.UmlZoomOut);
        Hook(e.NameScope.Find<Button>(PartZoomToFitButton), ZoomToFit, RiceStrings.UmlZoomToFit);
        Hook(e.NameScope.Find<Button>(PartResetZoomButton), ResetZoom, RiceStrings.UmlResetZoom);
    }

    private void Hook(Button? button, Action action, string tooltip)
    {
        if (button is null)
            return;
        EventHandler<RoutedEventArgs> handler = (_, _) => action();
        button.Click += handler;
        ToolTip.SetTip(button, tooltip);
        _buttons.Add((button, handler));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty || change.Property == DiagramIndexProperty)
            ParseSource();
        else if (change.Property == DiagramProperty)
            UpdatePseudoClasses();
        else if (change.Property == SelectedElementProperty)
        {
            UpdatePseudoClasses();
            RaiseEvent(new UmlSelectionChangedEventArgs(SelectionChangedEvent, change.OldValue, change.NewValue));
        }
    }

    private void ParseSource()
    {
        var source = Source;
        if (string.IsNullOrWhiteSpace(source))
        {
            Document = null;
            Diagnostics = [];
            ErrorText = null;
            SetCurrentValue(DiagramProperty, null);
            UpdatePseudoClasses();
            return;
        }

        var document = UmlDocument.Parse(source);
        Document = document;
        Diagnostics = document.Diagnostics;
        var error = document.Diagnostics.FirstOrDefault(d => d.Severity == UmlDiagnosticSeverity.Error)
                    ?? document.Diagnostics.FirstOrDefault(d => d.Severity == UmlDiagnosticSeverity.Warning);
        ErrorText = error is null ? null : string.Format(RiceStrings.UmlParseError, error.Line, error.Message);

        // While the text is being edited and cannot be read, the last good diagram stays.
        if (document.Diagrams.Count > 0)
            SetCurrentValue(DiagramProperty, document.Diagrams[Math.Clamp(DiagramIndex, 0, document.Diagrams.Count - 1)]);
        UpdatePseudoClasses();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":empty", Diagram is null);
        PseudoClasses.Set(":error", ErrorText is not null);
        PseudoClasses.Set(":hasselection", SelectedElement is not null);
    }
}
