using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Rounded frame of a <see cref="RiceGroupBox"/> with a gap in the top line for the header.
/// Drawn as a path, so the header does not need a background to hide the line.
/// </summary>
public class RiceGroupBoxFrame : Control
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        Border.BackgroundProperty.AddOwner<RiceGroupBoxFrame>();

    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        Border.BorderBrushProperty.AddOwner<RiceGroupBoxFrame>();

    /// <summary>Only the top value is used, for every side.</summary>
    public static readonly StyledProperty<Thickness> BorderThicknessProperty =
        Border.BorderThicknessProperty.AddOwner<RiceGroupBoxFrame>();

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<RiceGroupBoxFrame>();

    /// <summary>X of the gap in the top line.</summary>
    public static readonly StyledProperty<double> GapStartProperty =
        AvaloniaProperty.Register<RiceGroupBoxFrame, double>(nameof(GapStart));

    /// <summary>Width of the gap in the top line; 0 draws a closed frame.</summary>
    public static readonly StyledProperty<double> GapWidthProperty =
        AvaloniaProperty.Register<RiceGroupBoxFrame, double>(nameof(GapWidth));

    static RiceGroupBoxFrame() =>
        AffectsRender<RiceGroupBoxFrame>(BackgroundProperty, BorderBrushProperty, BorderThicknessProperty,
            CornerRadiusProperty, GapStartProperty, GapWidthProperty);

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public IBrush? BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public double GapStart
    {
        get => GetValue(GapStartProperty);
        set => SetValue(GapStartProperty, value);
    }

    public double GapWidth
    {
        get => GetValue(GapWidthProperty);
        set => SetValue(GapWidthProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var radius = CornerRadius;
        if (Background is { } background)
            context.DrawRectangle(background, null, new RoundedRect(bounds, radius));

        var thickness = BorderThickness.Top;
        if (BorderBrush is not { } brush || thickness <= 0)
            return;

        // The stroke is centered on the path, so the path runs half a line inside the bounds.
        var r = bounds.Deflate(thickness / 2);
        if (r.Width <= 0 || r.Height <= 0)
            return;
        var max = Math.Min(r.Width, r.Height) / 2;
        double tl = Math.Min(radius.TopLeft, max), tr = Math.Min(radius.TopRight, max);
        double br = Math.Min(radius.BottomRight, max), bl = Math.Min(radius.BottomLeft, max);

        // The gap stays on the straight part of the top line.
        var hasGap = GapWidth > 0;
        var gapStart = Math.Clamp(GapStart, r.Left + tl, r.Right - tr);
        var gapEnd = Math.Clamp(GapStart + GapWidth, gapStart, r.Right - tr);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(hasGap ? gapEnd : r.Left + tl, r.Top), false);
            ctx.LineTo(new Point(r.Right - tr, r.Top));
            Corner(ctx, new Point(r.Right, r.Top + tr), tr);
            ctx.LineTo(new Point(r.Right, r.Bottom - br));
            Corner(ctx, new Point(r.Right - br, r.Bottom), br);
            ctx.LineTo(new Point(r.Left + bl, r.Bottom));
            Corner(ctx, new Point(r.Left, r.Bottom - bl), bl);
            ctx.LineTo(new Point(r.Left, r.Top + tl));
            Corner(ctx, new Point(r.Left + tl, r.Top), tl);
            if (hasGap)
                ctx.LineTo(new Point(gapStart, r.Top));
            ctx.EndFigure(!hasGap);
        }

        context.DrawGeometry(null, new Pen(brush, thickness), geometry);
    }

    private static void Corner(StreamGeometryContext ctx, Point end, double radius)
    {
        if (radius > 0)
            ctx.ArcTo(end, new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        else
            ctx.LineTo(end);
    }
}
