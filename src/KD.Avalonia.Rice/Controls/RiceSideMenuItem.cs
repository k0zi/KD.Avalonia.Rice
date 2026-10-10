using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Item of a <see cref="RiceSideMenu"/>: <see cref="Icon"/> + text (<c>Content</c>).
/// The icon can be an <c>IImage</c> (e.g. <c>Bitmap</c>, <c>SvgImage</c>), a <c>MaterialIconKind</c>,
/// a string (e.g. an emoji) or any control.
/// Inside the item list it is selectable; in the header/footer it works as a button (<see cref="Click"/>, <see cref="Command"/>).
/// </summary>
public class RiceSideMenuItem : ListBoxItem
{
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<RiceSideMenuItem, object?>(nameof(Icon));

    public static readonly StyledProperty<bool> IsCollapsedProperty =
        RiceSideMenu.IsCollapsedProperty.AddOwner<RiceSideMenuItem>();

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<RiceSideMenuItem, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<RiceSideMenuItem, object?>(nameof(CommandParameter));

    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<RiceSideMenuItem, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public bool IsCollapsed
    {
        get => GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    /// <summary>Executed when the item is clicked or activated with Enter/Space.</summary>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public event EventHandler<RoutedEventArgs>? Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceSideMenuItem);

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        var clicked = e.InitialPressMouseButton == MouseButton.Left
                      && new Rect(Bounds.Size).Contains(e.GetPosition(this));
        base.OnPointerReleased(e);
        if (clicked)
            OnClick();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key is Key.Enter or Key.Space)
        {
            OnClick();
            e.Handled = true;
        }
    }

    protected virtual void OnClick()
    {
        RaiseEvent(new RoutedEventArgs(ClickEvent));
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCollapsedProperty)
            PseudoClasses.Set(":collapsed", IsCollapsed);
        if (change.Property == IsCollapsedProperty || change.Property == ContentProperty)
            UpdateCollapsedTip();
    }

    // Collapsed, the text is not visible, so it is shown as tool tip. Style priority keeps a user set tool tip.
    private void UpdateCollapsedTip()
    {
        if (IsCollapsed && Content is string text)
            SetValue(ToolTip.TipProperty, text, BindingPriority.Style);
        else
            SetValue(ToolTip.TipProperty, AvaloniaProperty.UnsetValue, BindingPriority.Style);
    }
}
