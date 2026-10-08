using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Lists the supported themes with light and dark color previews.
/// Selecting an item switches the theme immediately and persists the choice.
/// </summary>
public partial class ThemeSelector : UserControl
{
    private readonly ThemeManager _manager = ThemeManager.Instance;

    public ThemeSelector()
    {
        InitializeComponent();
        ThemeList.ItemsSource = _manager.SupportedThemes;
        ThemeList.SelectedItem = _manager.CurrentTheme;
        ThemeList.SelectionChanged += (_, _) =>
        {
            if (ThemeList.SelectedItem is RiceTheme theme)
                _manager.CurrentTheme = theme;
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _manager.PropertyChanged += OnManagerPropertyChanged;
        ThemeList.SelectedItem = _manager.CurrentTheme;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _manager.PropertyChanged -= OnManagerPropertyChanged;
    }

    private void OnManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(ThemeManager.CurrentTheme))
            ThemeList.SelectedItem = _manager.CurrentTheme;
    }
}
