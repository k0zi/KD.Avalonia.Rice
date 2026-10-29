using Avalonia;
using Avalonia.Controls;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Placeholder for a view without data ("No files yet", "No search results"): a large muted <see cref="Icon"/>, a
/// <see cref="Title"/>, a <see cref="Description"/> and the content below them, usually the buttons that get the user
/// going. Centered in the space it gets.
/// </summary>
public class RiceEmptyState : ContentControl
{
    public static readonly StyledProperty<object?> IconProperty =
        RiceCard.IconProperty.AddOwner<RiceEmptyState>();

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<RiceEmptyState, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<RiceEmptyState, string?>(nameof(Description));

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<RiceEmptyState, double>(nameof(IconSize), 64);

    /// <summary>An <c>IImage</c>, a <c>MaterialIconKind</c>, a string (e.g. an emoji) or any control.</summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Muted, wrapped text below the title.</summary>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Width and height of the icon. Default 64.</summary>
    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceEmptyState);
}
