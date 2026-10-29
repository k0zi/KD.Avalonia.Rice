using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls;

/// <summary>Outline of a <see cref="RiceSkeleton"/>.</summary>
public enum SkeletonShape
{
    /// <summary>A rounded block filling the control, e.g. an image or a button.</summary>
    Rectangle,

    /// <summary>A circle as large as the smaller side of the control, e.g. an avatar.</summary>
    Circle,

    /// <summary><see cref="RiceSkeleton.Lines"/> lines of text, the last one shorter.</summary>
    Text,
}

/// <summary>
/// Placeholder shaped like the content that is still loading (a block, a circle or lines of text), with a light
/// band sweeping over it. Compose several of them into the outline of a card or a list row, and swap them for the
/// real content once it is there.
/// </summary>
public class RiceSkeleton : Control
{
    public static readonly StyledProperty<SkeletonShape> ShapeProperty =
        AvaloniaProperty.Register<RiceSkeleton, SkeletonShape>(nameof(Shape));

    public static readonly StyledProperty<int> LinesProperty =
        AvaloniaProperty.Register<RiceSkeleton, int>(nameof(Lines), 3, coerce: (_, value) => Math.Max(value, 1));

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<RiceSkeleton, double>(nameof(LineHeight), 12);

    public static readonly StyledProperty<double> LineSpacingProperty =
        AvaloniaProperty.Register<RiceSkeleton, double>(nameof(LineSpacing), 8);

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<RiceSkeleton, CornerRadius>(nameof(CornerRadius), new CornerRadius(4));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<RiceSkeleton, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> HighlightProperty =
        AvaloniaProperty.Register<RiceSkeleton, IBrush?>(nameof(Highlight));

    public static readonly StyledProperty<bool> IsAnimatedProperty =
        AvaloniaProperty.Register<RiceSkeleton, bool>(nameof(IsAnimated), true);

    /// <summary>Position of the sweeping band from 0 (left of the control) to 1 (right of it); animated by the theme.</summary>
    public static readonly StyledProperty<double> ShimmerPositionProperty =
        AvaloniaProperty.Register<RiceSkeleton, double>(nameof(ShimmerPosition));

    // Width of the band relative to the control, and of the last text line relative to the others.
    private const double BandWidth = 0.4;
    private const double LastLineWidth = 0.6;

    static RiceSkeleton()
    {
        AffectsRender<RiceSkeleton>(ShapeProperty, LinesProperty, LineHeightProperty, LineSpacingProperty,
            CornerRadiusProperty, FillProperty, HighlightProperty, IsAnimatedProperty, ShimmerPositionProperty);
        AffectsMeasure<RiceSkeleton>(ShapeProperty, LinesProperty, LineHeightProperty, LineSpacingProperty);
    }

    public RiceSkeleton() => PseudoClasses.Set(":animated", true);

    public SkeletonShape Shape
    {
        get => GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
    }

    /// <summary>Number of text lines (<see cref="SkeletonShape.Text"/>). Default 3.</summary>
    public int Lines
    {
        get => GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    /// <summary>Height of a text line; also the default height of a rectangle. Default 12.</summary>
    public double LineHeight
    {
        get => GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    public double LineSpacing
    {
        get => GetValue(LineSpacingProperty);
        set => SetValue(LineSpacingProperty, value);
    }

    /// <summary>Corners of the rectangle and of the text lines. Default 4.</summary>
    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>The placeholder color; defaults to <c>RiceSkeletonBrush</c>.</summary>
    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>Solid color of the sweeping band; defaults to <c>RiceSkeletonHighlightBrush</c>.</summary>
    public IBrush? Highlight
    {
        get => GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }

    /// <summary>Sweeps the band over the placeholder. Default true.</summary>
    public bool IsAnimated
    {
        get => GetValue(IsAnimatedProperty);
        set => SetValue(IsAnimatedProperty, value);
    }

    public double ShimmerPosition
    {
        get => GetValue(ShimmerPositionProperty);
        set => SetValue(ShimmerPositionProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsAnimatedProperty)
            PseudoClasses.Set(":animated", IsAnimated);
    }

    protected override Size MeasureOverride(Size availableSize) => Shape switch
    {
        SkeletonShape.Circle => new Size(40, 40),
        SkeletonShape.Text => new Size(0, Lines * LineHeight + (Lines - 1) * LineSpacing),
        _ => new Size(0, LineHeight),
    };

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
            return;

        var shape = CreateGeometry(size);
        context.DrawGeometry(Fill, null, shape);

        if (!IsAnimated || Highlight is not ISolidColorBrush { Color: var color })
            return;
        // The band moves from fully left of the control to fully right of it, fading in and out at its edges.
        var band = size.Width * BandWidth;
        var x = -band + ShimmerPosition * (size.Width + band);
        var transparent = Color.FromArgb(0, color.R, color.G, color.B);
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops = { new GradientStop(transparent, 0), new GradientStop(color, 0.5), new GradientStop(transparent, 1) },
        };
        using (context.PushGeometryClip(shape))
            context.DrawRectangle(brush, null, new Rect(x, 0, band, size.Height));
    }

    internal Geometry CreateGeometry(Size size)
    {
        switch (Shape)
        {
            case SkeletonShape.Circle:
                var diameter = Math.Min(size.Width, size.Height);
                return new EllipseGeometry(new Rect((size.Width - diameter) / 2, (size.Height - diameter) / 2, diameter, diameter));
            case SkeletonShape.Text:
                var group = new GeometryGroup();
                for (var i = 0; i < Lines; i++)
                {
                    var width = i == Lines - 1 && Lines > 1 ? size.Width * LastLineWidth : size.Width;
                    group.Children.Add(RoundedRect(new Rect(0, i * (LineHeight + LineSpacing), width, LineHeight)));
                }
                return group;
            default:
                return RoundedRect(new Rect(size));
        }
    }

    private Geometry RoundedRect(Rect rect)
    {
        var radius = Math.Min(CornerRadius.TopLeft, Math.Min(rect.Width, rect.Height) / 2);
        return new RectangleGeometry(rect, radius, radius);
    }
}
