using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Side menu with icon + text items, an optional header and footer and a collapse button.
/// Collapsed, only the icons are visible, the items keep working (text shown as tool tip).
/// Selection works like a <see cref="ListBox"/>: bind <c>SelectedItem</c> / <c>SelectedIndex</c>.
/// </summary>
public class RiceSideMenu : ListBox
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
        AvaloniaProperty.Register<RiceSideMenu, object?>(nameof(Header));

    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty =
        AvaloniaProperty.Register<RiceSideMenu, IDataTemplate?>(nameof(HeaderTemplate));

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<RiceSideMenu, object?>(nameof(Footer));

    public static readonly StyledProperty<IDataTemplate?> FooterTemplateProperty =
        AvaloniaProperty.Register<RiceSideMenu, IDataTemplate?>(nameof(FooterTemplate));

    public static readonly RoutedEvent<RoutedEventArgs> CollapsedChangedEvent =
        RoutedEvent.Register<RiceSideMenu, RoutedEventArgs>(nameof(CollapsedChanged), RoutingStrategies.Bubble);

    private Button? _collapseButton;

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

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new RiceSideMenuItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<RiceSideMenuItem>(item, out recycleKey);

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
    }

    private void OnCollapseButtonClick(object? sender, RoutedEventArgs e) => ToggleCollapsed();

    private void UpdateCollapseButtonTip()
    {
        if (_collapseButton is not null)
            ToolTip.SetTip(_collapseButton, IsCollapsed ? RiceStrings.ExpandMenu : RiceStrings.CollapseMenu);
    }
}
