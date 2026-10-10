using Avalonia.Interactivity;
using KD.Avalonia.Rice.Controls;

namespace KD.Avalonia.Rice.Demo;

public partial class MainWindow : RiceWindow
{
    public MainWindow()
    {
        InitializeComponent();
        SideMenu.SelectionChanged += (_, _) =>
            PageTitle.Text = (SideMenu.SelectedItem as RiceSideMenuItem)?.Content as string ?? "Hello, Rice!";
    }

    private async void OnSettingsRequested(object? sender, RoutedEventArgs e) =>
        await new SettingsWindow().ShowDialog(this);

    private void OnAboutClicked(object? sender, RoutedEventArgs e) => RequestAbout();
}
