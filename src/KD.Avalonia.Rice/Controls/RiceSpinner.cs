using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Small busy indicator for inline use, e.g. inside a button next to its text: a spinning arc without a track
/// in the current text color (<c>Foreground</c> is inherited). Hidden while <see cref="IsActive"/> is false.
/// </summary>
public class RiceSpinner : TemplatedControl
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<RiceSpinner, bool>(nameof(IsActive), true);

    public static readonly StyledProperty<double> ThicknessProperty =
        RiceProgressBase.ThicknessProperty.AddOwner<RiceSpinner>(new(2));

    public RiceSpinner() => UpdateActive();

    /// <summary>Spins and is visible while true; false hides it and stops the animation.</summary>
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>Stroke width of the arc.</summary>
    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceSpinner);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty)
            UpdateActive();
    }

    private void UpdateActive() => PseudoClasses.Set(":active", IsActive);
}
