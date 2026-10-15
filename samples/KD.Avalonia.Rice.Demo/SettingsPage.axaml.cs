using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
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
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ThemeManager.Instance.PropertyChanged += OnThemeChanged;
        UpdateCurrentTheme();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ThemeManager.Instance.PropertyChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, PropertyChangedEventArgs e) => UpdateCurrentTheme();

    private void UpdateCurrentTheme() => CurrentThemeText.Text = ThemeManager.Instance.CurrentTheme.DisplayName;
}
