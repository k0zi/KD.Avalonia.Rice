using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using KD.Avalonia.Rice.Controls;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.Demo;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
        ChangeThemeButton.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is RiceWindow window)
                await window.ShowThemeDialog();
        };
        BlurSwitch.IsCheckedChanged += (_, _) =>
        {
            SetBlurredBackground(BlurSwitch.IsChecked == true);
            UpdateTransparencyText();
        };
    }

    // Mica (Windows 11), acrylic (Windows 10/11, macOS), blur (KDE), else plain transparency.
    private static readonly WindowTransparencyLevel[] BlurLevels =
    [
        WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur,
        WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent,
    ];

    private void SetBlurredBackground(bool blurred)
    {
        if (TopLevel.GetTopLevel(this) is not RiceWindow window)
            return;
        if (blurred)
        {
            window.TransparencyLevelHint = BlurLevels;
            // The blur shows through the translucent theme background; it follows theme switches.
            window[!TemplatedControl.BackgroundProperty] =
                window.GetResourceObservable("RiceWindowTranslucentBackgroundBrush").ToBinding();
        }
        else
        {
            window.ClearValue(TopLevel.TransparencyLevelHintProperty);
            window.ClearValue(TemplatedControl.BackgroundProperty);
        }
    }

    private void OnTransparencyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TopLevel.ActualTransparencyLevelProperty)
            UpdateTransparencyText();
    }

    private void UpdateTransparencyText()
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel)
            return;
        var level = topLevel.ActualTransparencyLevel;
        // GNOME (mutter) and X11 without KDE offer no blur protocol: the best they give is plain transparency.
        TransparencyText.Text = BlurSwitch.IsChecked == true && level == WindowTransparencyLevel.Transparent
            ? "Applied: Transparent – this desktop cannot blur windows (e.g. GNOME), only see-through"
            : $"Applied: {level}";
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ThemeManager.Instance.PropertyChanged += OnThemeChanged;
        UpdateCurrentTheme();
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            // The switch shows the window's state, as the page is kept and the window outlives it.
            BlurSwitch.IsChecked = topLevel.TransparencyLevelHint.Contains(WindowTransparencyLevel.AcrylicBlur);
            topLevel.PropertyChanged += OnTransparencyChanged;
            UpdateTransparencyText();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ThemeManager.Instance.PropertyChanged -= OnThemeChanged;
        if (e.Root is TopLevel topLevel)
            topLevel.PropertyChanged -= OnTransparencyChanged;
    }

    private void OnThemeChanged(object? sender, PropertyChangedEventArgs e) => UpdateCurrentTheme();

    private void UpdateCurrentTheme() => CurrentThemeText.Text = ThemeManager.Instance.CurrentTheme.DisplayName;
}
