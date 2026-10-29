using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.VisualTree;

namespace KD.Avalonia.Rice.Controls;

/// <summary>How much room an item takes in a <see cref="RiceRibbonGroup"/>.</summary>
public enum RiceRibbonItemSize
{
    /// <summary>Full height of the group: a big icon above the label.</summary>
    Large,
    /// <summary>One of three rows: a small icon and the label.</summary>
    Medium,
    /// <summary>One of three rows: only the small icon.</summary>
    Small,
}

/// <summary>
/// Office like ribbon: tabs (<see cref="RiceRibbonTab"/>) of command groups (<see cref="RiceRibbonGroup"/>) that hold
/// <see cref="RiceRibbonButton"/>s and any other control. When the ribbon is too narrow the groups collapse into a
/// button from the right, which opens the group in a flyout. Minimized (<see cref="IsMinimized"/>; double click on a tab,
/// the chevron or Ctrl+F1) only the tabs show, and a click on a tab opens its groups over the content below.
/// Alt or F10 shows key tips (<see cref="KeyTipProperty"/>): letters that select a tab and run its commands.
/// A <see cref="TabControl"/> underneath: bind <c>SelectedItem</c> / <c>SelectedIndex</c> as usual.
/// </summary>
public class RiceRibbon : TabControl
{
    /// <summary>Size of an item in a group (attached; <see cref="RiceRibbonButton"/>s default to Large, other controls to Medium).</summary>
    public static readonly AttachedProperty<RiceRibbonItemSize> SizeProperty =
        AvaloniaProperty.RegisterAttached<RiceRibbon, Control, RiceRibbonItemSize>("Size", RiceRibbonItemSize.Medium);

    public static readonly StyledProperty<bool> IsMinimizedProperty =
        AvaloniaProperty.Register<RiceRibbon, bool>(nameof(IsMinimized), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowMinimizeButtonProperty =
        AvaloniaProperty.Register<RiceRibbon, bool>(nameof(ShowMinimizeButton), true);

    /// <summary>Content of the accent button before the tabs (e.g. "File"); the button is hidden while it is null.</summary>
    public static readonly StyledProperty<object?> ApplicationButtonContentProperty =
        AvaloniaProperty.Register<RiceRibbon, object?>(nameof(ApplicationButtonContent));

    public static readonly StyledProperty<ICommand?> ApplicationButtonCommandProperty =
        AvaloniaProperty.Register<RiceRibbon, ICommand?>(nameof(ApplicationButtonCommand));

    /// <summary>Flyout (e.g. a <c>MenuFlyout</c>) the application button opens.</summary>
    public static readonly StyledProperty<FlyoutBase?> ApplicationMenuProperty =
        AvaloniaProperty.Register<RiceRibbon, FlyoutBase?>(nameof(ApplicationMenu));

    /// <summary>
    /// Key tip of a tab, a command or a group (its collapsed button) shown after Alt (attached). Letters or digits,
    /// e.g. "H" or "FP"; when not set, one is chosen from the label.
    /// </summary>
    public static readonly AttachedProperty<string?> KeyTipProperty =
        AvaloniaProperty.RegisterAttached<RiceRibbon, Control, string?>("KeyTip");

    /// <summary>Alt or F10 shows key tips to run the commands from the keyboard. Default true.</summary>
    public static readonly StyledProperty<bool> IsKeyTipsEnabledProperty =
        AvaloniaProperty.Register<RiceRibbon, bool>(nameof(IsKeyTipsEnabled), true);

    public static readonly StyledProperty<string?> ApplicationButtonKeyTipProperty =
        AvaloniaProperty.Register<RiceRibbon, string?>(nameof(ApplicationButtonKeyTip));

    /// <summary>Content at the right end of the tab strip, before the minimize button (e.g. a search box).</summary>
    public static readonly StyledProperty<object?> TabStripFooterProperty =
        AvaloniaProperty.Register<RiceRibbon, object?>(nameof(TabStripFooter));

    // A click on the tab whose popup was just light dismissed by the same click must not open it again.
    private const long ReopenGuardMilliseconds = 300;

    private Decorator? _contentHost;
    private Decorator? _popupHost;
    private Control? _contentBorder;
    private Popup? _popup;
    private Button? _minimizeButton;
    private readonly RiceRibbonKeyTips _keyTips;
    private TopLevel? _topLevel;
    private object? _popupClosedItem;
    private long _popupClosedAt;

    static RiceRibbon()
    {
        SelectionModeProperty.OverrideDefaultValue<RiceRibbon>(SelectionMode.Single | SelectionMode.AlwaysSelected);
    }

    public RiceRibbon()
    {
        AddHandler(PointerPressedEvent, OnTabPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        _keyTips = new RiceRibbonKeyTips(this);
    }

    public static RiceRibbonItemSize GetSize(Control control) => control.GetValue(SizeProperty);

    public static void SetSize(Control control, RiceRibbonItemSize value) => control.SetValue(SizeProperty, value);

    public static string? GetKeyTip(Control control) => control.GetValue(KeyTipProperty);

    public static void SetKeyTip(Control control, string? value) => control.SetValue(KeyTipProperty, value);

    public bool IsKeyTipsEnabled
    {
        get => GetValue(IsKeyTipsEnabledProperty);
        set => SetValue(IsKeyTipsEnabledProperty, value);
    }

    /// <summary>Key tip of the application button; when not set, one is chosen from its content.</summary>
    public string? ApplicationButtonKeyTip
    {
        get => GetValue(ApplicationButtonKeyTipProperty);
        set => SetValue(ApplicationButtonKeyTipProperty, value);
    }

    /// <summary>True while key tips are shown.</summary>
    public bool AreKeyTipsShown => _keyTips.IsActive;

    /// <summary>Only the tabs show; a click on a tab opens its groups in a popup.</summary>
    public bool IsMinimized
    {
        get => GetValue(IsMinimizedProperty);
        set => SetValue(IsMinimizedProperty, value);
    }

    /// <summary>Shows the chevron at the right end of the tab strip that minimizes / pins the ribbon. Default true.</summary>
    public bool ShowMinimizeButton
    {
        get => GetValue(ShowMinimizeButtonProperty);
        set => SetValue(ShowMinimizeButtonProperty, value);
    }

    public object? ApplicationButtonContent
    {
        get => GetValue(ApplicationButtonContentProperty);
        set => SetValue(ApplicationButtonContentProperty, value);
    }

    public ICommand? ApplicationButtonCommand
    {
        get => GetValue(ApplicationButtonCommandProperty);
        set => SetValue(ApplicationButtonCommandProperty, value);
    }

    public FlyoutBase? ApplicationMenu
    {
        get => GetValue(ApplicationMenuProperty);
        set => SetValue(ApplicationMenuProperty, value);
    }

    public object? TabStripFooter
    {
        get => GetValue(TabStripFooterProperty);
        set => SetValue(TabStripFooterProperty, value);
    }

    /// <summary>True while the groups of a minimized ribbon are open in the popup.</summary>
    public bool IsPopupOpen => _popup?.IsOpen == true;

    protected override Type StyleKeyOverride => typeof(RiceRibbon);

    public void ToggleMinimized() => IsMinimized = !IsMinimized;

    /// <summary>Shows the key tips of the tabs, as Alt does.</summary>
    public void ShowKeyTips() => _keyTips.Show();

    public void HideKeyTips() => _keyTips.Hide();

    internal Button? ApplicationButton { get; private set; }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new RiceRibbonTab();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<RiceRibbonTab>(item, out recycleKey);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ClosePopup();
        _minimizeButton?.Click -= OnMinimizeButtonClick;
        _popup?.Closed -= OnPopupClosed;
        _contentBorder?.RemoveHandler(Button.ClickEvent, OnContentClick);

        _contentHost = e.NameScope.Find<Decorator>("PART_ContentHost");
        _popupHost = e.NameScope.Find<Decorator>("PART_PopupHost");
        _contentBorder = e.NameScope.Find<Control>("PART_ContentBorder");
        _popup = e.NameScope.Find<Popup>("PART_Popup");
        _minimizeButton = e.NameScope.Find<Button>("PART_MinimizeButton");
        ApplicationButton = e.NameScope.Find<Button>("PART_ApplicationButton");

        _minimizeButton?.Click += OnMinimizeButtonClick;
        _popup?.Closed += OnPopupClosed;
        _contentBorder?.AddHandler(Button.ClickEvent, OnContentClick, RoutingStrategies.Bubble, handledEventsToo: true);
        UpdateMinimized();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsMinimizedProperty)
            UpdateMinimized();
        else if (change.Property == IsKeyTipsEnabledProperty && !IsKeyTipsEnabled)
            _keyTips.Hide();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Bubble);
        if (_topLevel is not null)
            _keyTips.Attach(_topLevel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _topLevel?.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _keyTips.Detach();
        _topLevel = null;
        ClosePopup();
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !IsEffectivelyVisible || !IsEffectivelyEnabled)
            return;
        if (e is { Key: Key.F1, KeyModifiers: KeyModifiers.Control })
        {
            ToggleMinimized();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && IsPopupOpen)
        {
            ClosePopup();
            e.Handled = true;
        }
    }

    private void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            || (e.Source as Visual)?.FindAncestorOfType<RiceRibbonTab>(includeSelf: true) is not { } tab
            || ItemsControlFromItemContainer(tab) != this)
            return;
        if (e.ClickCount == 2)
        {
            ToggleMinimized();
            return;
        }
        if (!IsMinimized)
            return;
        var item = ItemFromContainer(tab);
        if (IsPopupOpen && SelectedItem == item)
            ClosePopup();
        else if (!IsPopupOpen && !(item == _popupClosedItem && Environment.TickCount64 - _popupClosedAt < ReopenGuardMilliseconds))
            OpenPopup();
    }

    // A command run from the popup closes it, like a menu; a button that opens a flyout keeps it open.
    private void OnContentClick(object? sender, RoutedEventArgs e)
    {
        if (IsPopupOpen && e.Source is Button { Flyout: null })
            ClosePopup();
    }

    private void OnMinimizeButtonClick(object? sender, RoutedEventArgs e) => ToggleMinimized();

    internal void OpenPopup()
    {
        if (_popup is null || _popupHost is null || _contentHost is null || _contentBorder is null)
            return;
        _contentHost.Child = null;
        _popupHost.Child = _contentBorder;
        _popup.Width = Bounds.Width;
        _popup.IsOpen = true;
    }

    internal void ClosePopup()
    {
        if (_popup is { IsOpen: true })
            _popup.IsOpen = false;
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        _popupClosedItem = SelectedItem;
        _popupClosedAt = Environment.TickCount64;
        if (_popupHost is not null && _contentHost is not null && _popupHost.Child == _contentBorder)
        {
            _popupHost.Child = null;
            _contentHost.Child = _contentBorder;
        }
    }

    private void UpdateMinimized()
    {
        PseudoClasses.Set(":minimized", IsMinimized);
        if (!IsMinimized)
            ClosePopup();
        if (_minimizeButton is not null)
            ToolTip.SetTip(_minimizeButton, IsMinimized ? RiceStrings.RibbonExpand : RiceStrings.RibbonCollapse);
    }
}

/// <summary>
/// One tab of a <see cref="RiceRibbon"/>: its <see cref="Groups"/> are laid out from the left and collapse from the right
/// when there is not enough room. A contextual tab (<see cref="IsContextual"/>, e.g. "Table design" while a table is
/// selected) has an accent colored header; show and hide it with <c>IsVisible</c>.
/// </summary>
public class RiceRibbonTab : TabItem
{
    public static readonly StyledProperty<bool> IsContextualProperty =
        AvaloniaProperty.Register<RiceRibbonTab, bool>(nameof(IsContextual));

    private readonly RiceRibbonGroupsPanel _groupsPanel = new();

    public RiceRibbonTab()
    {
        Content = _groupsPanel;
    }

    /// <summary>The <see cref="RiceRibbonGroup"/>s of the tab (other controls are allowed too, they never collapse).</summary>
    [Content]
    public global::Avalonia.Controls.Controls Groups => _groupsPanel.Children;

    public bool IsContextual
    {
        get => GetValue(IsContextualProperty);
        set => SetValue(IsContextualProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceRibbonTab);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsContextualProperty)
            PseudoClasses.Set(":contextual", IsContextual);
    }
}
