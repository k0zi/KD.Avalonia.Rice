using Avalonia.Interactivity;
using KD.Avalonia.Rice.Controls;

namespace KD.Avalonia.Rice.Demo;

public partial class MainWindow : RiceWindow
{
    public MainWindow() => InitializeComponent();

    private async void OnSettingsRequested(object? sender, RoutedEventArgs e) =>
        await new SettingsWindow().ShowDialog(this);
}
