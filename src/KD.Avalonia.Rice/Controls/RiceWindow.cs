using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using KD.Avalonia.Rice.Options;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Frameless window with a custom title bar: icon and title on the left,
/// light/dark toggle, optional settings and about buttons and the window controls on the right.
/// </summary>
public class RiceWindow : Window
{
    public static readonly StyledProperty<bool> ShowThemeToggleProperty =
        AvaloniaProperty.Register<RiceWindow, bool>(nameof(ShowThemeToggle), true);

    public static readonly StyledProperty<bool> ShowSettingsButtonProperty =
        AvaloniaProperty.Register<RiceWindow, bool>(nameof(ShowSettingsButton));

    public static readonly StyledProperty<bool> ShowAboutButtonProperty =
        AvaloniaProperty.Register<RiceWindow, bool>(nameof(ShowAboutButton));

    public static readonly StyledProperty<object?> TitleBarContentProperty =
        AvaloniaProperty.Register<RiceWindow, object?>(nameof(TitleBarContent));

    public static readonly StyledProperty<ICommand?> SettingsCommandProperty =
        AvaloniaProperty.Register<RiceWindow, ICommand?>(nameof(SettingsCommand));

    public static readonly StyledProperty<RiceAppInfo> AppInfoProperty =
        AvaloniaProperty.Register<RiceWindow, RiceAppInfo>(nameof(AppInfo));

    public static readonly RoutedEvent<RoutedEventArgs> SettingsRequestedEvent =
        RoutedEvent.Register<RiceWindow, RoutedEventArgs>(nameof(SettingsRequested), RoutingStrategies.Direct);

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
    }

    public bool ShowThemeToggle
    {
        get => GetValue(ShowThemeToggleProperty);
        set => SetValue(ShowThemeToggleProperty, value);
    }

    public bool ShowSettingsButton
    {
        get => GetValue(ShowSettingsButtonProperty);
        set => SetValue(ShowSettingsButtonProperty, value);
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

    /// <summary>Executed when the settings button is clicked (after <see cref="SettingsRequested"/>).</summary>
    public ICommand? SettingsCommand
    {
        get => GetValue(SettingsCommandProperty);
        set => SetValue(SettingsCommandProperty, value);
    }

    public RiceAppInfo AppInfo
    {
        get => GetValue(AppInfoProperty);
        set => SetValue(AppInfoProperty, value);
    }

    public event EventHandler<RoutedEventArgs>? SettingsRequested
    {
        add => AddHandler(SettingsRequestedEvent, value);
        remove => RemoveHandler(SettingsRequestedEvent, value);
    }

    /// <summary>Raised before the built-in about dialog opens; set <c>Handled</c> to replace it.</summary>
    public event EventHandler<RoutedEventArgs>? AboutRequested
    {
        add => AddHandler(AboutRequestedEvent, value);
        remove => RemoveHandler(AboutRequestedEvent, value);
    }

    // Subclasses (e.g. MainWindow) must still pick up the RiceWindow control theme.
    protected override Type StyleKeyOverride => typeof(RiceWindow);

    public void RequestSettings()
    {
        RaiseEvent(new RoutedEventArgs(SettingsRequestedEvent));
        if (SettingsCommand?.CanExecute(null) == true)
            SettingsCommand.Execute(null);
    }

    public async void RequestAbout()
    {
        var args = new RoutedEventArgs(AboutRequestedEvent);
        RaiseEvent(args);
        if (!args.Handled)
            await new AboutDialog(AppInfo).ShowDialog(this);
    }

    public void ToggleMaximized() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
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
