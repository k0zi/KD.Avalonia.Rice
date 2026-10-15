using Avalonia.Interactivity;
using KD.Avalonia.Rice.Controls;

namespace KD.Avalonia.Rice.Demo;

public partial class MainWindow : RiceWindow
{
    public MainWindow()
    {
        InitializeComponent();
        SideMenu.SelectionChanged += (_, _) =>
        {
            if (SideMenu.SelectedItem is not RiceSideMenuItem item)
                return;
            PageTitle.Text = item.Content as string ?? "Hello, Rice!";
            ShowSettings(false);
        };
    }

    // Footer items are not selectable, so the menu selection is cleared while the settings page is shown.
    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        SideMenu.SelectedItem = null;
        ShowSettings(true);
    }

    private void ShowSettings(bool show)
    {
        SettingsPage.IsVisible = show;
        HomePage.IsVisible = !show;
    }

    private void OnAboutClicked(object? sender, RoutedEventArgs e) => RequestAbout();
}
