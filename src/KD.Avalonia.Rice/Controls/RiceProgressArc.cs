using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Draws one arc of a circle fitted to the smaller side of the control; used by <see cref="RiceProgressRing"/>.
/// Angles are in degrees, 0 is the right side, positive values go clockwise; a sweep of 360 draws the full circle.
/// </summary>
public class RiceProgressArc : Control
{
    public static readonly StyledProperty<double> StartAngleProperty =
        AvaloniaProperty.Register<RiceProgressArc, double>(nameof(StartAngle));

    public static readonly StyledProperty<double> SweepAngleProperty =
        AvaloniaProperty.Register<RiceProgressArc, double>(nameof(SweepAngle));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<RiceProgressArc, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<RiceProgressArc, double>(nameof(StrokeThickness), 4);

    // Kept across frames: an animated arc renders every frame, but its pen only changes with Stroke or StrokeThickness.
    private Pen? _pen;

    static RiceProgressArc() =>
        AffectsRender<RiceProgressArc>(StartAngleProperty, SweepAngleProperty, StrokeProperty, StrokeThicknessProperty);

    public double StartAngle
    {
        get => GetValue(StartAngleProperty);
        set => SetValue(StartAngleProperty, value);
    }

    public double SweepAngle
    {
        get => GetValue(SweepAngleProperty);
        set => SetValue(SweepAngleProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StrokeProperty || change.Property == StrokeThicknessProperty)
            _pen = null;
    }

    public override void Render(DrawingContext context)
    {
        var sweep = Math.Clamp(SweepAngle, 0, 360);
        var thickness = StrokeThickness;
        // Inset by half the stroke, so the ring is not clipped.
        var radius = (Math.Min(Bounds.Width, Bounds.Height) - thickness) / 2;
        if (Stroke is null || sweep <= 0 || thickness <= 0 || radius <= 0)
            return;

        // Round caps only show on an open arc; a closed circle has no ends.
        _pen ??= new Pen(Stroke, thickness, lineCap: PenLineCap.Round);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        if (sweep >= 360)
        {
            context.DrawEllipse(null, _pen, center, radius, radius);
            return;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(PointOnCircle(center, radius, StartAngle), false);
            ctx.ArcTo(PointOnCircle(center, radius, StartAngle + sweep), new Size(radius, radius), 0,
                sweep > 180, SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }
        context.DrawGeometry(null, _pen, geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double angle)
    {
        var radians = angle * Math.PI / 180;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }
}
