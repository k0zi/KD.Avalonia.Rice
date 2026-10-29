using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Side menu with icon + text items, an optional header and footer and a collapse button.
/// Collapsed, only the icons are visible, the items keep working (text shown as tool tip).
/// Items can have sub items (nested <see cref="RiceSideMenuItem"/>s, or <c>ItemsSource</c> with a <c>TreeDataTemplate</c>):
/// clicking such a parent item expands / collapses it instead of selecting it.
/// Selection works like a <see cref="TreeView"/>: bind <c>SelectedItem</c>, handle <c>SelectionChanged</c>.
/// </summary>
public class RiceSideMenu : TreeView
{
    /// <summary>Inherited by the items, the header and the footer, so their content can follow it too.</summary>
    public static readonly StyledProperty<bool> IsCollapsedProperty =
        AvaloniaProperty.Register<RiceSideMenu, bool>(nameof(IsCollapsed), inherits: true,
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> ExpandedWidthProperty =
        AvaloniaProperty.Register<RiceSideMenu, double>(nameof(ExpandedWidth), 240);

    public static readonly StyledProperty<double> CollapsedWidthProperty =
        AvaloniaProperty.Register<RiceSideMenu, double>(nameof(CollapsedWidth), 56);

    public static readonly StyledProperty<bool> ShowCollapseButtonProperty =
        AvaloniaProperty.Register<RiceSideMenu, bool>(nameof(ShowCollapseButton), true);

    public static readonly StyledProperty<object?> HeaderProperty =
        HeaderedContentControl.HeaderProperty.AddOwner<RiceSideMenu>();

    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty =
        HeaderedContentControl.HeaderTemplateProperty.AddOwner<RiceSideMenu>();

    public static readonly StyledProperty<object?> FooterProperty =
        RiceCard.FooterProperty.AddOwner<RiceSideMenu>();

    public static readonly StyledProperty<IDataTemplate?> FooterTemplateProperty =
        RiceCard.FooterTemplateProperty.AddOwner<RiceSideMenu>();

    public static readonly RoutedEvent<RoutedEventArgs> CollapsedChangedEvent =
        RoutedEvent.Register<RiceSideMenu, RoutedEventArgs>(nameof(CollapsedChanged), RoutingStrategies.Bubble);

    private Button? _collapseButton;
    private readonly List<RiceSideMenuItem> _selectionPath = [];

    public bool IsCollapsed
    {
        get => GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    public double ExpandedWidth
    {
        get => GetValue(ExpandedWidthProperty);
        set => SetValue(ExpandedWidthProperty, value);
    }

    public double CollapsedWidth
    {
        get => GetValue(CollapsedWidthProperty);
        set => SetValue(CollapsedWidthProperty, value);
    }

    public bool ShowCollapseButton
    {
        get => GetValue(ShowCollapseButtonProperty);
        set => SetValue(ShowCollapseButtonProperty, value);
    }

    /// <summary>Optional content above the items (e.g. logo and app name).</summary>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public IDataTemplate? HeaderTemplate
    {
        get => GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    /// <summary>Optional content below the items, e.g. <see cref="RiceSideMenuItem"/>s with a <c>Command</c>.</summary>
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

    public event EventHandler<RoutedEventArgs>? CollapsedChanged
    {
        add => AddHandler(CollapsedChangedEvent, value);
        remove => RemoveHandler(CollapsedChangedEvent, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceSideMenu);

    public void ToggleCollapsed() => IsCollapsed = !IsCollapsed;

    /// <summary>
    /// One command per item that can be selected (parent items only group them) and per footer item, for the
    /// <see cref="RiceCommandPalette"/>. Sub items show their parent as description. Data items (<c>ItemsSource</c>)
    /// are listed on the top level only, by their <c>ToString()</c>.
    /// </summary>
    public IEnumerable<RiceCommandItem> GetNavigationCommands()
    {
        var commands = new List<RiceCommandItem>();
        AddItems(Items, null);
        foreach (var footerItem in FooterItems(Footer))
        {
            if (Title(footerItem.Header) is { } title)
                commands.Add(NavigationCommand(title, footerItem.Icon, null, footerItem.PerformClick));
        }
        return commands;

        void AddItems(IEnumerable<object?> items, string? parent)
        {
            foreach (var item in items)
            {
                if (item is RiceSideMenuItem menuItem)
                {
                    if (Title(menuItem.Header) is not { } title)
                        continue;
                    if (menuItem.HasChildren)
                        AddItems(menuItem.Items, parent is null ? title : $"{parent} › {title}");
                    else
                        commands.Add(NavigationCommand(title, menuItem.Icon, parent, () => SelectedItem = menuItem));
                }
                else if (parent is null && item?.ToString() is { Length: > 0 } text)
                {
                    commands.Add(NavigationCommand(text, null, null, () => SelectedItem = item));
                }
            }
        }

        static string? Title(object? header) => header is string text && text.Length > 0 ? text : null;

        // A control can have one parent only: an Image icon is shared by its source, other control icons are left out.
        static RiceCommandItem NavigationCommand(string title, object? icon, string? parent, Action execute) => new(title, execute)
        {
            Icon = icon switch
            {
                Image image => image.Source,
                Control => null,
                _ => icon,
            },
            Description = parent,
            Category = RiceStrings.CommandPaletteNavigation,
        };

        static IEnumerable<RiceSideMenuItem> FooterItems(object? footer) => footer switch
        {
            RiceSideMenuItem item => [item],
            Panel panel => panel.Children.OfType<RiceSideMenuItem>(),
            _ => [],
        };
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new RiceSideMenuItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<RiceSideMenuItem>(item, out recycleKey);

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        // Containers of data items are created later, the selection may have been set before.
        if (SelectedItem is not null && _selectionPath.Count == 0)
            UpdateSelectionPath();
    }

    // Parent items are not selected, they expand / collapse. Collapsed, the menu opens first to show the sub items.
    public override bool UpdateSelectionFromEvent(Control container, RoutedEventArgs eventArgs)
    {
        if (container is not RiceSideMenuItem { HasChildren: true } parent || TreeItemFromContainer(parent) is null)
            return base.UpdateSelectionFromEvent(container, eventArgs);

        var trigger = !eventArgs.Handled && eventArgs switch
        {
            PointerEventArgs pointer => ShouldTriggerSelection(container, pointer),
            KeyEventArgs key => ShouldTriggerSelection(container, key),
            _ => false,
        };
        if (!trigger)
            return false;

        if (IsCollapsed)
        {
            IsCollapsed = false;
            parent.SetCurrentValue(TreeViewItem.IsExpandedProperty, true);
        }
        else
        {
            parent.SetCurrentValue(TreeViewItem.IsExpandedProperty, !parent.IsExpanded);
        }
        eventArgs.Handled = true;
        return true;
    }

    // Arrow keys move the focus and the selection together; parent items only get the focus.
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        if (e.NavigationMethod == NavigationMethod.Directional
            && GetContainerFromEventSource(e.Source!) is RiceSideMenuItem { HasChildren: true })
            return;
        base.OnGotFocus(e);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _collapseButton?.Click -= OnCollapseButtonClick;
        _collapseButton = e.NameScope.Find<Button>("PART_CollapseButton");
        _collapseButton?.Click += OnCollapseButtonClick;
        UpdateCollapseButtonTip();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCollapsedProperty)
        {
            PseudoClasses.Set(":collapsed", IsCollapsed);
            UpdateCollapseButtonTip();
            RaiseEvent(new RoutedEventArgs(CollapsedChangedEvent));
        }
        else if (change.Property == SelectedItemProperty)
        {
            UpdateSelectionPath();
        }
    }

    // Marks the parents of the selected item (they show the indicator while closed) and expands them.
    private void UpdateSelectionPath()
    {
        foreach (var item in _selectionPath)
            item.HasSelectedChild = false;
        _selectionPath.Clear();

        var selected = SelectedItem switch
        {
            null => null,
            Control control when control.Parent is not null => control,
            var item => TreeContainerFromItem(item),
        };
        for (var parent = selected?.Parent as RiceSideMenuItem; parent is not null; parent = parent.Parent as RiceSideMenuItem)
        {
            parent.HasSelectedChild = true;
            parent.SetCurrentValue(TreeViewItem.IsExpandedProperty, true);
            _selectionPath.Add(parent);
        }
    }

    private void OnCollapseButtonClick(object? sender, RoutedEventArgs e) => ToggleCollapsed();

    private void UpdateCollapseButtonTip()
    {
        if (_collapseButton is not null)
            ToolTip.SetTip(_collapseButton, IsCollapsed ? RiceStrings.ExpandMenu : RiceStrings.CollapseMenu);
    }
}
