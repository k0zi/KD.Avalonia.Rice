using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Item of a <see cref="RiceSideMenu"/>: <see cref="Icon"/> + text (<c>Header</c>).
/// The icon can be an <c>IImage</c> (e.g. <c>Bitmap</c>, <c>SvgImage</c>), a <c>MaterialIconKind</c>,
/// a string (e.g. an emoji) or any control.
/// Nested items are sub items: the item shows a chevron and expands / collapses (<c>IsExpanded</c>) on click.
/// Inside the item list it is selectable; in the header/footer it works as a button (<see cref="Click"/>, <see cref="Command"/>).
/// </summary>
public class RiceSideMenuItem : TreeViewItem
{
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<RiceSideMenuItem, object?>(nameof(Icon));

    public static readonly StyledProperty<bool> IsCollapsedProperty =
        RiceSideMenu.IsCollapsedProperty.AddOwner<RiceSideMenuItem>();

    /// <summary>Indentation of the sub items per level.</summary>
    public static readonly StyledProperty<double> IndentProperty =
        AvaloniaProperty.Register<RiceSideMenuItem, double>(nameof(Indent), 16);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<RiceSideMenuItem, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<RiceSideMenuItem, object?>(nameof(CommandParameter));

    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<RiceSideMenuItem, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    private Control? _headerPart;
    private Control? _headerContent;
    private bool _hasSelectedChild;

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

    public double Indent
    {
        get => GetValue(IndentProperty);
        set => SetValue(IndentProperty, value);
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

    public bool HasChildren => ItemCount > 0;

    /// <summary>True when one of the sub items (at any depth) is selected (<c>:childselected</c> pseudo class).</summary>
    public bool HasSelectedChild
    {
        get => _hasSelectedChild;
        internal set
        {
            _hasSelectedChild = value;
            PseudoClasses.Set(":childselected", value);
        }
    }

    public event EventHandler<RoutedEventArgs>? Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceSideMenuItem);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _headerPart = e.NameScope.Find<Control>("PART_Header");
        _headerContent = e.NameScope.Find<Control>("PART_HeaderContent");
        UpdateIndent();
    }

    // Only a click on the item's own row counts, not one on its sub items.
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        var target = _headerPart ?? this;
        var clicked = e.InitialPressMouseButton == MouseButton.Left
                      && new Rect(target.Bounds.Size).Contains(e.GetPosition(target));
        base.OnPointerReleased(e);
        if (clicked)
        {
            OnClick();
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var activate = !e.Handled && e.Key is Key.Enter or Key.Space;
        base.OnKeyDown(e);
        if (activate)
        {
            OnClick();
            e.Handled = true;
        }
    }

    // A single click already toggles the sub items (see RiceSideMenu), a double click would toggle them back.
    protected override void OnHeaderDoubleTapped(TappedEventArgs e)
    {
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
        else if (change.Property == IsExpandedProperty)
            PseudoClasses.Set(":expanded", IsExpanded);
        else if (change.Property == ItemCountProperty)
            PseudoClasses.Set(":haschildren", HasChildren);
        else if (change.Property == LevelProperty || change.Property == IndentProperty)
            UpdateIndent();

        if (change.Property == IsCollapsedProperty || change.Property == HeaderProperty)
            UpdateCollapsedTip();
    }

    // Depth below the top level items. Not Level: that also counts the footer's panels.
    private void UpdateIndent()
    {
        if (_headerContent is null)
            return;
        var depth = 0;
        for (var parent = Parent as RiceSideMenuItem; parent is not null; parent = parent.Parent as RiceSideMenuItem)
            depth++;
        _headerContent.Margin = new Thickness(depth * Indent, 0, 0, 0);
    }

    // Collapsed, the text is not visible, so it is shown as tool tip. Style priority keeps a user set tool tip.
    private void UpdateCollapsedTip()
    {
        if (IsCollapsed && Header is string text)
            SetValue(ToolTip.TipProperty, text, BindingPriority.Style);
        else
            SetValue(ToolTip.TipProperty, AvaloniaProperty.UnsetValue, BindingPriority.Style);
    }
}
