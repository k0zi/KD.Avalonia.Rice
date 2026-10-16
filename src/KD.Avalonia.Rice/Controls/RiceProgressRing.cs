using Avalonia;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Circular progress indicator. Determinate, it fills the ring by <c>Value</c> between <c>Minimum</c> and <c>Maximum</c>
/// (0–100 by default); with <c>IsIndeterminate</c> an arc keeps spinning until it is switched off.
/// </summary>
public class RiceProgressRing : RiceProgressBase
{
    public static readonly DirectProperty<RiceProgressRing, double> SweepAngleProperty =
        AvaloniaProperty.RegisterDirect<RiceProgressRing, double>(nameof(SweepAngle), o => o.SweepAngle);

    private double _sweepAngle;

    /// <summary>Length of the filled arc in degrees (0–360).</summary>
    public double SweepAngle
    {
        get => _sweepAngle;
        private set => SetAndRaise(SweepAngleProperty, ref _sweepAngle, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceProgressRing);

    protected override void OnPercentageUpdated() => SweepAngle = Percentage * 3.6;
}
