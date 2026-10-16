using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using KD.Avalonia.Rice.Controls.Uml;

namespace KD.Avalonia.Rice.Controls.Markdowns;

/// <summary>
/// Shows a Mermaid diagram (flowchart, sequence, class, state, entity relationship, pie, gantt, mind map) in the
/// colors of the current Rice theme. Pan by dragging or scrolling, zoom with Ctrl + wheel or the buttons, click an
/// element to select it.
/// </summary>
[TemplatePart(PartCanvas, typeof(UmlDiagramCanvas))]
[TemplatePart(PartZoomInButton, typeof(Button))]
[TemplatePart(PartZoomOutButton, typeof(Button))]
[TemplatePart(PartZoomToFitButton, typeof(Button))]
[TemplatePart(PartResetZoomButton, typeof(Button))]
[PseudoClasses(":empty", ":error", ":hasselection")]
public class MermaidView : TemplatedControl
{
    public const string PartCanvas = "PART_Canvas";
    public const string PartZoomInButton = "PART_ZoomInButton";
    public const string PartZoomOutButton = "PART_ZoomOutButton";
    public const string PartZoomToFitButton = "PART_ZoomToFitButton";
    public const string PartResetZoomButton = "PART_ResetZoomButton";

    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<MermaidView, string?>(nameof(Source));

    public static readonly DirectProperty<MermaidView, MermaidDocument?> DocumentProperty =
        AvaloniaProperty.RegisterDirect<MermaidView, MermaidDocument?>(nameof(Document), o => o.Document);

    public static readonly DirectProperty<MermaidView, IReadOnlyList<MermaidDiagnostic>> DiagnosticsProperty =
        AvaloniaProperty.RegisterDirect<MermaidView, IReadOnlyList<MermaidDiagnostic>>(nameof(Diagnostics), o => o.Diagnostics);

    public static readonly DirectProperty<MermaidView, string?> ErrorTextProperty =
        AvaloniaProperty.RegisterDirect<MermaidView, string?>(nameof(ErrorText), o => o.ErrorText);

    public static readonly StyledProperty<double> ZoomProperty =
        UmlDiagramCanvas.ZoomProperty.AddOwner<MermaidView>(new StyledPropertyMetadata<double>(1, BindingMode.TwoWay));

    public static readonly StyledProperty<double> MinZoomProperty = UmlDiagramCanvas.MinZoomProperty.AddOwner<MermaidView>();

    public static readonly StyledProperty<double> MaxZoomProperty = UmlDiagramCanvas.MaxZoomProperty.AddOwner<MermaidView>();

    public static readonly StyledProperty<object?> SelectedElementProperty =
        UmlDiagramCanvas.SelectedElementProperty.AddOwner<MermaidView>(new StyledPropertyMetadata<object?>(null, BindingMode.TwoWay));

    public static readonly StyledProperty<bool> IsSelectionEnabledProperty = UmlDiagramCanvas.IsSelectionEnabledProperty.AddOwner<MermaidView>();

    public static readonly StyledProperty<bool> ShowGridProperty = UmlDiagramCanvas.ShowGridProperty.AddOwner<MermaidView>();

    public static readonly StyledProperty<bool> ShowZoomControlsProperty =
        AvaloniaProperty.Register<MermaidView, bool>(nameof(ShowZoomControls), true);

    private UmlDiagramCanvas? _canvas;
    private MermaidDocument? _document;
    private IReadOnlyList<MermaidDiagnostic> _diagnostics = [];
    private string? _errorText;
    private object? _diagram;
    private readonly List<(Button Button, EventHandler<RoutedEventArgs> Handler)> _buttons = [];

    public MermaidView() => UpdatePseudoClasses();

    /// <summary>Mermaid text, e.g. <c>flowchart LR</c> followed by the nodes and edges.</summary>
    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public MermaidDocument? Document
    {
        get => _document;
        private set => SetAndRaise(DocumentProperty, ref _document, value);
    }

    public IReadOnlyList<MermaidDiagnostic> Diagnostics
    {
        get => _diagnostics;
        private set => SetAndRaise(DiagnosticsProperty, ref _diagnostics, value);
    }

    /// <summary>The first problem of <see cref="Diagnostics"/>, shown in the corner of the view.</summary>
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

    /// <summary>The selected model object: a <see cref="MermaidNode"/>, <see cref="MermaidEdge"/>, <see cref="UmlElement"/>, …</summary>
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

    public bool ShowZoomControls
    {
        get => GetValue(ShowZoomControlsProperty);
        set => SetValue(ShowZoomControlsProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(MermaidView);

    public void ZoomIn() => _canvas?.ZoomIn();

    public void ZoomOut() => _canvas?.ZoomOut();

    public void ZoomToFit() => _canvas?.ZoomToFit();

    public void ResetZoom() => _canvas?.ResetZoom();

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
        ShowDiagram();
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
        if (change.Property == SourceProperty)
            ParseSource();
        else if (change.Property == SelectedElementProperty)
            UpdatePseudoClasses();
    }

    private void ParseSource()
    {
        if (string.IsNullOrWhiteSpace(Source))
        {
            Document = null;
            Diagnostics = [];
            ErrorText = null;
            _diagram = null;
            ShowDiagram();
            return;
        }
        var document = MermaidDocument.Parse(Source);
        Document = document;
        Diagnostics = document.Diagnostics;
        ErrorText = document.Diagnostics.FirstOrDefault() is { } first ? string.Format(RiceStrings.MermaidParseError, first.Line, first.Message) : null;
        // While the text is being edited and cannot be read, the last good diagram stays.
        if (document.Diagram is not null)
            _diagram = document.Diagram;
        ShowDiagram();
    }

    private void ShowDiagram()
    {
        if (_canvas is not null)
            ApplyTo(_canvas, _diagram);
        UpdatePseudoClasses();
    }

    /// <summary>Shows a parsed Mermaid diagram (UML model or Mermaid diagram) on a canvas.</summary>
    internal static void ApplyTo(UmlDiagramCanvas canvas, object? diagram)
    {
        canvas.SceneSource = diagram as MermaidDiagram;
        canvas.Diagram = diagram as UmlDiagram;
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":empty", _diagram is null);
        PseudoClasses.Set(":error", ErrorText is not null);
        PseudoClasses.Set(":hasselection", SelectedElement is not null);
    }
}
