using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Wraps its content and, while <see cref="IsActive"/> is true, covers it with a translucent layer showing a
/// <see cref="RiceProgressRing"/> and an optional <see cref="Message"/>. The covered content does not get pointer or
/// keyboard input meanwhile. The ring is indeterminate by default; set <see cref="IsIndeterminate"/> to false and
/// <see cref="Value"/> (0–100) to show real progress.
/// </summary>
public class RiceLoadingOverlay : ContentControl
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        RiceSpinner.IsActiveProperty.AddOwner<RiceLoadingOverlay>(new(false));

    public static readonly StyledProperty<object?> MessageProperty =
        AvaloniaProperty.Register<RiceLoadingOverlay, object?>(nameof(Message));

    public static readonly StyledProperty<IDataTemplate?> MessageTemplateProperty =
        AvaloniaProperty.Register<RiceLoadingOverlay, IDataTemplate?>(nameof(MessageTemplate));

    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        RiceProgressBase.IsIndeterminateProperty.AddOwner<RiceLoadingOverlay>(new(true));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<RiceLoadingOverlay, double>(nameof(Value));

    public static readonly StyledProperty<bool> ShowValueProperty =
        RiceProgressBase.ShowValueProperty.AddOwner<RiceLoadingOverlay>(new(true));

    public static readonly StyledProperty<double> IndicatorSizeProperty =
        AvaloniaProperty.Register<RiceLoadingOverlay, double>(nameof(IndicatorSize), 48);

    public static readonly StyledProperty<IBrush?> OverlayBrushProperty =
        AvaloniaProperty.Register<RiceLoadingOverlay, IBrush?>(nameof(OverlayBrush));

    public static readonly StyledProperty<bool> BlurContentProperty =
        AvaloniaProperty.Register<RiceLoadingOverlay, bool>(nameof(BlurContent));

    public RiceLoadingOverlay()
    {
        // Tunnel: the covered content must not see keys or text, Tab included, while the overlay is shown.
        AddHandler(KeyDownEvent, BlockInput, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, BlockInput, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, BlockInput, RoutingStrategies.Tunnel);
    }

    /// <summary>Shows the overlay and blocks the content.</summary>
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>Text (or any content) below the ring, e.g. "Loading…".</summary>
    public object? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public IDataTemplate? MessageTemplate
    {
        get => GetValue(MessageTemplateProperty);
        set => SetValue(MessageTemplateProperty, value);
    }

    /// <summary>Spinning ring (default); false shows <see cref="Value"/>.</summary>
    public bool IsIndeterminate
    {
        get => GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    /// <summary>Progress 0–100, shown when <see cref="IsIndeterminate"/> is false.</summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Shows the percentage in the ring (determinate only). Default true.</summary>
    public bool ShowValue
    {
        get => GetValue(ShowValueProperty);
        set => SetValue(ShowValueProperty, value);
    }

    /// <summary>Width and height of the ring.</summary>
    public double IndicatorSize
    {
        get => GetValue(IndicatorSizeProperty);
        set => SetValue(IndicatorSizeProperty, value);
    }

    /// <summary>The covering layer; defaults to <c>RiceOverlayBrush</c> (translucent background color).</summary>
    public IBrush? OverlayBrush
    {
        get => GetValue(OverlayBrushProperty);
        set => SetValue(OverlayBrushProperty, value);
    }

    /// <summary>Blurs the covered content while active.</summary>
    public bool BlurContent
    {
        get => GetValue(BlurContentProperty);
        set => SetValue(BlurContentProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceLoadingOverlay);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty)
            PseudoClasses.Set(":active", IsActive);
    }

    private void BlockInput(object? sender, RoutedEventArgs e)
    {
        if (IsActive)
            e.Handled = true;
    }
}
