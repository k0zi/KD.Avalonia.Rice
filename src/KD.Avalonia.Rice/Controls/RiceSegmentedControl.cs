using Avalonia;
using Avalonia.Controls;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Row of 2–5 linked buttons of which exactly one is selected (like the linked buttons of GNOME / libadwaita):
/// a view switcher, a filter, a unit picker. A <see cref="ListBox"/> underneath: bind <c>SelectedItem</c> /
/// <c>SelectedIndex</c>, handle <c>SelectionChanged</c>. Items are <see cref="RiceSegmentedItem"/>s or data items.
/// </summary>
public class RiceSegmentedControl : ListBox
{
    /// <summary>Gives every segment the width of the widest one. Default false.</summary>
    public static readonly StyledProperty<bool> IsEqualWidthProperty =
        AvaloniaProperty.Register<RiceSegmentedControl, bool>(nameof(IsEqualWidth));

    static RiceSegmentedControl()
    {
        SelectionModeProperty.OverrideDefaultValue<RiceSegmentedControl>(SelectionMode.Single | SelectionMode.AlwaysSelected);
    }

    public bool IsEqualWidth
    {
        get => GetValue(IsEqualWidthProperty);
        set => SetValue(IsEqualWidthProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceSegmentedControl);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new RiceSegmentedItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<RiceSegmentedItem>(item, out recycleKey);
}

/// <summary>One segment of a <see cref="RiceSegmentedControl"/>: optional <see cref="Icon"/> and the content.</summary>
public class RiceSegmentedItem : ListBoxItem
{
    public static readonly StyledProperty<object?> IconProperty =
        RiceCard.IconProperty.AddOwner<RiceSegmentedItem>();

    /// <summary>An <c>IImage</c>, a <c>MaterialIconKind</c>, a string (e.g. an emoji) or any control.</summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceSegmentedItem);
}
