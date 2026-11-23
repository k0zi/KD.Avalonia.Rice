using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Three large, soft color blobs that fade into each other, optionally drifting slowly. It draws only the blobs, so put
/// it on a solid background. <see cref="RiceWindow"/> uses it for <see cref="RiceWindowMaterial.Gradient"/>; the colors
/// default to pastels of the current theme's accent and two shades of it.
/// </summary>
public class RiceGradientBackground : Control
{
    public static readonly StyledProperty<Color> Color1Property =
        AvaloniaProperty.Register<RiceGradientBackground, Color>(nameof(Color1));

    public static readonly StyledProperty<Color> Color2Property =
        AvaloniaProperty.Register<RiceGradientBackground, Color>(nameof(Color2));

    public static readonly StyledProperty<Color> Color3Property =
        AvaloniaProperty.Register<RiceGradientBackground, Color>(nameof(Color3));

    public static readonly StyledProperty<bool> IsAnimatedProperty =
        AvaloniaProperty.Register<RiceGradientBackground, bool>(nameof(IsAnimated));

    /// <summary>Position in the drift cycle from 0 to 1 (1 looks the same as 0); animated by the theme.</summary>
    public static readonly StyledProperty<double> PhaseProperty =
        AvaloniaProperty.Register<RiceGradientBackground, double>(nameof(Phase));

    // Center and radius relative to the control (the radius to its larger side), how far the center drifts and how many
    // times it goes around horizontally / vertically per cycle; whole numbers keep the loop seamless.
    private readonly record struct Blob(double X, double Y, double Radius, double Drift, int TurnsX, int TurnsY, double Offset);

    private static readonly Blob[] Blobs =
    [
        new(0.12, 0.08, 0.75, 0.10, 1, 2, 0.0),
        new(0.92, 0.38, 0.60, 0.12, 2, 1, 0.3),
        new(0.38, 1.00, 0.70, 0.10, 1, 1, 0.6),
    ];

    static RiceGradientBackground()
    {
        AffectsRender<RiceGradientBackground>(Color1Property, Color2Property, Color3Property, PhaseProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<RiceGradientBackground>(false);
    }

    /// <summary>The largest blob, top left; defaults to <c>RiceGradient1Color</c> (the accent).</summary>
    public Color Color1
    {
        get => GetValue(Color1Property);
        set => SetValue(Color1Property, value);
    }

    /// <summary>The blob on the right; defaults to <c>RiceGradient2Color</c>.</summary>
    public Color Color2
    {
        get => GetValue(Color2Property);
        set => SetValue(Color2Property, value);
    }

    /// <summary>The blob at the bottom; defaults to <c>RiceGradient3Color</c>.</summary>
    public Color Color3
    {
        get => GetValue(Color3Property);
        set => SetValue(Color3Property, value);
    }

    /// <summary>Lets the blobs drift slowly (one cycle takes a minute). Default false.</summary>
    public bool IsAnimated
    {
        get => GetValue(IsAnimatedProperty);
        set => SetValue(IsAnimatedProperty, value);
    }

    public double Phase
    {
        get => GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // A hidden background (e.g. a window with another material) does not keep the animation running.
        if (change.Property == IsAnimatedProperty || change.Property == IsVisibleProperty)
            PseudoClasses.Set(":animated", IsAnimated && IsVisible);
    }

    /// <summary>Center of blob <paramref name="index"/> relative to the control at <paramref name="phase"/>.</summary>
    internal static Point BlobCenter(int index, double phase)
    {
        var blob = Blobs[index];
        var angle = 2 * Math.PI * (phase + blob.Offset);
        return new Point(blob.X + blob.Drift * Math.Sin(blob.TurnsX * angle), blob.Y + blob.Drift * Math.Cos(blob.TurnsY * angle));
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
            return;

        var extent = Math.Max(size.Width, size.Height);
        Color[] colors = [Color1, Color2, Color3];
        for (var i = 0; i < Blobs.Length; i++)
        {
            var color = colors[i];
            if (color.A == 0)
                continue;
            var center = BlobCenter(i, Phase);
            var point = new RelativePoint(center.X * size.Width, center.Y * size.Height, RelativeUnit.Absolute);
            var radius = new RelativeScalar(Blobs[i].Radius * extent, RelativeUnit.Absolute);
            // Falls off quickly at first and slowly towards the edge, so neighboring blobs melt into each other.
            var brush = new RadialGradientBrush
            {
                Center = point,
                GradientOrigin = point,
                RadiusX = radius,
                RadiusY = radius,
                GradientStops =
                {
                    new GradientStop(color, 0),
                    new GradientStop(ColorMath.WithAlpha(color, (byte)(color.A * 0.45)), 0.45),
                    new GradientStop(ColorMath.WithAlpha(color, 0), 1),
                },
            };
            context.DrawRectangle(brush, null, new Rect(size));
        }
    }
}
