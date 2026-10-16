using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Draws a pill shaped segment of a horizontal track; used by <see cref="RiceProgressBar"/>.
/// <see cref="Start"/> and <see cref="End"/> are fractions of the width (0 = left, 1 = right); the part outside 0–1
/// is cut, so an animation can slide the segment in and out.
/// </summary>
public class RiceProgressSegment : Control
{
    public static readonly StyledProperty<double> StartProperty =
        AvaloniaProperty.Register<RiceProgressSegment, double>(nameof(Start));

    public static readonly StyledProperty<double> EndProperty =
        AvaloniaProperty.Register<RiceProgressSegment, double>(nameof(End), 1);

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<RiceProgressSegment, IBrush?>(nameof(Fill));

    static RiceProgressSegment() =>
        AffectsRender<RiceProgressSegment>(StartProperty, EndProperty, FillProperty);

    public double Start
    {
        get => GetValue(StartProperty);
        set => SetValue(StartProperty, value);
    }

    public double End
    {
        get => GetValue(EndProperty);
        set => SetValue(EndProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        var left = Math.Clamp(Start, 0, 1) * width;
        var right = Math.Clamp(End, 0, 1) * width;
        if (Fill is null || right - left <= 0 || height <= 0)
            return;

        // Pill shape: fully rounded ends, also for segments shorter than the height.
        var radius = Math.Min(height, right - left) / 2;
        context.DrawRectangle(Fill, null, new RoundedRect(new Rect(left, 0, right - left, height), radius));
    }
}
