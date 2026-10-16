using Avalonia;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>Color slots of the scene; the canvas maps them to its (theme) brushes.</summary>
internal enum UmlBrush
{
    None,
    Node,
    Header,
    Border,
    Line,
    Text,
    MutedText,
    Note,
    NoteBorder,
    Container,
    Accent,
    /// <summary>Filled symbols: initial state, fork bars, filled arrow heads.</summary>
    Solid,
    /// <summary>The canvas background, e.g. inside hollow arrow heads and behind labels.</summary>
    Background,
    Lane,
    Public,
    Private,
    Protected,
    PackageVisibility,
    BadgeClass,
    BadgeAbstract,
    BadgeInterface,
    BadgeEnum,
    BadgeOther,
    /// <summary>Chart colors (pie slices, gantt sections, mind map branches); Series0 + n.</summary>
    Series0,
    Series1,
    Series2,
    Series3,
    Series4,
    Series5,
    Series6,
    Series7,
    /// <summary>Text on the chart colors.</summary>
    OnSeries,
}

internal enum UmlShape
{
    Rectangle,
    RoundedRectangle,
    /// <summary>Rectangle with rounded top corners (header band of a classifier).</summary>
    TopRounded,
    Ellipse,
    Diamond,
    Hexagon,
    /// <summary>Rectangle with a folded top-right corner.</summary>
    Note,
    /// <summary>Folder / package: a tab of width <see cref="ShapePrim.Extra"/> and height <see cref="ShapePrim.Extra2"/> on top.</summary>
    Folder,
    /// <summary>Frame title tag (pentagon) in the top-left corner, size Extra × Extra2.</summary>
    Frame,
    /// <summary>Box with depth <see cref="ShapePrim.Extra"/> (deployment node).</summary>
    Node3D,
    Cylinder,
    Cloud,
    /// <summary>Two stacked rectangles.</summary>
    Collections,
    /// <summary>Band with pointed ends (concise timing lifeline).</summary>
    Band,
    /// <summary>Rectangle with double vertical lines at the sides (flowchart subroutine).</summary>
    Subroutine,
    /// <summary>Leaning right.</summary>
    Parallelogram,
    /// <summary>Leaning left.</summary>
    ParallelogramAlt,
    /// <summary>Wider at the bottom.</summary>
    Trapezoid,
    /// <summary>Wider at the top.</summary>
    TrapezoidAlt,
    /// <summary>Flag: rectangle with a notch on the left.</summary>
    Asymmetric,
    DoubleCircle,
}

internal enum UmlStroke
{
    Solid,
    Dashed,
    Dotted,
}

internal enum UmlCurve
{
    Polyline,
    /// <summary>Smooth curve through the points.</summary>
    Smooth,
    /// <summary>Points are start, control 1, control 2, end of one cubic Bézier.</summary>
    Bezier,
}

internal enum UmlGlyph
{
    VisibilityPublic,
    VisibilityPrivate,
    VisibilityProtected,
    VisibilityPackage,
    /// <summary>Round badge with a letter (C, I, E, A, …).</summary>
    Badge,
    ComponentIcon,
    ArtifactIcon,
    StickFigure,
    Boundary,
    Control,
    Entity,
    DatabaseIcon,
    /// <summary>Destruction of a lifeline.</summary>
    Cross,
    /// <summary>Encircled cross (flow final, exit point).</summary>
    CircleCross,
    /// <summary>Filled circle in a ring (final state).</summary>
    Bullseye,
    /// <summary>Filled circle (initial state).</summary>
    Dot,
    /// <summary>Hollow circle (entry point, provided interface).</summary>
    Circle,
    /// <summary>Required interface (socket half circle), opening to the right.</summary>
    Socket,
}

internal abstract class UmlPrim
{
    /// <summary>Index of the <see cref="UmlHitItem"/> this belongs to, -1 for none.</summary>
    public int Item { get; set; } = -1;
}

internal sealed class ShapePrim : UmlPrim
{
    public UmlShape Shape { get; init; }
    public Rect Rect { get; init; }
    public UmlBrush Fill { get; init; } = UmlBrush.Node;
    public UmlBrush Stroke { get; init; } = UmlBrush.Border;
    public double Thickness { get; init; } = 1;
    public double Radius { get; init; }
    public bool Shadow { get; init; }
    public UmlStroke StrokeStyle { get; init; }
    public double Extra { get; init; }
    public double Extra2 { get; init; }
}

internal sealed class TextPrim : UmlPrim
{
    public required string Text { get; init; }
    /// <summary>Top-left corner.</summary>
    public Point Origin { get; init; }
    public Size Size { get; init; }
    public double FontSize { get; init; }
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public bool Underline { get; init; }
    public bool Center { get; init; }
    public UmlBrush Brush { get; init; } = UmlBrush.Text;
}

internal sealed class LinePrim : UmlPrim
{
    public required IReadOnlyList<Point> Points { get; init; }
    public UmlBrush Brush { get; init; } = UmlBrush.Line;
    public double Thickness { get; init; } = 1.25;
    public UmlStroke Style { get; init; }
    public UmlCurve Curve { get; init; }
}

internal sealed class HeadPrim : UmlPrim
{
    public Point Tip { get; init; }
    /// <summary>Unit vector pointing into the tip.</summary>
    public Vector Direction { get; init; }
    public UmlArrowHead Head { get; init; }
    public UmlBrush Brush { get; init; } = UmlBrush.Line;
}

internal sealed class GlyphPrim : UmlPrim
{
    public UmlGlyph Glyph { get; init; }
    public Rect Rect { get; init; }
    public string? Letter { get; init; }
    public UmlBrush Brush { get; init; } = UmlBrush.Line;
    public UmlBrush Fill { get; init; } = UmlBrush.Node;
}

/// <summary>Slice of a pie chart; angles in degrees, clockwise from the top.</summary>
internal sealed class WedgePrim : UmlPrim
{
    public Point Center { get; init; }
    public double Radius { get; init; }
    public double StartAngle { get; init; }
    public double SweepAngle { get; init; }
    public UmlBrush Fill { get; init; } = UmlBrush.Series0;
    public UmlBrush Stroke { get; init; } = UmlBrush.Background;
}

/// <summary>Something that can be hovered and selected: an element, a relation, a message, …</summary>
internal sealed class UmlHitItem(object model, Rect bounds)
{
    public object Model { get; } = model;

    public Rect Bounds { get; set; } = bounds;

    /// <summary>The line of an edge; hit within a few pixels of it.</summary>
    public IReadOnlyList<Point>? Path { get; init; }

    public UmlCurve Curve { get; init; }

    /// <summary>Models at the two ends of an edge, highlighted together with it.</summary>
    public object? From { get; init; }

    public object? To { get; init; }

    /// <summary>Containers are hit only on their title and border, so their content stays reachable.</summary>
    public bool IsContainer { get; init; }

    public double HeaderHeight { get; init; }

    public double CornerRadius { get; init; } = 6;

    public bool IsEdge => Path is not null;
}

/// <summary>A laid out diagram: drawing primitives in painting order plus the interactive items.</summary>
internal sealed class UmlScene
{
    public List<UmlPrim> Prims { get; } = [];

    public List<UmlHitItem> Items { get; } = [];

    public Rect Bounds { get; set; }

    public static UmlScene Empty { get; } = new();

    public int AddItem(UmlHitItem item)
    {
        Items.Add(item);
        return Items.Count - 1;
    }

    public T Add<T>(T prim, int item = -1) where T : UmlPrim
    {
        prim.Item = item;
        Prims.Add(prim);
        return prim;
    }
}

/// <summary>A diagram that builds its own scene (Mermaid diagrams without a UML model).</summary>
internal interface IUmlSceneSource
{
    UmlScene Build(UmlStyle style);
}

/// <summary>Text measurement; the real one uses Avalonia's text layout, tests use an estimate.</summary>
internal interface IUmlTextMeasurer
{
    Size Measure(string text, double fontSize, bool bold = false, bool italic = false);
}

/// <summary>Font sizes and spacing shared by the scene builders.</summary>
internal sealed class UmlStyle(IUmlTextMeasurer measurer, double fontSize)
{
    public static UmlBrush Series(int index) => UmlBrush.Series0 + (index % 8 + 8) % 8;

    public IUmlTextMeasurer Measurer { get; } = measurer;

    public double FontSize { get; } = fontSize;

    public double SmallFontSize => Math.Max(8, FontSize - 2);

    public double Padding => Math.Round(FontSize * 0.7);

    public Size Measure(string text, bool bold = false, bool italic = false, bool small = false) =>
        string.IsNullOrEmpty(text) ? default : Measurer.Measure(text, small ? SmallFontSize : FontSize, bold, italic);

    public TextPrim Text(string text, Point origin, bool bold = false, bool italic = false, bool small = false,
        UmlBrush brush = UmlBrush.Text, bool underline = false, bool center = false)
    {
        var size = Measure(text, bold, italic, small);
        return new TextPrim
        {
            Text = text,
            Origin = origin,
            Size = size,
            FontSize = small ? SmallFontSize : FontSize,
            Bold = bold,
            Italic = italic,
            Underline = underline,
            Center = center,
            Brush = brush,
        };
    }

    /// <summary>A text centered horizontally on <paramref name="centerX"/>.</summary>
    public TextPrim CenteredText(string text, double centerX, double top, bool bold = false, bool italic = false,
        bool small = false, UmlBrush brush = UmlBrush.Text, bool underline = false)
    {
        var size = Measure(text, bold, italic, small);
        return Text(text, new Point(centerX - size.Width / 2, top), bold, italic, small, brush, underline, center: true);
    }

    public static string Guillemets(IEnumerable<string> stereotypes) => string.Join(" ", stereotypes.Select(s => $"«{s}»"));
}
