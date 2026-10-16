using Avalonia.Controls;
using Avalonia.Interactivity;
using KD.Avalonia.Rice.Controls;

namespace KD.Avalonia.Rice.Demo;

public partial class MainWindow : RiceWindow
{
    // Pages are created on first use and kept, so their state (e.g. the chat) survives navigation.
    private readonly Dictionary<object, Control> _pages = [];

    public MainWindow()
    {
        InitializeComponent();
        SideMenu.SelectionChanged += (_, _) =>
        {
            if (SideMenu.SelectedItem is RiceSideMenuItem item)
                ShowPage(item);
        };
        ShowPage(HomeItem);
    }

    // Footer items are not selectable, so the menu selection is cleared while the settings page is shown.
    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        SideMenu.SelectedItem = null;
        ShowPage(nameof(SettingsPage));
    }

    private void OnAboutClicked(object? sender, RoutedEventArgs e) => RequestAbout();

    private void ShowPage(object key)
    {
        if (!_pages.TryGetValue(key, out var page))
        {
            page = key switch
            {
                _ when key == CardItem => new CardPage(),
                _ when key == GroupBoxItem => new GroupBoxPage(),
                _ when key == ChatItem => new ChatPage(),
                _ when key == ProgressItem => new ProgressPage(),
                _ when key == UmlItem => new UmlPage(),
                _ when key == MarkdownItem => new MarkdownPage(),
                nameof(SettingsPage) => new SettingsPage(),
                _ => new HomePage(),
            };
            _pages[key] = page;
        }
        PageHost.Content = page;
    }
}
