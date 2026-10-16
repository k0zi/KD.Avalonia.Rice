using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Common part of <see cref="RiceProgressRing"/> and <see cref="RiceProgressBar"/>: determinate progress by <c>Value</c>
/// between <c>Minimum</c> and <c>Maximum</c> (0–100 by default), or <see cref="IsIndeterminate"/> for work of unknown length.
/// </summary>
public abstract class RiceProgressBase : RangeBase
{
    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<RiceProgressBase, bool>(nameof(IsIndeterminate));

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<RiceProgressBase, double>(nameof(Thickness), 4);

    public static readonly StyledProperty<bool> ShowValueProperty =
        AvaloniaProperty.Register<RiceProgressBase, bool>(nameof(ShowValue));

    public static readonly StyledProperty<string> ValueStringFormatProperty =
        AvaloniaProperty.Register<RiceProgressBase, string>(nameof(ValueStringFormat), "{0:0}%");

    public static readonly DirectProperty<RiceProgressBase, double> PercentageProperty =
        AvaloniaProperty.RegisterDirect<RiceProgressBase, double>(nameof(Percentage), o => o.Percentage);

    public static readonly DirectProperty<RiceProgressBase, string> ValueTextProperty =
        AvaloniaProperty.RegisterDirect<RiceProgressBase, string>(nameof(ValueText), o => o.ValueText);

    private double _percentage;
    private string _valueText = "";

    protected RiceProgressBase() => UpdateProgress();

    /// <summary>Shows a moving indicator instead of <c>Value</c>, for work of unknown length.</summary>
    public bool IsIndeterminate
    {
        get => GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    /// <summary>Stroke width of the ring / height of the bar.</summary>
    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    /// <summary>Shows <see cref="ValueText"/> (determinate only).</summary>
    public bool ShowValue
    {
        get => GetValue(ShowValueProperty);
        set => SetValue(ShowValueProperty, value);
    }

    /// <summary>Format of <see cref="ValueText"/>; the argument is the <see cref="Percentage"/>.</summary>
    public string ValueStringFormat
    {
        get => GetValue(ValueStringFormatProperty);
        set => SetValue(ValueStringFormatProperty, value);
    }

    /// <summary>Progress between 0 and 100, computed from <c>Value</c>, <c>Minimum</c> and <c>Maximum</c>.</summary>
    public double Percentage
    {
        get => _percentage;
        private set => SetAndRaise(PercentageProperty, ref _percentage, value);
    }

    public string ValueText
    {
        get => _valueText;
        private set => SetAndRaise(ValueTextProperty, ref _valueText, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == MinimumProperty || change.Property == MaximumProperty
            || change.Property == IsIndeterminateProperty || change.Property == ShowValueProperty
            || change.Property == ValueStringFormatProperty)
            UpdateProgress();
    }

    /// <summary>Called after <see cref="Percentage"/> changed, to update the indicator's geometry.</summary>
    protected virtual void OnPercentageUpdated()
    {
    }

    private void UpdateProgress()
    {
        var range = Maximum - Minimum;
        Percentage = range > 0 ? Math.Clamp((Value - Minimum) / range * 100, 0, 100) : 0;
        ValueText = string.Format(CultureInfo.CurrentCulture, ValueStringFormat, Percentage);
        PseudoClasses.Set(":indeterminate", IsIndeterminate);
        PseudoClasses.Set(":determinate", !IsIndeterminate);
        PseudoClasses.Set(":showvalue", ShowValue);
        OnPercentageUpdated();
    }
}
