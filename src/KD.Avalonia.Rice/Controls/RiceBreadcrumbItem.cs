using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// One segment of a <see cref="RiceBreadcrumb"/>: optional <see cref="Icon"/> and the content, with a chevron in front
/// of every segment but the first. Pseudo classes: <c>:first</c>, <c>:last</c> (the current location, not clickable).
/// </summary>
public class RiceBreadcrumbItem : ContentControl
{
    public static readonly StyledProperty<object?> IconProperty =
        RiceCard.IconProperty.AddOwner<RiceBreadcrumbItem>();

    private bool _isLast;

    public RiceBreadcrumbItem() => Focusable = true;

    /// <summary>An <c>IImage</c>, a <c>MaterialIconKind</c>, a string (e.g. an emoji) or any control.</summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceBreadcrumbItem);

    internal void SetPosition(bool isFirst, bool isLast)
    {
        _isLast = isLast;
        PseudoClasses.Set(":first", isFirst);
        PseudoClasses.Set(":last", isLast);
        Cursor = isLast ? null : new Cursor(StandardCursorType.Hand);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty)
            PseudoClasses.Set(":has-icon", Icon is not null);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!e.Handled && e.InitialPressMouseButton == MouseButton.Left && IsPointerOver)
            Click(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key is Key.Enter or Key.Space)
            Click(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);

    private void Click(global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isLast)
            return;
        e.Handled = true;
        (ItemsControl.ItemsControlFromItemContainer(this) as RiceBreadcrumb)?.OnItemClicked(this);
    }
}
