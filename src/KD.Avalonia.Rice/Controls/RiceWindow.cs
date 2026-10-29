using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using KD.Avalonia.Rice.Notifications;
using KD.Avalonia.Rice.Options;
using KD.Avalonia.Rice.Theming;
using Avalonia.VisualTree;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Frameless window with a custom title bar: icon and title on the left,
/// light/dark toggle, optional theme settings and about buttons and the window controls on the right.
/// </summary>
public class RiceWindow : Window
{
    public static readonly StyledProperty<bool> ShowTitleBarProperty =
        AvaloniaProperty.Register<RiceWindow, bool>(nameof(ShowTitleBar), true);

    public static readonly StyledProperty<bool> ShowThemeToggleProperty =
        AvaloniaProperty.Register<RiceWindow, bool>(nameof(ShowThemeToggle), true);

    public static readonly StyledProperty<bool> ShowThemeSettingsButtonProperty =
        AvaloniaProperty.Register<RiceWindow, bool>(nameof(ShowThemeSettingsButton));

    public static readonly StyledProperty<bool> ShowAboutButtonProperty =
        AvaloniaProperty.Register<RiceWindow, bool>(nameof(ShowAboutButton));

    public static readonly StyledProperty<object?> TitleBarContentProperty =
        AvaloniaProperty.Register<RiceWindow, object?>(nameof(TitleBarContent));

    public static readonly StyledProperty<RiceAppInfo> AppInfoProperty =
        AvaloniaProperty.Register<RiceWindow, RiceAppInfo>(nameof(AppInfo));

    public static readonly StyledProperty<ToastPosition> ToastPositionProperty =
        RiceToastHost.PositionProperty.AddOwner<RiceWindow>();

    /// <summary>Opens the command palette; null turns the shortcut off. Default Ctrl+Shift+P.</summary>
    public static readonly StyledProperty<KeyGesture?> CommandPaletteGestureProperty =
        AvaloniaProperty.Register<RiceWindow, KeyGesture?>(nameof(CommandPaletteGesture),
            new KeyGesture(Key.P, KeyModifiers.Control | KeyModifiers.Shift));

    public static readonly StyledProperty<bool> IncludeNavigationCommandsProperty =
        AvaloniaProperty.Register<RiceWindow, bool>(nameof(IncludeNavigationCommands), true);

    public static readonly RoutedEvent<RoutedEventArgs> AboutRequestedEvent =
        RoutedEvent.Register<RiceWindow, RoutedEventArgs>(nameof(AboutRequested), RoutingStrategies.Direct);

    private static readonly (string Name, WindowEdge Edge)[] ResizeGrips =
    [
        ("PART_ResizeN", WindowEdge.North), ("PART_ResizeS", WindowEdge.South),
        ("PART_ResizeW", WindowEdge.West), ("PART_ResizeE", WindowEdge.East),
        ("PART_ResizeNW", WindowEdge.NorthWest), ("PART_ResizeNE", WindowEdge.NorthEast),
        ("PART_ResizeSW", WindowEdge.SouthWest), ("PART_ResizeSE", WindowEdge.SouthEast),
    ];

    public RiceWindow()
    {
        AppInfo = ThemeManager.Instance.Options.AppInfo;
        Title = AppInfo.Name;
        if (AppInfo.Icon is { } icon)
            Icon = new WindowIcon(icon);
        // The platform may report its transparency level up front without a change notification.
        UpdateRoundedPseudoClass();
        // Tunnel: the shortcuts work wherever the focus is, even in a text box.
        AddHandler(KeyDownEvent, OnShortcutKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>Hides the whole title bar, e.g. for dialogs that only show their content.</summary>
    public bool ShowTitleBar
    {
        get => GetValue(ShowTitleBarProperty);
        set => SetValue(ShowTitleBarProperty, value);
    }

    public bool ShowThemeToggle
    {
        get => GetValue(ShowThemeToggleProperty);
        set => SetValue(ShowThemeToggleProperty, value);
    }

    /// <summary>Shows the title bar button that opens the <see cref="ThemeDialog"/>.</summary>
    public bool ShowThemeSettingsButton
    {
        get => GetValue(ShowThemeSettingsButtonProperty);
        set => SetValue(ShowThemeSettingsButtonProperty, value);
    }

    public bool ShowAboutButton
    {
        get => GetValue(ShowAboutButtonProperty);
        set => SetValue(ShowAboutButtonProperty, value);
    }

    /// <summary>Extra content placed in the title bar between the title and the buttons.</summary>
    public object? TitleBarContent
    {
        get => GetValue(TitleBarContentProperty);
        set => SetValue(TitleBarContentProperty, value);
    }

    public RiceAppInfo AppInfo
    {
        get => GetValue(AppInfoProperty);
        set => SetValue(AppInfoProperty, value);
    }

    /// <summary>Where <see cref="RiceToast"/> stacks the toasts of this window, below the title bar.</summary>
    public ToastPosition ToastPosition
    {
        get => GetValue(ToastPositionProperty);
        set => SetValue(ToastPositionProperty, value);
    }

    /// <summary>
    /// The commands of the command palette (<see cref="ShowCommandPalette"/>). Their <see cref="RiceCommandItem.Gesture"/>s
    /// work in the whole window, also while the palette is closed.
    /// </summary>
    public AvaloniaList<RiceCommandItem> Commands { get; } = [];

    /// <summary>
    /// Extra modes of the command palette, chosen by a prefix. Built in: "&gt;" searches only <see cref="Commands"/>,
    /// "@" only the side menu items (with <see cref="IncludeNavigationCommands"/>).
    /// </summary>
    public AvaloniaList<RiceCommandProvider> CommandProviders { get; } = [];

    public KeyGesture? CommandPaletteGesture
    {
        get => GetValue(CommandPaletteGestureProperty);
        set => SetValue(CommandPaletteGestureProperty, value);
    }

    /// <summary>Lists the items of the window's <see cref="RiceSideMenu"/> in the command palette too. Default true.</summary>
    public bool IncludeNavigationCommands
    {
        get => GetValue(IncludeNavigationCommandsProperty);
        set => SetValue(IncludeNavigationCommandsProperty, value);
    }

    /// <summary>The built-in toast host, once the template is applied.</summary>
    public RiceToastHost? ToastHost { get; private set; }

    /// <summary>Raised before the built-in about dialog opens; set <c>Handled</c> to replace it.</summary>
    public event EventHandler<RoutedEventArgs>? AboutRequested
    {
        add => AddHandler(AboutRequestedEvent, value);
        remove => RemoveHandler(AboutRequestedEvent, value);
    }

    // Subclasses (e.g. MainWindow) must still pick up the RiceWindow control theme.
    protected override Type StyleKeyOverride => typeof(RiceWindow);

    /// <summary>Opens the theme selector as a modal dialog of this window.</summary>
    public Task ShowThemeDialog() => new ThemeDialog().ShowDialog(this);

    /// <summary>
    /// Opens the command palette over this window with <see cref="Commands"/> and, with
    /// <see cref="IncludeNavigationCommands"/>, the items of its side menu.
    /// </summary>
    public void ShowCommandPalette(string? query = null)
    {
        var navigation = GetNavigationCommands().ToList();
        RiceCommandPalette.Show(GetPaletteCommands(navigation), GetPaletteProviders(navigation), this, query);
    }

    /// <summary>What the command palette lists without a prefix: <see cref="Commands"/>, then the side menu items.</summary>
    protected virtual IEnumerable<RiceCommandItem> GetPaletteCommands(IReadOnlyList<RiceCommandItem> navigation) =>
        Commands.Concat(navigation);

    /// <summary>The modes of the command palette: "&gt;" commands, "@" pages, then <see cref="CommandProviders"/>.</summary>
    protected virtual IEnumerable<RiceCommandProvider> GetPaletteProviders(IReadOnlyList<RiceCommandItem> navigation)
    {
        if (Commands.Count > 0)
            yield return new RiceCommandProvider(">", RiceStrings.CommandPaletteCommandsMode, Commands.ToList())
            {
                Icon = Material.Icons.MaterialIconKind.ConsoleLine,
            };
        if (navigation.Count > 0)
            yield return new RiceCommandProvider("@", RiceStrings.CommandPaletteNavigationMode, navigation)
            {
                Icon = Material.Icons.MaterialIconKind.Compass,
            };
        foreach (var provider in CommandProviders)
            yield return provider;
    }

    private IEnumerable<RiceCommandItem> GetNavigationCommands() => IncludeNavigationCommands
        ? this.GetVisualDescendants().OfType<RiceSideMenu>().SelectMany(menu => menu.GetNavigationCommands())
        : [];

    private void OnShortcutKeyDown(object? sender, KeyEventArgs e)
    {
        if (CommandPaletteGesture?.Matches(e) == true)
        {
            if (RiceCommandPalette.IsShown(this))
                RiceCommandPalette.Hide(this);
            else
                ShowCommandPalette();
            e.Handled = true;
        }
        else if (!RiceCommandPalette.IsShown(this)
                 && Commands.FirstOrDefault(c => c.Gesture?.Matches(e) == true && c.CanInvoke()) is { } command)
        {
            command.Invoke();
            e.Handled = true;
        }
    }

    public async void RequestAbout()
    {
        var args = new RoutedEventArgs(AboutRequestedEvent);
        RaiseEvent(args);
        if (!args.Handled)
            await new AboutDialog(AppInfo).ShowDialog(this);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CornerRadiusProperty)
        {
            // Rounded corners need a transparent window; square ones keep the cheaper opaque window.
            TransparencyLevelHint = CornerRadius == default ? [] : [WindowTransparencyLevel.Transparent];
        }
        else if (change.Property == ActualTransparencyLevelProperty)
        {
            UpdateRoundedPseudoClass();
        }
    }

    // Without a compositor (e.g. plain X11) the corners would be black, so fall back to square ones.
    private void UpdateRoundedPseudoClass() =>
        PseudoClasses.Set(":rounded", ActualTransparencyLevel != WindowTransparencyLevel.None);

    public void ToggleMaximized() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ToastHost = e.NameScope.Find<RiceToastHost>("PART_ToastHost");
        foreach (var (name, edge) in ResizeGrips)
        {
            if (e.NameScope.Find<Control>(name) is { } grip)
                grip.PointerPressed += (_, args) => BeginResize(edge, args);
        }
    }

    private void BeginResize(WindowEdge edge, PointerPressedEventArgs e)
    {
        if (CanResize && WindowState == WindowState.Normal && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginResizeDrag(edge, e);
            e.Handled = true;
        }
    }
}
