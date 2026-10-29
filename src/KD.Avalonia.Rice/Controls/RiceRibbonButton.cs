using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.VisualTree;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Command button of a <see cref="RiceRibbonGroup"/>: <see cref="Icon"/> and label (the content) in one of three
/// <see cref="Size"/>s – Large (big icon above the label, the default), Medium (small icon and label in a row) or Small
/// (only the small icon; add a <c>ToolTip.Tip</c>). With a <c>Flyout</c> (e.g. a <c>MenuFlyout</c>) it shows a
/// drop down arrow and opens the flyout.
/// </summary>
public class RiceRibbonButton : Button
{
    public static readonly StyledProperty<object?> IconProperty =
        RiceCard.IconProperty.AddOwner<RiceRibbonButton>();

    public static readonly AttachedProperty<RiceRibbonItemSize> SizeProperty =
        RiceRibbon.SizeProperty.AddOwner<RiceRibbonButton>();

    static RiceRibbonButton()
    {
        SizeProperty.OverrideDefaultValue<RiceRibbonButton>(RiceRibbonItemSize.Large);
    }

    public RiceRibbonButton()
    {
        UpdatePseudoClasses();
    }

    /// <summary>An <c>IImage</c>, a <c>MaterialIconKind</c>, a string (e.g. an emoji) or any control.</summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public RiceRibbonItemSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceRibbonButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty || change.Property == FlyoutProperty)
            UpdatePseudoClasses();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":large", Size == RiceRibbonItemSize.Large);
        PseudoClasses.Set(":medium", Size == RiceRibbonItemSize.Medium);
        PseudoClasses.Set(":small", Size == RiceRibbonItemSize.Small);
        PseudoClasses.Set(":dropdown", Flyout is not null);
    }
}

/// <summary>
/// <see cref="RiceRibbonButton"/> that stays pressed (<see cref="IsChecked"/>), e.g. Bold or a view option. Toggle
/// buttons with the same <see cref="GroupName"/> in the same group work as radio buttons (e.g. text alignment).
/// </summary>
public class RiceRibbonToggleButton : RiceRibbonButton
{
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<RiceRibbonToggleButton, bool>(nameof(IsChecked), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> GroupNameProperty =
        AvaloniaProperty.Register<RiceRibbonToggleButton, string?>(nameof(GroupName));

    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public string? GroupName
    {
        get => GetValue(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceRibbonToggleButton);

    protected override void OnClick()
    {
        // A radio button is not unchecked by a click on it.
        IsChecked = GroupName is not null || !IsChecked;
        base.OnClick();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != IsCheckedProperty)
            return;
        PseudoClasses.Set(":checked", IsChecked);
        if (IsChecked && GroupName is not null && this.GetVisualParent() is Panel panel)
        {
            foreach (var other in panel.Children.OfType<RiceRibbonToggleButton>())
            {
                if (other != this && other.GroupName == GroupName)
                    other.IsChecked = false;
            }
        }
    }
}

/// <summary>Vertical line between the items of a <see cref="RiceRibbonGroup"/>.</summary>
public class RiceRibbonSeparator : TemplatedControl
{
    static RiceRibbonSeparator()
    {
        RiceRibbon.SizeProperty.OverrideDefaultValue<RiceRibbonSeparator>(RiceRibbonItemSize.Large);
    }

    protected override Type StyleKeyOverride => typeof(RiceRibbonSeparator);
}
