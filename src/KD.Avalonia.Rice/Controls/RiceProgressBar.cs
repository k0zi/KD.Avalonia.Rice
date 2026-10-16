using Avalonia;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Horizontal progress bar. Determinate, it fills from the left by <c>Value</c> between <c>Minimum</c> and
/// <c>Maximum</c> (0–100 by default); with <c>IsIndeterminate</c> a segment keeps sliding through until it is
/// switched off. <c>Thickness</c> is the height of the bar.
/// </summary>
public class RiceProgressBar : RiceProgressBase
{
    public static readonly DirectProperty<RiceProgressBar, double> FractionProperty =
        AvaloniaProperty.RegisterDirect<RiceProgressBar, double>(nameof(Fraction), o => o.Fraction);

    private double _fraction;

    /// <summary>Filled part of the bar, 0–1.</summary>
    public double Fraction
    {
        get => _fraction;
        private set => SetAndRaise(FractionProperty, ref _fraction, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceProgressBar);

    protected override void OnPercentageUpdated() => Fraction = Percentage / 100;
}
