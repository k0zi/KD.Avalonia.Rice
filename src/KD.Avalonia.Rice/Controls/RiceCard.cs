using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Raised surface for related content, with an optional header (<c>Header</c> as title, <see cref="Subtitle"/>,
/// <see cref="Icon"/>, <see cref="HeaderActions"/> on the right) and an optional <see cref="Footer"/> (e.g. buttons).
/// <see cref="Elevation"/> sets the shadow depth (0 = flat).
/// </summary>
public class RiceCard : HeaderedContentControl
{
    public static readonly StyledProperty<object?> SubtitleProperty =
        AvaloniaProperty.Register<RiceCard, object?>(nameof(Subtitle));

    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<RiceCard, object?>(nameof(Icon));

    public static readonly StyledProperty<object?> HeaderActionsProperty =
        AvaloniaProperty.Register<RiceCard, object?>(nameof(HeaderActions));

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<RiceCard, object?>(nameof(Footer));

    public static readonly StyledProperty<IDataTemplate?> FooterTemplateProperty =
        AvaloniaProperty.Register<RiceCard, IDataTemplate?>(nameof(FooterTemplate));

    public static readonly StyledProperty<int> ElevationProperty =
        AvaloniaProperty.Register<RiceCard, int>(nameof(Elevation), 1, coerce: (_, value) => Math.Clamp(value, 0, 3));

    private static readonly string[] ElevationClasses = [":elevation0", ":elevation1", ":elevation2", ":elevation3"];

    public RiceCard() => UpdatePseudoClasses();

    public object? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>An <c>IImage</c>, a <c>MaterialIconKind</c>, a string (e.g. an emoji) or any control.</summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Right side of the header, e.g. a menu button.</summary>
    public object? HeaderActions
    {
        get => GetValue(HeaderActionsProperty);
        set => SetValue(HeaderActionsProperty, value);
    }

    /// <summary>Bar below the content, separated by a line; right aligned by default (e.g. buttons).</summary>
    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    public IDataTemplate? FooterTemplate
    {
        get => GetValue(FooterTemplateProperty);
        set => SetValue(FooterTemplateProperty, value);
    }

    /// <summary>Shadow depth from 0 (flat) to 3.</summary>
    public int Elevation
    {
        get => GetValue(ElevationProperty);
        set => SetValue(ElevationProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceCard);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeaderProperty || change.Property == SubtitleProperty || change.Property == IconProperty
            || change.Property == HeaderActionsProperty || change.Property == FooterProperty
            || change.Property == ElevationProperty)
            UpdatePseudoClasses();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":has-header", Header is not null || Subtitle is not null || Icon is not null || HeaderActions is not null);
        PseudoClasses.Set(":has-footer", Footer is not null);
        for (var i = 0; i < ElevationClasses.Length; i++)
            PseudoClasses.Set(ElevationClasses[i], Elevation == i);
    }
}
