using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using KD.Avalonia.Rice.Theming;
using Material.Icons;

namespace KD.Avalonia.Rice.Controls;

/// <summary>Title bar used by the <see cref="RiceWindow"/> template.</summary>
public partial class RiceTitleBar : UserControl
{
    private RiceWindow? _window;

    public RiceTitleBar()
    {
        InitializeComponent();

        DragArea.PointerPressed += OnDragAreaPointerPressed;
        ThemeToggleButton.Click += (_, _) => ThemeManager.Instance.ToggleVariant();
        SettingsButton.Click += (_, _) => _window?.RequestSettings();
        AboutButton.Click += (_, _) => _window?.RequestAbout();
        MinimizeButton.Click += (_, _) => _window?.WindowState = WindowState.Minimized;
        MaximizeButton.Click += (_, _) => _window?.ToggleMaximized();
        CloseButton.Click += (_, _) => _window?.Close();

        ToolTip.SetTip(ThemeToggleButton, RiceStrings.ToggleTheme);
        ToolTip.SetTip(SettingsButton, RiceStrings.Settings);
        ToolTip.SetTip(AboutButton, RiceStrings.About);
        ToolTip.SetTip(MinimizeButton, RiceStrings.Minimize);
        ToolTip.SetTip(CloseButton, RiceStrings.Close);
        ActualThemeVariantChanged += (_, _) => UpdateThemeIcon();
        UpdateThemeIcon();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = TopLevel.GetTopLevel(this) as RiceWindow;
        if (_window is not null)
        {
            _window.PropertyChanged += OnWindowPropertyChanged;
            UpdateMaximizeIcon();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_window is not null)
            _window.PropertyChanged -= OnWindowPropertyChanged;
        _window = null;
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty)
            UpdateMaximizeIcon();
    }

    private void OnDragAreaPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_window is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (e.ClickCount == 2)
        {
            if (_window.CanMaximize)
                _window.ToggleMaximized();
        }
        else
        {
            _window.BeginMoveDrag(e);
        }
        e.Handled = true;
    }

    // The button shows the variant it switches to.
    private void UpdateThemeIcon()
    {
        var isDark = ActualThemeVariant == ThemeVariant.Dark;
        ThemeToggleIcon.Kind = isDark ? MaterialIconKind.WeatherSunny : MaterialIconKind.WeatherNight;
    }

    private void UpdateMaximizeIcon()
    {
        var maximized = _window?.WindowState == WindowState.Maximized;
        MaximizeIcon.Kind = maximized ? MaterialIconKind.WindowRestore : MaterialIconKind.WindowMaximize;
        ToolTip.SetTip(MaximizeButton, maximized ? RiceStrings.Restore : RiceStrings.Maximize);
    }
}
