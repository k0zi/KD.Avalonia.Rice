using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using KD.Avalonia.Rice.Controls;
using KD.Avalonia.Rice.Notifications;

namespace KD.Avalonia.Rice.Demo;

public partial class RibbonPage : UserControl
{
    private const double DefaultFontSize = 14;

    public RibbonPage()
    {
        InitializeComponent();
        // Every command shows its name in the status line.
        Ribbon.AddHandler(Button.ClickEvent, OnCommandClick);
        BoldButton.PropertyChanged += (_, e) =>
        {
            if (e.Property == RiceRibbonToggleButton.IsCheckedProperty)
                Editor.FontWeight = BoldButton.IsChecked ? FontWeight.Bold : FontWeight.Normal;
        };
        ItalicButton.PropertyChanged += (_, e) =>
        {
            if (e.Property == RiceRibbonToggleButton.IsCheckedProperty)
                Editor.FontStyle = ItalicButton.IsChecked ? FontStyle.Italic : FontStyle.Normal;
        };
        AlignLeft.Click += (_, _) => Editor.TextAlignment = TextAlignment.Left;
        AlignCenter.Click += (_, _) => Editor.TextAlignment = TextAlignment.Center;
        AlignRight.Click += (_, _) => Editor.TextAlignment = TextAlignment.Right;
    }

    private void OnCommandClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is RiceRibbonButton button)
            StatusText.Text = $"Command: {button.Content ?? ToolTip.GetTip(button)}";
    }

    private void OnLauncherClick(object? sender, RoutedEventArgs e)
    {
        if (sender is RiceRibbonGroup group)
            RiceToast.ShowInfo($"Here the \"{group.Header}\" dialog would open.");
    }

    private void OnGrowFont(object? sender, RoutedEventArgs e) => Editor.FontSize = Math.Min(48, Editor.FontSize + 2);

    private void OnShrinkFont(object? sender, RoutedEventArgs e) => Editor.FontSize = Math.Max(8, Editor.FontSize - 2);

    private void OnResetFont(object? sender, RoutedEventArgs e) => Editor.FontSize = DefaultFontSize;
}
